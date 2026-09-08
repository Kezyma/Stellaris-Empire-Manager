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

    /// <summary>
    /// A nomad's arkship is named out of the ship names, not the planet names.
    /// </summary>
    /// <remarks>
    /// A nomadic empire the game saved itself carries <c>HUM1_SHIP_TimaphontheImplacable</c> in its
    /// <c>planet_name</c>, and that key sits in HUM1's <c>ship_names</c> block. The field is shared
    /// and the pool behind it is not.
    /// </remarks>
    [Fact]
    public void AnArkshipIsNamedFromTheShipsAndAWorldFromTheWorlds()
    {
        var database = new GameDatabase
        {
            SchemaVersion = 1,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
            NameLists =
            [
                new NameListDefinition("HUM1", "human")
                {
                    PlanetNames = ["Terra"],
                    ShipNames = ["Timaphon the Implacable"],
                },
            ],
        };

        var names = new NameGenerator(database);

        Assert.Equal("Timaphon the Implacable", names.Ship("HUM1"));
        Assert.Equal("Terra", names.Planet("HUM1"));

        // A list nobody has is no list at all, rather than someone else's ship.
        Assert.Null(names.Ship("AVI3"));
    }

    /// <summary>
    /// A list that borrows another's species borrows nothing else.
    /// </summary>
    /// <remarks>
    /// The game's README draws the line: customize_random_override changes "the random name button
    /// for species/homeworld/home system", and all three of those read common/species_names. A list
    /// keeps its own leaders, ships, fleets and colonies. Read as a general redirection, the Systems
    /// Alliance - whose HUMAN1 list is full of Johns and Peters - was offered HUM2's Merg and Japra
    /// to name its Prime Minister.
    /// </remarks>
    [Fact]
    public void AListPointingAtAnotherForItsSpeciesKeepsItsOwnPeople()
    {
        var database = new GameDatabase
        {
            SchemaVersion = 1,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
            NameLists =
            [
                new NameListDefinition("HUMAN1", "Humanoid")
                {
                    RandomNameSource = "HUM2",
                    CharacterNames = new NameSet
                    {
                        FullNames = new GenderedNames { Any = ["John Smith"] },
                    },
                },
                new NameListDefinition("HUM2", "Humanoid")
                {
                    CharacterNames = new NameSet
                    {
                        FullNames = new GenderedNames { Any = ["Japra Kap"] },
                    },
                },
            ],
        };

        var names = new NameGenerator(database);

        // The species are asked of the list it points at.
        Assert.Equal("HUM2", names.SpeciesNameSourceFor("HUMAN1"));

        // Its rulers are its own.
        Assert.Equal("John Smith", names.Ruler("HUMAN1", female: false));
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

    /// <summary>
    /// The names come likeliest first, and ties are settled alphabetically.
    /// </summary>
    /// <remarks>
    /// The order is the whole of what a reader gets from the weights. Two shapes apply here: a
    /// plain one weighted five, whose two nouns split four to one, and a wordier one weighted one,
    /// which adds a descriptor drawn evenly. So the six names carry 4, 1, 0.4, 0.4, 0.1 and 0.1 of
    /// the draw between them, and the two pairs that tie are the ones sorted by name.
    /// </remarks>
    [Fact]
    public void NamesComeLikeliestFirstThenAlphabetically()
    {
        var names = new NameGenerator(NamedEmpires()).EmpireNames(Context(), Sources);

        Assert.Equal(
            ["Vandal Empire", "Vandal Combine", "Vandal Free Empire", "Vandal Grand Empire",
             "Vandal Free Combine", "Vandal Grand Combine"],
            names.Select(n => n.Text));

        // Descending throughout, and the two ties are the alphabetical pairs above.
        Assert.Equal([.. names.Select(n => n.Weight).OrderDescending()], names.Select(n => n.Weight));
    }

    /// <summary>
    /// The weight of a name is the shape's own, narrowed by each word's share of its list.
    /// </summary>
    /// <remarks>
    /// Which is the game's draw written out, and the reason the order above is the game's order and
    /// not merely a plausible one.
    /// </remarks>
    [Fact]
    public void ANamesWeightIsItsShapesTimesItsWords()
    {
        var names = new NameGenerator(NamedEmpires()).EmpireNames(Context(), Sources);

        // Five for the shape, four fifths for "Empire" against "Combine".
        Assert.Equal(4, names.Single(n => n.Text == "Vandal Empire").Weight, 6);
        Assert.Equal(1, names.Single(n => n.Text == "Vandal Combine").Weight, 6);

        // One for the other shape, halved by the descriptor and split again by the same two nouns.
        Assert.Equal(0.4, names.Single(n => n.Text == "Vandal Free Empire").Weight, 6);
        Assert.Equal(0.1, names.Single(n => n.Text == "Vandal Grand Combine").Weight, 6);
    }

    /// <summary>
    /// Randomising draws by those weights rather than evenly across the list.
    /// </summary>
    /// <remarks>
    /// The defect this exists for: picking evenly gave the shapes with the most words in them
    /// almost every outcome, because they contribute the most rows. Of the six weight here, "Vandal
    /// Empire" holds four - so the game says it two times in three, where an even draw across the
    /// same six names would say it one time in six.
    /// </remarks>
    [Fact]
    public void RandomisingFollowsTheGamesOdds()
    {
        var generator = new NameGenerator(NamedEmpires(), new Random(7));
        var context = Context();

        var drawn = Enumerable.Range(0, 2000)
            .Select(_ => generator.Empire(context, Sources)!.Text)
            .GroupBy(t => t, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count() / 2000d, StringComparer.Ordinal);

        // Three standard errors either side of what the weights say, so the assertion is about the
        // odds rather than about this seed.
        Assert.InRange(drawn["Vandal Empire"], 0.63, 0.70);
        Assert.InRange(drawn["Vandal Combine"], 0.14, 0.20);
        Assert.InRange(drawn["Vandal Grand Combine"], 0.006, 0.028);
    }

    /// <summary>
    /// The prefix form is not offered as a name, because it is not one.
    /// </summary>
    /// <remarks>
    /// It reads like a name, which is how it got in. The game's note beside the localisation format
    /// it uses says it exists "only to generate a ship prefix acronym: 'Empire Sol' -> 'ESL'", and
    /// the player's own file bears that out - three of its empires carry AofB and none carries
    /// AofBpfx. Offering it added a hundred and forty strings to a democracy's list of nine hundred
    /// and sixty that the game would never have named an empire.
    /// </remarks>
    [Fact]
    public void ThePrefixFormIsNotAName()
    {
        var database = NamedEmpires(new EmpireNameFormat("{[This.GetSpeciesAdj] {<nouns>}}")
        {
            PrefixFormat = "{<nouns> [This.GetSpeciesAdj]}",
            Weight = 5,
        });

        var names = new NameGenerator(database).EmpireNames(Context(), Sources).Select(n => n.Text);

        Assert.Equal(["Vandal Empire", "Vandal Combine"], names);
    }

    /// <summary>Two shapes that spell the same name are one entry holding both their chances.</summary>
    [Fact]
    public void TwoWaysToTheSameNameAddUp()
    {
        var database = NamedEmpires(
            new EmpireNameFormat("{[This.GetSpeciesAdj] {<nouns>}}") { Weight = 5 },
            new EmpireNameFormat("{[This.GetSpeciesAdj] {<nouns>}}") { Weight = 3 });

        var names = new NameGenerator(database).EmpireNames(Context(), Sources);

        Assert.Equal(2, names.Count);
        Assert.Equal(6.4, names.Single(n => n.Text == "Vandal Empire").Weight, 6);
    }

    private static EmpireNameSources Sources { get; } = new() { SpeciesAdjective = "Vandal" };

    private static DesignContext Context() =>
        new EmpireRules(NamedEmpires()).CreateContext(RulesTestData.ValidEmpire());

    /// <summary>
    /// Two shapes and two word lists, weighted the way the game weights its own.
    /// </summary>
    /// <remarks>
    /// Deliberately uneven on both axes, since a set where every weight matched would pass whether
    /// the weights were read or ignored.
    /// </remarks>
    private static GameDatabase NamedEmpires(params EmpireNameFormat[] formats) => new()
    {
        SchemaVersion = 1,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
        EmpireNameParts =
        [
            new EmpireNamePartsList("nouns", [new EmpireNamePart("Empire", 4), new EmpireNamePart("Combine", 1)]),
            new EmpireNamePartsList("descs", [new EmpireNamePart("Free", 1), new EmpireNamePart("Grand", 1)]),
        ],
        EmpireNameFormats = formats.Length > 0
            ?
            [
                .. formats
            ]
            :
            [
                new EmpireNameFormat("{[This.GetSpeciesAdj] {<nouns>}}") { Weight = 5 },
                new EmpireNameFormat("{[This.GetSpeciesAdj] {<descs> {<nouns>}}}") { Weight = 1 },
            ],
    };

    private static GameDatabase Database(NameSet characters) => new()
    {
        SchemaVersion = 1,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },
        NameLists = [new NameListDefinition("MAM1", "Mammalian") { CharacterNames = characters }],
    };
}
