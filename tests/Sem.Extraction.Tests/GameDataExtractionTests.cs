using Sem.Extraction;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// Runs the extractor against the installed game.
/// </summary>
/// <remarks>
/// The counts below were each checked against the game's own files rather than taken from the
/// extractor's output, so they test the extractor rather than merely recording what it did. When a
/// patch changes them these tests fail first, and the failure says which stage to look at.
/// </remarks>
public sealed class GameDataExtractionTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static readonly Lazy<GameDatabase> Database =
        new(() => GameDataExtractor.ExtractFrom(InstallRoot!));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ExtractsTheExpectedNumberOfEachKindOfThing()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        Assert.Equal("v4.4.6", database.GameVersion);

        (string What, int Expected, int Actual)[] counts =
        [
            ("archetypes", 6, database.Archetypes.Count),
            ("species classes", 42, database.SpeciesClasses.Count),
            ("ethics", 17, database.Ethics.Count),
            ("authorities", 8, database.Authorities.Count),
            ("origins", 77, database.Civics.Count(c => c.IsOrigin)),
            ("starting ruler traits", 34, database.Traits.Count(t => t.Kind == TraitKind.StartingRuler)),
            ("portrait categories", 18, database.PortraitCategories.Count),
            ("portrait sets", 67, database.PortraitSets.Count),
            ("portraits", 496, database.Portraits.Count(p => !p.IsGroup)),
            ("portrait groups", 50, database.Portraits.Count(p => p.IsGroup)),
            ("starting worlds", 11, database.PlanetClasses.Count(p => p.IsStartingWorld)),
            ("starting systems", 23, database.Initializers.Count),
            ("advisor voices", 27, database.AdvisorVoices.Count),
            ("rooms", 41, database.Rooms.Count),
            ("flag categories", 19, database.FlagCategories.Count),
            ("flag colours", 72, database.FlagColors.Count),
            ("built-in empires", 52, database.PrescriptedEmpires.Count),
        ];

        var wrong = counts.Where(c => c.Expected != c.Actual).ToList();

        Assert.True(
            wrong.Count == 0,
            "Extraction counts changed:\r\n" +
            string.Join("\r\n", wrong.Select(c => $"  {c.What}: expected {c.Expected}, extracted {c.Actual}")));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryConditionInTheGameCompiles()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // Unknown conditions are permitted by design, but a new one means a patch has introduced
        // script the compiler does not understand, and the rules it feeds are then guesswork.
        Assert.True(
            database.UnrecognisedTriggers.Count == 0,
            "Conditions the compiler did not recognise:\r\n" +
            string.Join("\r\n", database.UnrecognisedTriggers.Select(p => $"  {p.Value,5}  {p.Key}")));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TraitBudgetsMatchTheArchetypesTheGameDefines()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        AssertBudget("BIOLOGICAL", points: 2, maxTraits: 5);
        AssertBudget("MACHINE", points: 1, maxTraits: 5);
        AssertBudget("ROBOT", points: 0, maxTraits: 4);

        // Lithoids inherit the biological allowance rather than stating their own.
        AssertBudget("LITHOID", points: 2, maxTraits: 5);

        void AssertBudget(string key, int points, int maxTraits)
        {
            var archetype = Assert.Single(database.Archetypes, a => a.Key == key);
            Assert.Equal((points, maxTraits), (archetype.TraitPoints, archetype.MaxTraits));
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EthicsCarryTheirCostsAndTheirFanaticPairings()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var militarist = Single(database.Ethics, e => e.Key == "ethic_militarist");
        Assert.Equal(1, militarist.Cost);
        Assert.Equal("mil", militarist.Category);
        Assert.Equal("ethic_fanatic_militarist", militarist.FanaticVariant);
        Assert.False(militarist.IsFanatic);

        var fanatic = Single(database.Ethics, e => e.Key == "ethic_fanatic_militarist");
        Assert.Equal(2, fanatic.Cost);
        Assert.True(fanatic.IsFanatic);
        Assert.Equal("ethic_militarist", fanatic.RegularVariant);

        var gestalt = Single(database.Ethics, e => e.Key == "ethic_gestalt_consciousness");
        Assert.Equal(3, gestalt.Cost);
        Assert.True(gestalt.IsGestalt);

        // Three points buys one fanatic plus one ordinary ethic, or three ordinary ones.
        Assert.Equal(3, database.Defines.EthicsPoints);
        Assert.Equal(2, database.Defines.CivicPoints);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TraitsCarryTheConstraintsThatBlockThem()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var aquatic = Single(database.Traits, t => t.Key == "trait_aquatic");
        Assert.Equal(2, aquatic.Cost);
        Assert.Equal("Aquatics Species Pack", aquatic.RequiredDlc);
        Assert.Contains("pc_ocean", aquatic.AllowedPlanetClasses);
        Assert.Contains("BIOLOGICAL", aquatic.AllowedArchetypes);

        // Drawbacks cost negative points, which is how they pay for beneficial traits.
        Assert.True(
            Single(database.Traits, t => t.Key == "trait_deviants").Cost < 0,
            "Deviants is a drawback and should give points back.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OppositeTraitsExcludeEachOtherBothWays()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var byKey = database.Traits.ToDictionary(t => t.Key, StringComparer.Ordinal);

        foreach (var trait in database.Traits.Where(t => t.Kind == TraitKind.Species))
        {
            foreach (var opposite in trait.Opposites)
            {
                if (byKey.TryGetValue(opposite, out var other))
                {
                    Assert.True(
                        other.Opposites.Contains(trait.Key),
                        $"{opposite} does not exclude {trait.Key}, so picking {opposite} first would " +
                        "leave the pairing allowed.");
                }
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OriginsCarryWhatTheyChangeAboutAnEmpire()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var voidDwellers = Single(database.Civics, c => c.Key == "origin_void_dwellers");
        Assert.True(voidDwellers.IsOrigin);
        Assert.Equal("pc_habitat", voidDwellers.StartingColony);
        Assert.Contains("void_dweller_system", voidDwellers.Initializers);
        Assert.Contains("trait_void_dweller_1", voidDwellers.ForcedTraits);

        // Syncretic Evolution needs the player to design a second species.
        var syncretic = Single(database.Civics, c => c.Key == "origin_syncretic_evolution");
        Assert.True(syncretic.RequiresSecondarySpecies);
        Assert.Contains("trait_syncretic_proles", syncretic.SecondarySpeciesTraits);

        // Several civics and origins change how many traits a species may take.
        var naturalDesign = Single(database.Civics, c => c.Key == "civic_natural_design");
        Assert.Contains("BIOLOGICAL_species_trait_points_add", naturalDesign.TraitBudgetModifiers.Keys);
        Assert.Contains("BIOLOGICAL_species_trait_picks_add", naturalDesign.TraitBudgetModifiers.Keys);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PortraitSetsKeepTheOrderTheGameListsThemIn()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var mammalians = Single(database.PortraitSets, s => s.Key == "mammalians");
        Assert.Equal("MAM", mammalians.SpeciesClass);

        // The game uses conditional groups with no condition purely to arrange the picker, so
        // sorting or de-duplicating here would rearrange the player's portrait list.
        Assert.Equal("mam5", mammalians.Portraits[0].Key);

        // Sets name groups as freely as they name portraits, and a group carries the likeness the
        // picker should show.
        var group = Single(database.Portraits, p => p.Key == "mam4");
        Assert.Equal("mam4_f", group.ResolvesTo);
        Assert.True(group.IsGroup);

        // Every portrait a set names must exist, or the picker would show a broken entry.
        var known = database.Portraits.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var missing = database.PortraitSets
            .SelectMany(s => s.Portraits.Select(p => p.Key))
            .Where(k => !known.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"Portrait sets name unknown portraits: {string.Join(", ", missing)}");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ContentPacksAreNamedExactlyAsTheGameMatchesThem()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // These strings are compared literally in the game's script, so a near miss silently
        // disables everything the pack unlocks.
        foreach (var name in (string[])["Utopia", "Megacorp", "The Machine Age", "Aquatics Species Pack"])
        {
            Assert.Contains(database.Dlc, d => d.Name == name);
        }

        Assert.True(database.Dlc.Count(d => d.Installed) >= 30);

        // Every pack a condition asks about must be one the database knows, or the check can
        // never be satisfied.
        var known = database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        CollectDlcNames(database, referenced);

        var unknown = referenced.Where(n => !known.Contains(n)).ToList();
        Assert.True(unknown.Count == 0, $"Conditions name unknown content packs: {string.Join(", ", unknown)}");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void LocalisationIsPrunedToWhatTheDesignerCanShow()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        var database = Database.Value;

        var all = extractor.ExtractLocalisation();
        var pruned = extractor.ExtractLocalisation(reachableFrom: database);

        // English in full is mostly event and dialogue text no empire designer will ever display.
        Assert.True(all.Count > 100_000, $"Expected the full localisation, got {all.Count} entries.");
        Assert.True(pruned.Count < all.Count / 10, $"Pruning kept {pruned.Count} of {all.Count} entries.");

        foreach (var key in (string[])
        [
            "trait_adaptive",
            "trait_adaptive_desc",
            "civic_beacon_of_liberty",
            "origin_default",
            "ethic_militarist",
            "auth_democratic",
            "SPECIES_CLASS_MUST_USE_GESTALT_CONSCIOUSNESS",
        ])
        {
            Assert.True(pruned.ContainsKey(key), $"Pruning dropped '{key}', which the designer shows.");
        }
    }

    private static T Single<T>(IEnumerable<T> items, Predicate<T> predicate) => Assert.Single(items, predicate);

    private static void CollectDlcNames(GameDatabase database, HashSet<string> names)
    {
        foreach (var requirement in database.Civics.SelectMany(c => new[] { c.Playable, c.Potential, c.Possible })
                     .Concat(database.Authorities.SelectMany(a => new[] { a.Playable, a.Possible }))
                     .Concat(database.SpeciesClasses.SelectMany(s => new[] { s.Playable, s.Possible }))
                     .Concat(database.PlanetClasses.Select(p => p.Potential)))
        {
            Walk(requirement);
        }

        void Walk(Requirement requirement)
        {
            switch (requirement)
            {
                case DlcRequirement dlc:
                    names.Add(dlc.Name);
                    break;

                case AllRequirement all:
                    foreach (var item in all.Items)
                    {
                        Walk(item);
                    }

                    break;

                case AnyRequirement any:
                    foreach (var item in any.Items)
                    {
                        Walk(item);
                    }

                    break;

                case NotRequirement not:
                    Walk(not.Item);
                    break;
            }
        }
    }
}
