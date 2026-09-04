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
    public void TheLinkIsSafeAsAPathSegmentAndNotOnlyAsAQuery()
    {
        // A link carries the empire in the path now - "…/e/<payload>" - which is nine characters
        // shorter than the query it replaced and leaves no question mark or equals sign for a chat
        // client to read as punctuation. That only holds while every character the packing can emit
        // is one a path segment takes as itself: a slash would end the segment, and a percent would
        // be decoded on the way back in and hand the decoder bytes it never wrote.
        var encoded = DesignLink.Encode(Load());

        Assert.Equal(encoded, Uri.EscapeDataString(encoded));
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('%', encoded);
        Assert.DoesNotContain('?', encoded);
        Assert.DoesNotContain('#', encoded);
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

    /// <summary>
    /// A link written by the first packing, which must go on opening for ever.
    /// </summary>
    /// <remarks>
    /// Captured from the running app before the tree packing existed. It is the only test that can
    /// fail when the old decoder is changed, because everything else round-trips through whatever
    /// the encoder currently writes and would agree with itself however both ends drifted. Somebody
    /// has this link in a chat window; this is what says so.
    /// </remarks>
    [Fact]
    public void ALinkFromTheFirstPackingStillOpens()
    {
        const string Shared =
            "AW1QTUsDMRAdPChEFPUg6EFrUPELrVLUVvbkxYs3PYcxmbVDs8mSbBWR_ndn7XopHsLMe_OReU-_kPcx9B5j"
            + "sDE5DSO4W6AU7LcsDHNNlikLuH56fRb-fDytMEhyKPha4hbsxvSOgS0MA1b077a9mYKL-UZYc1RFm7BhK5V1"
            + "R7mOkhuqak4kVK9rVDaGhgOFBr3Qx5Y_2DMGg2mSx1ybhimZ9oaDbmITliusKvzt6reVpQXcir2Xd1ZHDs2X"
            + "MEfzzNxcOpdlx0lbhZOsVcmeCv3R8Tcw0olEjH7zaCcSw9T7LsBQBN7K5HZnwen8otmfYaYkOYNMfyBfXvUV"
            + "rMIO9AfKEzoRYT3mXMSyZMvo4QFW-D3ERKaOqUnIjXHT2rMV02IoviirFGNVaEclTn1jWqRVrvEzGAr45skV"
            + "IarWMZuLbzljo2vVavYD";

        var restored = DesignLink.Decode(Shared);

        Assert.NotNull(restored);
        Assert.Equal("Tellon Concord", restored.Key);
        Assert.Equal("HUM", restored.Species.Class);
        Assert.Equal("human", restored.Species.Portrait);
        Assert.Equal("pc_continental", restored.PlanetClass);
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

        // What the packing is for, measured over this corpus rather than asserted. Version one
        // wrote the design as text and reached a median of 660 characters; writing it as a tree
        // against a frozen vocabulary reaches 372. The bounds sit clear of the measurements so an
        // empire with unusually long names does not fail the build, and close enough that losing
        // the packing would.
        //
        // The median is asserted as well as the maximum because one empire in the corpus carries a
        // four-hundred-word biography and dominates the maximum on its own - a change that doubled
        // every other link would still pass a bound set only by that one.
        var sorted = lengths.Order().ToList();
        var median = sorted[sorted.Count / 2];
        var longest = sorted[^1];

        Assert.True(
            median <= 450,
            $"The median link is {median} characters; shortest {sorted[0]}, longest {longest}.");

        Assert.True(
            longest <= 900,
            $"The longest link is {longest} characters, median {median}.");
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
