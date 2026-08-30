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
    public void TheEngineOwnTemplatesAreComposedRatherThanLookedUp()
    {
        // Neither %ADJECTIVE% nor %ADJ% appears anywhere in the game's text: the engine builds them.
        // Looked up and not found, an adjective came out as the species' own name.
        var species = Species();
        NameWriter.Species(species, Oxanalytor);

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["SPEC_Oxanalytor"] = "Oxanalytor",
        });

        Assert.Equal("Oxanalytoran", localizer.Name(species.Adjective));
    }

    [Fact]
    public void AWholeEmpireNameComesBackOutOfItsNestedParts()
    {
        // The Blessed Oxanalytoran Union, exactly as the player's own file stores it: a wrapper over
        // a descriptor over an adjective over the species, with the noun hanging off the adjective.
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["SPEC_Oxanalytor"] = "Oxanalytor",
        });

        Assert.Equal("Blessed Oxanalytoran Union", localizer.Name(design.Name));
    }

    [Fact]
    public void ALeaderWearsBothHalvesOfTheirName()
    {
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Lawrence",
            ["HUMAN1_CHR_Whitfield"] = "Whitfield",
        });

        Assert.Equal("Lawrence Whitfield", localizer.Name(design.Ruler.Name.FullNames));
    }

    [Theory]
    [InlineData("%LEADER_1%")]
    [InlineData("%LEADER_2%")]
    public void EitherLeaderTemplateWearsBothHalvesOfTheName(string template)
    {
        // Both forms hold a given name and a family name, and both mean the whole name. Reading only
        // the first variable of %LEADER_1% cost twelve of the player's own rulers their surname.
        var design = EmpireDesignsFile.LoadText(
            BlessedOxanalytoranUnion.Replace("%LEADER_2%", template, StringComparison.Ordinal)).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Lawrence",
            ["HUMAN1_CHR_Whitfield"] = "Whitfield",
        });

        Assert.Equal("Lawrence Whitfield", localizer.Name(design.Ruler.Name.FullNames));
    }

    [Fact]
    public void AFamilyNameWrittenRoundAGivenOneWrapsIt()
    {
        // Six hundred and thirty-six of the game's name parts are frames rather than words: the
        // family name HUMAN3_CHR_Aburius is "$1$ Aburia", and the given name goes in the hole. Set
        // side by side with the hole deleted, which is what happened before, this read
        // "Lawrence Aburia" with a stray space, or worse.
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Gaius",
            ["HUMAN1_CHR_Whitfield"] = "$1$ Aburia",
        });

        Assert.Equal("Gaius Aburia", localizer.Name(design.Ruler.Name.FullNames));
    }

    [Fact]
    public void AGivenNameWrittenRoundAFamilyOneWrapsItToo()
    {
        // The other seventy-four hold the hole at the end instead: "Feathers of $1$" over "Silver".
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Feathers of $1$",
            ["HUMAN1_CHR_Whitfield"] = "Silver",
        });

        Assert.Equal("Feathers of Silver", localizer.Name(design.Ruler.Name.FullNames));
    }

    [Theory]
    [InlineData("male", "Gaius Aburius")]
    [InlineData("female", "Gaius Aburia")]
    [InlineData(null, "Gaius Aburia")]
    public void ANameWrittenInTwoFormsPicksTheOneThatSuits(string? gender, string expected)
    {
        // Four hundred and sixty-three name parts carry two forms, and the game picks by gender.
        // Nothing here knew the syntax, so the bars and the tag were shown to the player as though
        // they were part of the name.
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Gaius",
            ["HUMAN1_CHR_Whitfield"] = "$1$ Aburia|||masc:$1$ Aburius",
        });

        Assert.Equal(expected, localizer.Name(design.Ruler.Name.FullNames, gender: gender));
    }

    [Fact]
    public void ARulersNameIsReadTheSameWayWhicheverShapeHoldsIt()
    {
        // A design copied out of a running game keeps the name in two parts rather than one, and
        // half the screens that showed a ruler had forgotten that case.
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["HUMAN1_CHR_Lawrence"] = "Lawrence",
            ["HUMAN1_CHR_Whitfield"] = "Whitfield",
        });

        Assert.Equal("Lawrence Whitfield", localizer.RulerName(design.Ruler));
    }

    [Fact]
    public void ANamePartWithNoTextIsShownAsTheWordsItSpells()
    {
        // The game's empire name parts have no text of their own — "Corporate_Alliance" is not in
        // any of its files — and it writes them out as they read. Shown as the key, the underscore
        // came with it.
        var design = EmpireDesignsFile.LoadText(HumanCorporateAlliance).Designs[0];

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["PRESCRIPTED_species_adjective_humans1"] = "Human",
        });

        Assert.Equal("Human Corporate Alliance", localizer.Name(design.Name));
    }

    /// <summary>Another of the player's own, and the one whose noun has an underscore in it.</summary>
    private const string HumanCorporateAlliance = """
        "Human Corporate Alliance"=
        {
        	key="Human Corporate Alliance"
        	name=
        	{
        		key="%ADJ%"
        		variables=
        		{
        			{
        				key="1"
        				value=
        				{
        					key="PRESCRIPTED_species_adjective_humans1"
        					variables=
        					{
        						{ key="1" value={ key="Corporate_Alliance" } }
        					}
        				}
        			}
        		}
        	}
        }
        """;

    [Fact]
    public void ANamesOwnVariablesBeatTheGamesTextOfTheSameName()
    {
        var design = EmpireDesignsFile.LoadText(BlessedOxanalytoranUnion).Designs[0];

        // A design's variables are the more specific of the two, so an entry that happens to share a
        // placeholder's name must not shadow them.
        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["Blessed"] = "Blessed $1$",
            ["1"] = "SOMETHING ELSE",
            ["SPEC_Oxanalytor"] = "Oxanalytor",
        });

        Assert.Equal("Blessed Oxanalytoran Union", localizer.Name(design.Name));
    }

    /// <summary>
    /// One of the player's own empires, copied out of their file unchanged.
    /// </summary>
    private const string BlessedOxanalytoranUnion = """
        "Blessed Oxanalytoran Union"=
        {
        	key="Blessed Oxanalytoran Union"
        	name=
        	{
        		key="%ADJ%"
        		variables=
        		{
        			{
        				key="1"
        				value=
        				{
        					key="Blessed"
        					variables=
        					{
        						{
        							key="1"
        							value=
        							{
        								key="%ADJECTIVE%"
        								variables=
        								{
        									{
        										key="adjective"
        										value={ key="SPEC_Oxanalytor" }
        									}
        									{
        										key="1"
        										value={ key="Union" }
        									}
        								}
        							}
        						}
        					}
        				}
        			}
        		}
        	}
        	ruler=
        	{
        		name=
        		{
        			full_names=
        			{
        				key="%LEADER_2%"
        				variables=
        				{
        					{ key="1" value={ key="HUMAN1_CHR_Lawrence" } }
        					{ key="2" value={ key="HUMAN1_CHR_Whitfield" } }
        				}
        			}
        		}
        	}
        }
        """;

    [Fact]
    public void APlaceholderNothingFilledIsDroppedRatherThanShown()
    {
        // The Commonwealth of Man's species adjective is stored as the key whose text is
        // "Human $1$" — the same entry serves the empire's own name, where something does fill it.
        // Read as the species' adjective there is nothing to fill it with, and the field showed the
        // machinery.
        var species = Species();
        NameWriter.Write(species.Adjective, "PRESCRIPTED_species_adjective_humans2", text: null);

        var localizer = new Localizer(new Dictionary<string, string>
        {
            ["PRESCRIPTED_species_adjective_humans2"] = "Human $1$",
        });

        Assert.Equal("Human", localizer.Name(species.Adjective));
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
