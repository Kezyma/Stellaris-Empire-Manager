using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// The game keeps no adjective beside a species name — it rewrites the ending — and the designer
/// offers ready-made species that must arrive complete. These check both.
/// </summary>
public sealed class NameGeneratorTests
{
    [Theory]

    // The game's own suffix rules, longest match first.
    [InlineData("Alari", "Alarian")]
    [InlineData("Jhabbanid", "Jhabbanan")]
    [InlineData("Mireesh", "Mireesh")]
    [InlineData("Rethar", "Retharan")]
    [InlineData("Cybrex", "Cybrex")]
    [InlineData("Torinus", "Torinan")]
    [InlineData("Sathyrelia", "Sathyrelian")]
    public void AnAdjectiveIsTheNameWithItsEndingRewritten(string name, string expected)
    {
        Assert.Equal(expected, NameGenerator.Adjective(name));
    }

    [Fact]
    public void ANameMatchingNoRuleKeepsItsOwnForm()
    {
        // What the game falls back to as well. An invented adjective would be worse than the name.
        Assert.Equal("Zzk", NameGenerator.Adjective("Zzk"));
    }

    [Fact]
    public void ASuggestedSpeciesArrivesCompleteAndSuitsItsClass()
    {
        var database = new GameDatabase
        {
            SchemaVersion = 1,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
            SpeciesNames =
            [
                new SpeciesNameSuggestion("MAM", "Vandal")
                {
                    Plural = "Vandals",
                    HomePlanet = "Vandalia",
                    HomeSystem = "Vandal",
                    NameList = "MAM1",
                },
                new SpeciesNameSuggestion("AVI", "Kelthi") { Plural = "Kelthi" },
            ],
        };

        var suggestion = new NameGenerator(database, new Random(1)).Species("MAM");

        Assert.NotNull(suggestion);
        Assert.Equal("Vandal", suggestion.Name);
        Assert.Equal("Vandals", suggestion.Plural);
        Assert.Equal("Vandalia", suggestion.HomePlanet);
        Assert.Equal("MAM1", suggestion.NameList);
    }

    [Fact]
    public void AClassTheGameNamesNoSpeciesForStillGetsOne()
    {
        var database = new GameDatabase
        {
            SchemaVersion = 1,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
            SpeciesNames = [new SpeciesNameSuggestion("MAM", "Vandal") { Plural = "Vandals" }],
        };

        // Falling back to the whole set beats leaving the button doing nothing.
        Assert.NotNull(new NameGenerator(database, new Random(1)).Species("LITHOID"));
    }

    [Fact]
    public void ARulerJoinsAFirstNameToAFamilyName()
    {
        var database = Database(new NameSet
        {
            FirstNames = new GenderedNames { Male = ["Tig"], Female = ["Tigi"] },
            SecondNames = new GenderedNames { Any = ["J'Khanna"] },
        });

        var generator = new NameGenerator(database, new Random(1));

        Assert.Equal("Tig J'Khanna", generator.Ruler("MAM1"));
        Assert.Equal("Tigi J'Khanna", generator.Ruler("MAM1", female: true));
    }

    [Fact]
    public void AnUngenderedListStandsInWhereThereIsNoGenderedOne()
    {
        var database = Database(new NameSet
        {
            FirstNames = new GenderedNames { Any = ["Ekko"] },
            SecondNames = new GenderedNames { Any = ["Vail"] },
        });

        Assert.Equal("Ekko Vail", new NameGenerator(database, new Random(1)).Ruler("MAM1", female: true));
    }

    [Fact]
    public void AListThatNamesItsLeadersOutrightNeedsNoFamilyName()
    {
        // The human lists work this way: complete names by gender, and no first names at all. Read
        // only for the ungendered fields, a list like this looks empty and the button does nothing.
        var database = Database(new NameSet
        {
            FullNames = new GenderedNames { Male = ["Falatir"], Female = ["Falatira"] },
        });

        var generator = new NameGenerator(database, new Random(1));

        Assert.Equal("Falatir", generator.Ruler("MAM1"));
        Assert.Equal("Falatira", generator.Ruler("MAM1", female: true));
    }

    [Fact]
    public void AListWithNoNamesGivesNoneRatherThanSomethingEmpty()
    {
        Assert.Null(new NameGenerator(Database(new NameSet()), new Random(1)).Ruler("MAM1"));
        Assert.Null(new NameGenerator(Database(new NameSet()), new Random(1)).Ruler("not-a-list"));
    }

    private static GameDatabase Database(NameSet characters) => new()
    {
        SchemaVersion = 1,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
        NameLists = [new NameListDefinition("MAM1", "Mammalian") { CharacterNames = characters }],
    };
}
