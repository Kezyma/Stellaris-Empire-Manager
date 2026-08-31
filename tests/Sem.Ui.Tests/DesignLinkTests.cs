using Sem.Designs;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Sending someone an empire.
/// </summary>
/// <remarks>
/// The encoding is opaque on purpose: a design is a species with templated names, a flag, a ruler
/// and every trait, and a readable parameter per field would need a parser per field and would
/// silently drop whatever was added next. Carrying the design itself cannot drift out of step with
/// what a design is.
/// </remarks>
public sealed class DesignLinkTests
{
    private const string Sample = """
        "Peacock Dynamics"=
        {
        	key="Peacock Dynamics"
        	species=
        	{
        		class="AVI"
        		portrait="avi18"
        		species_name=
        		{
        			key="Peacock"
        			literal=yes
        		}
        		trait="trait_aquatic"
        		trait="trait_deviants"
        	}
        	name=
        	{
        		key="Peacock Dynamics"
        		literal=yes
        	}
        	authority="auth_corporate"
        	ethic="ethic_fanatic_militarist"
        	ethic="ethic_xenophile"
        	planet_class="pc_tropical"
        }
        """;

    private static EmpireDesign Load() => EmpireDesignsFile.LoadText(Sample).Designs[0];

    [Fact]
    public void AnEmpireComesBackFromALinkExactlyAsItWent()
    {
        var original = Load();

        var restored = DesignLink.Decode(DesignLink.Encode(original));

        Assert.NotNull(restored);
        Assert.Equal(original.Key, restored.Key);
        Assert.Equal(original.Authority, restored.Authority);
        Assert.Equal(original.PlanetClass, restored.PlanetClass);
        Assert.Equal(original.Ethics, restored.Ethics);
        Assert.Equal(original.Species.Portrait, restored.Species.Portrait);
        Assert.Equal(original.Species.Traits, restored.Species.Traits);
        Assert.Equal(original.Species.Name.Key, restored.Species.Name.Key);
        Assert.True(restored.Species.Name.IsLiteral);
    }

    [Fact]
    public void TheLinkCarriesNothingAUrlWouldObjectTo()
    {
        var encoded = DesignLink.Encode(Load());

        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }

    [Fact]
    public void AnEmpireWithAQuotationMarkInItsNameStillTravels()
    {
        // The key is written between quotation marks, so one inside it closed them early and the
        // link came back as nothing at all — quietly, since a link that will not parse is treated
        // as no design.
        var design = Load();
        design.Rename("The \"Peacock\" Dynamics");

        var restored = DesignLink.Decode(DesignLink.Encode(design));

        Assert.NotNull(restored);
        Assert.Equal(design.Authority, restored.Authority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 at all!")]
    [InlineData("QUJD")]
    public void ALinkThatWillNotUnpackIsNoDesignRatherThanAnError(string? encoded)
    {
        // Chat clients truncate, people mistype, and links go stale. None of that should throw on
        // the way into a page.
        Assert.Null(DesignLink.Decode(encoded));
    }

    [Fact]
    public void ALinkCutShortIsNoDesignRatherThanAnError()
    {
        // Cutting the packed bytes anywhere lands part way through a compressed block or part way
        // through a coded run, and both have to come back as nothing rather than as an exception on
        // the way into a page.
        var encoded = DesignLink.Encode(Load());

        for (var length = 1; length < encoded.Length; length++)
        {
            Assert.Null(Record.Exception(() => DesignLink.Decode(encoded[..length])));
        }
    }

    [Theory]
    [InlineData("key= variables= trait_")]
    [InlineData("Ærøskøbing")]
    [InlineData("東京")]
    [InlineData("A {braced} name")]
    [InlineData("An = equals sign")]
    [InlineData("A \"quoted\" {braced} name")]
    [InlineData("key=\" variables={ trait=\"trait_ Ærøskøbing 東京")]
    public void TextThatLooksLikeTheCodingIsCarriedAsItself(string name)
    {
        // The packing replaces runs of text with codes, so an empire named after one of those runs
        // is the case where a coder and a decoder can disagree. Accented letters are here for the
        // same reason: the codes are bytes, and a character that takes two of them must not be cut
        // down the middle.
        var design = Load();
        design.Rename(name);

        var encoded = DesignLink.Encode(design);
        var restored = DesignLink.Decode(encoded);

        Assert.NotNull(restored);
        Assert.Equal(design.Key, restored.Key);
        Assert.Equal(encoded, DesignLink.Encode(restored));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryEmpireInTheCorpusComesBackAsTheSameLink()
    {
        // Re-encoding is the strongest statement of what this has to do: if a restored design packs
        // to the same string it was unpacked from, then nothing was lost, added or reordered on the
        // way — without this test having to know one field of a design from another.
        var files = DesignFiles();
        Skip.If(files.Count == 0, "Sandbox copies are missing. Run: dotnet run --project src/Sem.Cli -- devsync");

        var lengths = new List<int>();

        foreach (var path in files)
        {
            foreach (var design in EmpireDesignsFile.Load(File.ReadAllBytes(path)).Designs)
            {
                var encoded = DesignLink.Encode(design);
                var restored = DesignLink.Decode(encoded);

                Assert.NotNull(restored);
                Assert.Equal(design.Key, restored.Key);
                Assert.Equal(encoded, DesignLink.Encode(restored));

                lengths.Add(encoded.Length);
            }
        }

        Assert.True(lengths.Count > 15, $"Only {lengths.Count} empires were packed; the corpus should be larger.");

        // What the packing is for. Before the compact writing and the table of common runs, these
        // same empires averaged 1,129 characters and the longest was 1,258. The bound is well clear
        // of the measured maximum so that an empire with unusually long names does not fail the
        // build, but close enough that losing the packing would.
        var longest = lengths.Max();

        Assert.True(
            longest <= 1_000,
            $"The longest link is {longest} characters, averaging {lengths.Average():F0}.");
    }

    /// <summary>The player's own designs, as copied into the sandbox. Never the originals.</summary>
    private static IReadOnlyList<string> DesignFiles() =>
        Repository() is { } root && Directory.Exists(Path.Combine(root, "sandbox", "userdata"))
            ? [.. Directory.EnumerateFiles(
                Path.Combine(root, "sandbox", "userdata"),
                // The live file only; the dated backups beside it hold empires from earlier versions.
                "user_empire_designs_v3.4.txt")]
            : [];

    private static string? Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "sandbox")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
