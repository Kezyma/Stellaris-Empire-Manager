using Sem.GameData;

namespace Sem.Core.Tests.GameData;

/// <summary>
/// That each by-key lookup answers from its own collection, and only from its own.
/// </summary>
/// <remarks>
/// The indexes are held in an array, one slot per lookup, and the slot is a number written out by
/// hand beside each one. A collision there cannot be caught by asking the lookups questions: each
/// index remembers the list it was built from and rebuilds when handed another, so two lookups
/// sharing a slot still give correct answers - they simply rebuild over each other for ever, at
/// exactly the cost the index exists to avoid. It is a performance fault wearing correctness's
/// clothes, so the slots are checked as what they are, by reading them.
/// </remarks>
public sealed class DatabaseLookupTests
{
    /// <summary>A database with exactly one thing in every collection a lookup reads.</summary>
    private static GameDatabase Database { get; } = new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Archetypes = [new ArchetypeDefinition("archetype", 2, 5, false)],
        SpeciesClasses = [new SpeciesClassDefinition("species_class", null)],
        Traits = [new TraitDefinition("trait", TraitKind.Species)],
        Ethics = [new EthicDefinition("ethic", 1, "category")],
        Authorities = [new AuthorityDefinition("authority")],
        Civics = [new CivicDefinition("civic", false)],
        AscensionPerks = [new AscensionPerkDefinition("perk")],
        TraditionTrees = [new TraditionTreeDefinition("tree")],
        Traditions = [new TraditionDefinition("tradition")],
        GovernmentTypes = [new GovernmentTypeDefinition("government", 1, 0)],
        Personalities = [new PersonalityDefinition("personality", 1, 0)],
        PlanetClasses = [new PlanetClassDefinition("planet_class")],
        Portraits = [new PortraitDefinition("portrait")],
        PortraitSets = [new PortraitSetDefinition("portrait_set", null)],
        NameLists = [new NameListDefinition("name_list", null)],
        Initializers = [new InitializerDefinition("initializer", InitializerUsage.CustomEmpire)],
        AdvisorVoices = [new AdvisorVoiceDefinition("advisor", "advisor_name")],
        Rooms = [new RoomDefinition("room")],
        GraphicalCultures = [new GraphicalCultureDefinition("graphical_culture")],
        ShipSets = [new ShipSetDefinition("ship_set", "ship_set_name")],
        LeaderClasses = [new LeaderClassDefinition("leader_class", "leader_class_name")],
        FlagCategories = [new FlagCategoryDefinition("flag_category", false)],
        FlagColors = [new FlagColorDefinition("flag_color", 1, 2, 3)],
        EmpireFlagSets = [new EmpireFlagSet("flag_set", [])],
        Arkships = [new ArkshipDefinition("arkship")],
    };

    /// <summary>Each lookup, the key its own collection holds, and every key it must refuse.</summary>
    public static TheoryData<string, Func<GameDatabase, string, bool>> Lookups()
    {
        var data = new TheoryData<string, Func<GameDatabase, string, bool>>();

        void Add(string key, Func<GameDatabase, string, object?> find) =>
            data.Add(key, (db, k) => find(db, k) is not null);

        Add("archetype", (db, k) => db.Archetype(k));
        Add("species_class", (db, k) => db.SpeciesClass(k));
        Add("trait", (db, k) => db.Trait(k));
        Add("ethic", (db, k) => db.Ethic(k));
        Add("authority", (db, k) => db.Authority(k));
        Add("civic", (db, k) => db.Civic(k));
        Add("perk", (db, k) => db.AscensionPerk(k));
        Add("tree", (db, k) => db.TraditionTree(k));
        Add("tradition", (db, k) => db.Tradition(k));
        Add("government", (db, k) => db.GovernmentType(k));
        Add("personality", (db, k) => db.Personality(k));
        Add("planet_class", (db, k) => db.PlanetClass(k));
        Add("portrait", (db, k) => db.Portrait(k));
        Add("portrait_set", (db, k) => db.PortraitSet(k));
        Add("name_list", (db, k) => db.NameList(k));
        Add("initializer", (db, k) => db.Initializer(k));
        Add("advisor", (db, k) => db.AdvisorVoice(k));
        Add("room", (db, k) => db.Room(k));
        Add("graphical_culture", (db, k) => db.GraphicalCulture(k));
        Add("ship_set", (db, k) => db.ShipSet(k));
        Add("leader_class", (db, k) => db.LeaderClass(k));
        Add("flag_category", (db, k) => db.FlagCategory(k));
        Add("flag_color", (db, k) => db.FlagColor(k));
        Add("flag_set", (db, k) => db.EmpireFlagSet(k));
        Add("arkship", (db, k) => db.Arkship(k));

        return data;
    }

    [Theory]
    [MemberData(nameof(Lookups))]
    public void EachLookupFindsWhatItsOwnCollectionHolds(string key, Func<GameDatabase, string, bool> finds) =>
        Assert.True(finds(Database, key), $"'{key}' was not found by its own lookup.");

    /// <summary>And answers nothing for a key that belongs to one of the others.</summary>
    [Theory]
    [MemberData(nameof(Lookups))]
    public void EachLookupRefusesEveryOtherCollectionsKeys(string key, Func<GameDatabase, string, bool> finds)
    {
        foreach (var other in Lookups().Select(row => (string)row[0]).Where(k => k != key))
        {
            Assert.False(finds(Database, other), $"'{key}'s lookup also found '{other}'.");
        }
    }

    /// <summary>Asked repeatedly and in turn, since each lookup holds state between calls.</summary>
    [Fact]
    public void TheAnswersHoldWhenTheLookupsAreInterleaved()
    {
        var rows = Lookups().Select(row => ((string)row[0], (Func<GameDatabase, string, bool>)row[1])).ToList();

        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var (key, finds) in rows)
            {
                Assert.True(finds(Database, key), $"'{key}' was lost on pass {pass}.");
            }
        }
    }

    /// <summary>A key nothing defines is nothing, and so is no key at all.</summary>
    [Fact]
    public void NothingIsFoundForAKeyNobodyDefines()
    {
        Assert.Null(Database.Trait("no_such_trait"));
        Assert.Null(Database.Trait(null));
        Assert.Null(Database.Trait(string.Empty));
    }

    /// <summary>
    /// A database made by <c>with</c> answers from the list it was given, not the one it replaced.
    /// </summary>
    /// <remarks>
    /// A record's copy constructor copies the index array, and the extractor finishes with
    /// <c>database with { Portraits = … }</c>. Without the source-list check beside each index, the
    /// copy would go on answering from the list it had just replaced.
    /// </remarks>
    [Fact]
    public void AReplacedCollectionIsTheOneThatAnswers()
    {
        // Asked first, so the original's index is built and copied.
        Assert.NotNull(Database.Portrait("portrait"));

        var replaced = Database with { Portraits = [new PortraitDefinition("other_portrait")] };

        Assert.NotNull(replaced.Portrait("other_portrait"));
        Assert.Null(replaced.Portrait("portrait"));
    }

    /// <summary>Last definition wins, and a repeated key does not throw - the game's own load order.</summary>
    [Fact]
    public void ARepeatedKeyTakesTheLastDefinitionRatherThanThrowing()
    {
        var doubled = Database with
        {
            Rooms = [new RoomDefinition("room") { Image = "first" }, new RoomDefinition("room") { Image = "last" }],
        };

        Assert.Equal("last", doubled.Room("room")?.Image);
    }

    /// <summary>
    /// Every lookup owns a slot of its own, and together they fill the array exactly.
    /// </summary>
    /// <remarks>
    /// Read out of the source, because there is nowhere else the invariant lives: the slots are
    /// literals in the argument list and their only constraint is that no two agree. Deliberately
    /// brittle - if the shape of those lines changes this fails loudly rather than quietly passing
    /// on nothing, which is the failure a check like this is otherwise prone to.
    /// </remarks>
    [Fact]
    public void EveryLookupOwnsASlotOfItsOwn()
    {
        var source = LookupSource();
        var slots = System.Text.RegularExpressions.Regex
            .Matches(source, @"Find\((\w+), \w+ => \w+\.Key, key, (\d+)\);")
            .Select(m => (Collection: m.Groups[1].Value, Slot: int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        Assert.Equal(25, slots.Count);
        Assert.Equal(slots.Count, slots.Select(s => s.Slot).Distinct().Count());
        Assert.Equal(slots.Count, slots.Select(s => s.Collection).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, slots.Count), slots.Select(s => s.Slot).Order());

        // And the array is exactly as long as the slots need it to be.
        Assert.Contains($"new (object, object)?[{slots.Count}]", source, StringComparison.Ordinal);
    }

    /// <summary>The lookup source, found by walking up from the test binary to the repository.</summary>
    private static string LookupSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Sem.GameData", "GameDatabase.Lookup.cs");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("GameDatabase.Lookup.cs was not found above the test binary.");
    }
}
