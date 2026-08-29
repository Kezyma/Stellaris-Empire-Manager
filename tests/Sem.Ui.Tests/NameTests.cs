using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// How a name gets into a design and back out of it.
/// </summary>
/// <remarks>
/// The game keeps two kinds of name and the difference matters. A name chosen from one of its own
/// lists is a localisation key, so the empire reads correctly in whatever language it is opened in;
/// a name the player typed is text, and stays exactly as typed. Writing everything as text quietly
/// translates every empire into English for good, and reading a key as though it were text is how a
/// built-in empire came to be called <c>PRESCRIPTED_species_name_iferyx</c>.
/// </remarks>
public sealed class NameTests
{
    private const string Sample = """
        "Test"=
        {
        	key="Test"
        	species=
        	{
        		class="MAM"
        	}
        	name=
        	{
        		key="Test"
        		literal=yes
        	}
        }
        """;

    private static SpeciesDesign Species() =>
        EmpireDesignsFile.LoadText(Sample).Designs[0].Species;

    private static readonly SpeciesNameSuggestion Oxanalytor =
        new("TOX", "Oxanalytor")
        {
            Plural = "Oxanalytors",
            NameKey = "SPEC_Oxanalytor",
            PluralKey = "SPEC_Oxanalytor_pl",
        };

    [Fact]
    public void ASpeciesChosenFromTheGamesListIsStoredByItsKey()
    {
        var species = Species();

        NameWriter.Species(species, Oxanalytor);

        Assert.Equal("SPEC_Oxanalytor", species.Name.Key);
        Assert.False(species.Name.IsLiteral);

        Assert.Equal("SPEC_Oxanalytor_pl", species.Plural.Key);
        Assert.False(species.Plural.IsLiteral);
    }

    [Fact]
    public void TheAdjectiveIsStoredAsTheGameStoresIt()
    {
        // Not "Oxanalytoran". The game keeps the template and the species it is formed from, and
        // builds the word when it shows it.
        var species = Species();

        NameWriter.Species(species, Oxanalytor);

        Assert.Equal(LocRef.AdjectiveTemplate, species.Adjective.Key);
        Assert.False(species.Adjective.IsLiteral);

        var variable = Assert.Single(species.Adjective.Variables);
        Assert.Equal("adjective", variable.Key);
        Assert.Equal("SPEC_Oxanalytor", variable.Value?.Key);
    }

    [Fact]
    public void ANameTheGameNeverHeardOfIsKeptExactlyAsTyped()
    {
        var species = Species();

        NameWriter.Write(species.Name, key: null, text: "Peacock");

        Assert.Equal("Peacock", species.Name.Key);
        Assert.True(species.Name.IsLiteral);
    }

    [Fact]
    public void AStoredKeyIsShownAsTheWordItStandsFor()
    {
        var species = Species();
        NameWriter.Species(species, Oxanalytor);

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["SPEC_Oxanalytor"] = "Oxanalytor",
            ["SPEC_Oxanalytor_pl"] = "Oxanalytors",
            [LocRef.AdjectiveTemplate] = "$adjective$an",
        });

        Assert.Equal("Oxanalytor", localizer.Name(species.Name));
        Assert.Equal("Oxanalytors", localizer.Name(species.Plural));
        Assert.Equal("Oxanalytoran", localizer.Name(species.Adjective));
    }

    [Fact]
    public void ATemplateTheGameHasNoTextForFallsBackToWhatItWasMadeFrom()
    {
        // Better the species' own name than the word "%ADJECTIVE%" in a field a player is reading.
        var species = Species();
        NameWriter.Species(species, Oxanalytor);

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["SPEC_Oxanalytor"] = "Oxanalytor",
        });

        Assert.Equal("Oxanalytor", localizer.Name(species.Adjective));
    }

    [Fact]
    public void TextThePlayerTypedIsShownBackUnchanged()
    {
        var species = Species();
        NameWriter.Write(species.Name, key: null, text: "SPEC_Oxanalytor");

        // Literal text is never looked up, even when it happens to read like a key.
        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["SPEC_Oxanalytor"] = "Oxanalytor",
        });

        Assert.Equal("SPEC_Oxanalytor", localizer.Name(species.Name));
    }
}
