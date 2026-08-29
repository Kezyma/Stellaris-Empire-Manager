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
}
