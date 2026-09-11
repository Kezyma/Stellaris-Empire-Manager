using Sem.Extraction;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
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
            // Nine, not eleven: a volcanic world and an arkship are each flagged initial, and each
            // is also flagged starting_planet = no. Neither is a world a player may simply pick —
            // an Infernal species class adds the first, and being nomadic puts you on the second.
            // Sixty-nine, not seventy: the folder holds nine random_list blocks naming groups of
            // worlds for the galaxy generator, and read as classes they collapsed into a single
            // phantom that sat in the shipped data being counted as a world.
            ("planet classes", 69, database.PlanetClasses.Count),
            ("starting worlds", 9, database.PlanetClasses.Count(p => p.IsStartingWorld)),
            ("starting systems", 23, database.Initializers.Count),
            ("advisor voices", 27, database.AdvisorVoices.Count),
            // Ninety-one: every room the installation has a picture of. Forty-one the game's own
            // designer offers, twenty-five more it hands out during play, and twenty-five it draws
            // only for an event - the Contingency's transmission, the Shroud, the enclaves - which
            // are the same artwork in the same frame and which a design may name. The room selector
            // names one further, synth_queen_room, that no installation has ever had a picture for.
            ("rooms", 91, database.Rooms.Count),
            ("rooms the designer offers", 41, database.Rooms.Count(r => r.IsOffered)),

            // Twenty-one named sets of country flags, carried by the game's own empires.
            ("empire flag sets", 21, database.EmpireFlagSets.Count),

            // Twenty-two: twenty-one emblem categories and the backgrounds. Three of the emblem
            // categories carry show_in_designer = no - the enclaves, the pre-FTL ages and the
            // special emblems - which keeps them out of the game's own picker and not out of a
            // design: its scripts hand them to empires by writing the category and file a design
            // stores, and one built with them was checked in game and loads.
            ("flag categories", 22, database.FlagCategories.Count),
            ("flag categories the designer offers", 19, database.FlagCategories.Count(c => c.IsOffered)),
            ("emblems", 323, database.FlagCategories.Where(c => !c.IsBackground).Sum(c => c.Files.Count)),
            ("emblems the designer offers", 283,
                database.FlagCategories.Where(c => !c.IsBackground && c.IsOffered).Sum(c => c.Files.Count)),
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
    public void OnlyTheConditionsThatNeedAGameInProgressGoUnread()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // The conditions on a conditional modifier are a separate matter from the ones that gate an
        // option: a modifier may well depend on something only a running game can answer, and
        // RequirementEvaluator.CanDecide exists so those are left out of the totals rather than
        // guessed at. Every one of these is of that kind — whether a tradition has been adopted,
        // whether a scope exists, whether anybody has been made a rival.
        //
        // Pinned by name because the next one would not be. A patch introducing one this app could
        // answer, but does not, would quietly go on leaving a bonus out of every total.
        string[] known =
        [
            "exists",
            "has_active_tradition",
            "has_tradition",
            "is_species_class",
            "is_scope_valid",

            // The five that arrived with the triggered blocks and the tradition swaps, neither of
            // which was compiled before. Rivals, federations and an economy are all things a design
            // does not have yet, and calc_true_if counts over them.
            "any_rival_country",
            "resource_expenses_compare",
            "calc_true_if",
            "has_federation",
            "federation",
        ];

        var unexpected = database.UnrecognisedEffectConditions.Keys.Except(known, StringComparer.Ordinal);

        Assert.True(
            !unexpected.Any(),
            "Conditions on modifiers that the compiler did not recognise, beyond the known five:\r\n" +
            string.Join(
                "\r\n",
                database.UnrecognisedEffectConditions
                    .Where(p => !known.Contains(p.Key, StringComparer.Ordinal))
                    .Select(p => $"  {p.Value,5}  {p.Key}")));
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
    public void RoomsTheGameAssignsAreKeptApartFromTheOnesItOffers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // The brick room one of the game's own empires sits in. Its condition in the selector names
        // a country type a player can never be — the same kind of guard the fallen empires have — so
        // it is not something the designer offers. But a design that names a room gets that room,
        // which is exactly how that empire comes to be in it.
        var brick = Single(database.Rooms, r => r.Key == "pre_ftl_ancient_room");

        Assert.False(brick.IsOffered);
        Assert.NotNull(brick.Image);

        Assert.True(Single(database.Rooms, r => r.Key == "default_room").IsOffered);

        // A room the game only ever draws for an event is still a room, and a design may name one.
        // These were left out while the selector was treated as the definition of what exists; the
        // Contingency's own transmission room was the plain case for why that was wrong.
        var contingency = Single(database.Rooms, r => r.Key == "ethic_spaceship_room");

        Assert.False(contingency.IsOffered);
        Assert.NotNull(contingency.Image);

        // A room the selector names but no installation has a picture for is left out entirely:
        // naming one would be the one way to ask for something that cannot be drawn.
        Assert.DoesNotContain(database.Rooms, r => r.Key == "synth_queen_room");
        Assert.All(database.Rooms, r => Assert.NotNull(r.Image));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheGamesOwnEmpiresTravelInTheFormatAPlayerCanEdit()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // A browser has no installation to convert them with, so the conversion has to have happened
        // already. Without it there is nothing to take a copy of.
        Assert.All(database.PrescriptedEmpires, e => Assert.False(string.IsNullOrEmpty(e.Design)));

        var une = Single(database.PrescriptedEmpires, e => e.Key == "humans1");

        Assert.Equal("empire_human_1", une.FlagSet);

        // And it parses back as a design, which is what taking a copy does with it.
        var reopened = Sem.Designs.EmpireDesignsFile.LoadText(une.Design!);
        var design = Assert.Single(reopened.Designs);

        Assert.Equal("auth_democratic", design.Authority);
        Assert.Equal("empire_human_1", design.PrescriptedFlag);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ATraitBorrowingAnothersArtworkSaysSoAndIsBelieved()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        extractor.Extract();

        var sources = extractor.Assets.Requests.ToDictionary(
            r => r.Destination,
            r => r.Source,
            StringComparer.OrdinalIgnoreCase);

        // Fifty-three traits have no artwork of their own and name a another trait's. Ignoring that
        // left them on the game's unknown-trait placeholder, so two drawbacks were wearing what
        // reads as an ordinary trait's badge.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_jinxed.dds",
            sources["icons/traits/trait_humanoid_jinxed.png"]);

        Assert.Equal(
            "gfx/interface/icons/traits/trait_psychological_infertility.dds",
            sources["icons/traits/trait_humanoid_psychological_infertility.png"]);

        // The Lithoid traits do the same, wearing their organic counterparts' artwork.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_adaptive.dds",
            sources["icons/traits/trait_adaptive_lithoid.png"]);

        // A trait that names nothing still follows the naming convention.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_adaptive.dds",
            sources["icons/traits/trait_adaptive.png"]);

        // The species trait writes its artwork as a plain path, and that is honoured. Its leader
        // twin used to be asserted beside it, for the opposite behaviour - Galactic Paragons stacks
        // layers into an icon block and nothing here draws layers, so it fell back to the unknown
        // badge. The leader traits are no longer carried at all, so there is nothing left to fall
        // back: that whole family was a fifth of the download and no picker ever offered one.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_unplugged_positive_1.dds",
            sources["icons/traits/trait_unplugged_cybernetic_positives_1.png"]);

        Assert.DoesNotContain("icons/traits/leader_trait_unplugged_cybernetic_positives_1.png", sources.Keys);
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

        // Several civics and origins change how many traits a species may take. Natural Design says
        // so in a plain modifier block.
        var naturalDesign = Single(database.Civics, c => c.Key == "civic_natural_design");
        Assert.Contains("BIOLOGICAL_species_trait_points_add", naturalDesign.Effects.Modifiers.Keys);
        Assert.Contains("BIOLOGICAL_species_trait_picks_add", naturalDesign.Effects.Modifiers.Keys);

        // The hive mind's version says so only inside its swaps, and has no plain modifier block at
        // all. Reading the always-on modifiers alone lost it, which cost a hive mind two points and
        // two picks in the designer while the modifier panel beside it showed the bonus.
        var innateDesign = Single(database.Civics, c => c.Key == "civic_hive_natural_design");
        Assert.Empty(innateDesign.Effects.Modifiers);
        Assert.NotEmpty(innateDesign.Effects.Conditional);

        Assert.All(
            innateDesign.Effects.Conditional,
            swap =>
            {
                Assert.Equal(2, swap.Modifiers["BIOLOGICAL_species_trait_points_add"]);
                Assert.Equal(2, swap.Modifiers["BIOLOGICAL_species_trait_picks_add"]);
            });
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

    /// <summary>
    /// A name list's characters come from every culture it defines, and include its regnal names.
    /// </summary>
    /// <remarks>
    /// Two gaps, both counted out of the game's files. The reader took the first culture only, on
    /// the stated grounds that every list a player can choose holds one — but HUMAN1 and HUMAN2 each
    /// hold eleven, weighted, so a human empire was offered about a tenth of the names the game has.
    /// And regnal_first_names and regnal_second_names, which sixty-one of the seventy-one lists
    /// declare, were never asked for at all.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ANameListOffersEveryCultureItHoldsAndItsRegnalNamesToo()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var human = Single(database.NameLists, l => l.Key == "HUMAN1").CharacterNames;

        // Eleven cultures between them, and no single one of them holds anything like this many.
        Assert.True(
            human.FirstNames.All.Count > 1000,
            $"HUMAN1 offers {human.FirstNames.All.Count} first names; one culture alone is about a tenth of that.");

        Assert.True(human.SecondNames.All.Count > 900);

        // The regnal pool is its own, and is not empty for a list that declares one.
        Assert.NotEmpty(human.RegnalFirstNames.All);
        Assert.NotEmpty(human.RegnalSecondNames.All);

        // A list with one culture is unaffected by reading them all.
        var plantoid = Single(database.NameLists, l => l.Key == "PLANT4").CharacterNames;
        Assert.NotEmpty(plantoid.FirstNames.All);
        Assert.NotEmpty(plantoid.RegnalFirstNames.All);

        // And the assembled suggestions draw on both pools rather than the ordinary one alone.
        Assert.True(plantoid.Assembled(500).Count > plantoid.FirstNames.All.Count);
    }

    /// <summary>
    /// The ascended forms a portrait can wear, which a design stores as <c>evolution_mask</c>.
    /// </summary>
    /// <remarks>
    /// Counted out of the game's own files rather than read back from the extractor. There is one
    /// top-level <c>portrait_evolution</c> in the whole directory, in 00_portraits_main.txt, and
    /// fifty-nine portraits that override it: twenty-one cybernetic and fourteen Biogenesis ones
    /// with two stages apiece, and thirteen psionic and eleven synthetic ones with one.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryPortraitKnowsHowManyAscendedFormsItHas()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // The directory default: two stages of cybernetisation and psionic ascension, each naming
        // the asset suffix that dresses it.
        var mammalian = Single(database.Portraits, p => p.Key == "mam1");
        Assert.Equal(["_stage_1", "_stage_2", "_ascended"], mammalian.EvolutionStages);

        // A portrait with its own block takes it instead of the default. These write their stages as
        // decal and mask paths rather than a suffix, so the names are empty and only the count says
        // anything.
        var cybernetic = Single(database.Portraits, p => p.Key == "cyb1");
        Assert.Equal(2, cybernetic.EvolutionStages.Count);
        Assert.All(cybernetic.EvolutionStages, name => Assert.Equal(string.Empty, name));

        Assert.Equal(2, Single(database.Portraits, p => p.Key == "pro1_f").EvolutionStages.Count);
        Assert.Single(Single(database.Portraits, p => p.Key == "psionic_01").EvolutionStages);
        Assert.Single(Single(database.Portraits, p => p.Key == "synth01").EvolutionStages);

        // A group is what a design stores, so it has to carry the stages of the face underneath it
        // rather than none at all.
        var group = Single(database.Portraits, p => p.Key == "mam4");
        Assert.True(group.IsGroup);
        Assert.Equal(3, group.EvolutionStages.Count);

        // Every portrait ends up with a stage count, because every one either names its own or
        // falls back on the default.
        Assert.DoesNotContain(database.Portraits, p => p.EvolutionStages.Count == 0);

        // And the overrides are exactly the fifty-nine counted in the files.
        Assert.Equal(59, database.Portraits.Count(p => !p.IsGroup && p.EvolutionStages.Count != 3));
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

        // A third rather than a tenth, and deliberately: the thirty-two thousand name-list entries
        // are kept whole because a player's own empire may name any of them and nothing in the
        // game's own content points at them. What is dropped is still three quarters of the file.
        Assert.True(pruned.Count < all.Count / 3, $"Pruning kept {pruned.Count} of {all.Count} entries.");

        // The names themselves, which were pruned and left custom empires reading as their own keys.
        Assert.Contains("PLANT4_CHR_Cytophane", pruned);

        // A key with a hyphen in it, which the reference pattern used to stop short of.
        Assert.Contains("FUN3_CHR_uvi-Livve", pruned);

        // And the name-system formats, which an empire's own name is built out of.
        Assert.Contains("AofB", pruned);

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

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AFlagIsFramedWithTheGamesOwnMeasurements()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var frames = database.FlagFrames.ToDictionary(f => f.Key, StringComparer.Ordinal);

        // The five the game's interface actually uses, and not the three dead ones beside them.
        Assert.Equal(5, frames.Count);
        Assert.DoesNotContain("GFX_empire_flag_medium", frames.Keys);

        // The room's banner, whose numbers were checked by hand against the file.
        var banner = frames["GFX_empire_flag_128"];

        Assert.Equal(131, banner.FrameSize);
        Assert.Equal(111, banner.BackgroundSize);
        Assert.Equal(83, banner.EmblemSize);
        Assert.Equal(10, banner.BackgroundOffset);
        Assert.Equal(24, banner.EmblemOffset);

        // The emblem is never the whole flag, and never the same fraction of it twice: the point of
        // reading these rather than picking one number is that they run from seven tenths to four
        // fifths as the flag gets smaller.
        Assert.All(frames.Values, frame =>
        {
            Assert.InRange(frame.EmblemSize / frame.BackgroundSize, 0.6, 0.85);
            Assert.True(frame.FrameSize > frame.BackgroundSize, $"{frame.Key} has no border.");
            Assert.False(string.IsNullOrEmpty(frame.FrameImage), $"{frame.Key} has no frame picture.");
            Assert.False(string.IsNullOrEmpty(frame.MaskImage), $"{frame.Key} has no mask.");
        });

        Assert.True(
            frames["GFX_empire_flag_32"].EmblemSize / frames["GFX_empire_flag_32"].BackgroundSize >
            frames["GFX_empire_flag_200"].EmblemSize / frames["GFX_empire_flag_200"].BackgroundSize,
            "A small flag should carry a proportionally larger emblem, so that it stays legible.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ANomadBeginsAboardOneOfThreeArkships()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // Nine arkships exist across three families and three tiers; only the first tier of each is
        // something an empire starts with, and the game marks exactly those three.
        Assert.Equal(3, database.Arkships.Count);
        Assert.All(database.Arkships, a => Assert.EndsWith("_tier_1", a.Key, StringComparison.Ordinal));

        // In declaration order, which is the order the game's own panel stacks its three tiles.
        Assert.Equal(
            ["civilian_arkship_name", "science_arkship_name", "military_arkship_name"],
            database.Arkships.Select(a => a.NameKey));

        // Each names a sprite that is one frame of the ship-size sheet. Asserted because it was
        // missed once already: the definitions were read for their key alone, and three cards in the
        // designer sat blank beside a panel where everything else has a picture.
        Assert.All(
            database.Arkships,
            a => Assert.False(
                string.IsNullOrEmpty(a.Icon),
                $"{a.Key} resolved no icon; the game declares one and it has evidently moved."));

        // And that is as far as the icon goes: all nine arkships name frame 29 of a 29-frame sheet,
        // so the three icons are the same picture and this assertion passed while the panel showed
        // one glyph three times. What tells them apart is the model, drawn as a shipset is, reached
        // through the entity the ship size names.
        Assert.All(
            database.Arkships,
            a =>
            {
                Assert.False(
                    string.IsNullOrEmpty(a.Entity),
                    $"{a.Key} names no entity, so there is no way to find its model.");

                Assert.False(
                    string.IsNullOrEmpty(a.DescriptionKey),
                    $"{a.Key} has no description key.");
            });
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void NoNameListOffersATemplateAsThoughItWereAName()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // A family name is often a frame written round a given one — "$1$ Aburia" — and some carry a
        // second form after a run of bars. Joined with a space rather than composed, a third of the
        // game's lists offered the player names with the machinery still in them.
        foreach (var list in database.NameLists)
        {
            foreach (var name in list.CharacterNames.Assembled(20))
            {
                Assert.True(
                    name.IndexOfAny(['$', '|']) < 0,
                    $"{list.Key} offers \"{name}\".");
            }
        }
    }

    /// <summary>
    /// A nomad's arkship is named from the ship names its own list holds.
    /// </summary>
    /// <remarks>
    /// Checked against a nomadic empire the game itself wrote, which carries
    /// <c>HUM1_SHIP_TimaphontheImplacable</c> in <c>planet_name</c>. That key is in HUM1's
    /// <c>ship_names</c> and not in its <c>planet_names</c>, which is the whole reason the field
    /// cannot draw from the same pool for both.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnArkshipIsNamedFromTheShipsAndNotFromTheWorlds()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var list = Database.Value.NameLists.Single(n => n.Key == "HUM1");

        Assert.Contains("Timaphon the Implacable", list.ShipNames);
        Assert.DoesNotContain("Timaphon the Implacable", list.PlanetNames);

        // And no list's ships are merely its worlds again, so one pool cannot stand in for the
        // other. Four of the sixty-seven do share a handful - HUMAN1 names both a world and a ship
        // Concord - which is why this asks what the ship pool holds alone rather than for two sets
        // that never meet.
        Assert.All(
            Database.Value.NameLists.Where(n => n.ShipNames.Count > 0 && n.PlanetNames.Count > 0),
            n => Assert.NotEmpty(n.ShipNames.Except(n.PlanetNames, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The name field is relabelled for a nomad, and the words for it survive the pruner.
    /// </summary>
    /// <remarks>
    /// The game swaps this label inside its executable rather than in its interface files -
    /// <c>ARKSHIP_NAME</c> appears in no <c>.gui</c>, no script and no event - so nothing in the
    /// data refers to it and the pruner drops it unless it is asked for by name. It reads through
    /// two further entries, which the reference-following pass has to bring with it.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheFieldANomadNamesIsCalledTheArkshipsName()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        var text = extractor.ExtractLocalisation(reachableFrom: Database.Value);

        Assert.Equal("$ARKSHIP_LABEL$ Name", text.GetValueOrDefault("ARKSHIP_NAME"));
        Assert.Equal("$arkship_cap$", text.GetValueOrDefault("ARKSHIP_LABEL"));
        Assert.Equal("Arkship", text.GetValueOrDefault("arkship_cap"));

        // The one it replaces is still there, for every empire that has a world.
        Assert.Equal("Homeworld Name", text.GetValueOrDefault("HOMEWORLD_NAME"));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AShipsetIsCalledWhatTheGameCallsIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        var database = Database.Value;
        var text = extractor.ExtractLocalisation(reachableFrom: database);

        var sets = database.GraphicalCultures.ToDictionary(c => c.Key, StringComparer.Ordinal);

        // The game names a shipset by its key shouted, under a "Graphical Cultures" heading, and
        // names only the pair Biogenesis added. Reading the key as written gave "biogenesis_01".
        Assert.Equal("Spinovore", text.GetValueOrDefault(sets["biogenesis_01"].NameKey));
        Assert.Equal("Shellcraft", text.GetValueOrDefault(sets["biogenesis_02"].NameKey));

        // Everything else has no entry under any spelling, which is a gap in the game's own text
        // rather than one here — the caller falls back to the readable key.
        Assert.False(text.ContainsKey(sets["mammalian_01"].NameKey));

        // Descriptions, unlike names, are there for all of them.
        Assert.All(
            database.GraphicalCultures.Where(c => sets.ContainsKey(c.Key) && c.ShipPreview is not null),
            culture => Assert.True(
                text.ContainsKey(culture.DescriptionKey),
                $"{culture.Key} has no description under {culture.DescriptionKey}."));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void CityBandsCarryThePopulationTheyBelongTo()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var humanoid = Single(database.GraphicalCultures, c => c.Key == "humanoid_01");

        Assert.Equal(6, humanoid.CityLayers.Count);

        // The last band is an ecumenopolis, which the game draws only on a world at five. Nothing
        // in the designer reaches that, and drawing it anyway is what hid the planet.
        var ecumenopolis = humanoid.CityLayers[humanoid.CityLayers.Count - 1];

        Assert.Equal(5, ecumenopolis.MinPop);
        Assert.Null(ecumenopolis.MaxPop);
        Assert.False(ecumenopolis.AppearsAt(database.Defines.CityPopLevel));

        // Every other band does belong on the world the designer shows.
        Assert.All(
            humanoid.CityLayers.Take(humanoid.CityLayers.Count - 1),
            band => Assert.True(band.AppearsAt(database.Defines.CityPopLevel), $"Band {band.Band} is not drawn."));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheDesignerDrawsTheWorldTheGameSaysItDoes()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // DEFAULT_CITY_POP_LEVEL, whose line in the defines is commented "Shown in empire designer".
        Assert.Equal(4, Database.Value.Defines.CityPopLevel);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASceneryBandKnowsWhichBandItIs()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // An arctic world has a first, third and fourth band and no second. Held as a plain list its
        // third took the second's place and every row of hills after the gap was drawn a row forward.
        var arctic = Single(database.PlanetClasses, p => p.Key == "pc_arctic");

        Assert.Equal([1, 3, 4], arctic.Scenery.Select(s => s.Band));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASetThatModelsNoShipsOfItsOwnIsNotAShipset()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        Assert.Equal(2, database.ShipSets.Count);

        var biological = Single(database.ShipSets, s => s.NameKey == "SHIPSET_BIOLOGICAL");
        var mechanical = Single(database.ShipSets, s => s.NameKey == "SHIPSET_MECHANICAL");

        Assert.True(biological.Includes("bio_ship"));
        Assert.False(mechanical.Includes("bio_ship"));
        Assert.True(mechanical.Includes("default_ship"));

        Assert.Equal("bio_ship", Single(database.GraphicalCultures, c => c.Key == "biogenesis_01").ShipCategory);
        Assert.Equal("default_ship", Single(database.GraphicalCultures, c => c.Key == "humanoid_01").ShipCategory);

        // These two dress cities and fly whatever their fallback builds, which is why Wilderness
        // showed a Biogenesis corvette — it was one.
        Assert.Null(Single(database.GraphicalCultures, c => c.Key == "wilderness_01").ShipCategory);
        Assert.Null(Single(database.GraphicalCultures, c => c.Key == "solarpunk_01").ShipCategory);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryRulerTraitWearsItsOwnIcon()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // Its own extractor, because the asset requests are needed and the shared one has been
        // read from more than once by the time this runs.
        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        var rulers = extractor.Extract().Traits.Where(t => t.Kind == TraitKind.StartingRuler).ToList();

        // A ruler trait does not name a picture, it describes one: an inline script stacking a
        // coloured background, the trait's own glyph over it, and whatever markers its rarity and
        // council seat call for. Missing that, all thirty-four fell through to the unknown-trait
        // icon and were one picture; taking only the glyph out of the description made them
        // thirty-four near-black marks on nothing.
        Assert.All(rulers, t => Assert.False(string.IsNullOrEmpty(t.Icon), $"{t.Key} has no icon."));

        var stacks = extractor.Assets.Composites
            .ToDictionary(c => c.Destination, c => c.Layers, StringComparer.Ordinal);

        var drawn = rulers
            .Select(t => stacks.GetValueOrDefault(t.Icon!))
            .OfType<IReadOnlyList<AssetLayer>>()
            .ToList();

        Assert.Equal(rulers.Count, drawn.Count);

        // Every one is a stack rather than a lone picture, and its background is painted — that
        // colour is the whole reason these read as icons instead of as smudges.
        Assert.All(drawn, layers =>
        {
            Assert.True(layers.Count > 1, "A composed icon of one layer is just the glyph again.");
            Assert.Contains(layers, l => l.Tint is not null);
        });

        Assert.DoesNotContain(
            drawn.SelectMany(layers => layers).Select(l => l.Source),
            source => source.EndsWith("trait_unknown.dds", StringComparison.OrdinalIgnoreCase));

        // Asserted on what each stack is made of rather than on the path it will be written to,
        // which is the trait's own key and so distinct however the icon was found. Comparing those
        // said nothing, and hid seven traits that were still falling back. On the whole stack rather
        // than on one layer of it, because where the glyph sits depends on the recipe: a rarity puts
        // a glow and a frame in ahead of it.
        var stacked = drawn
            .Select(layers => string.Join(
                "|",
                layers.Select(l => $"{l.Source}#{l.Frame?.Frame}#{l.Tint}")))
            .ToList();

        Assert.Equal(rulers.Count, stacked.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AListThatNumbersItsFleetsSaysSo()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // Toxoid 3 names no fleets. It gives a template and counts, which is not the same as
        // holding nothing.
        var toxoid = Single(database.NameLists, n => n.Key == "TOX3");

        Assert.Empty(toxoid.FleetNames);
        Assert.Contains("$R$", toxoid.FleetPattern);

        // And a list that does name them still does.
        Assert.NotEmpty(Single(database.NameLists, n => n.Key == "TOX1").FleetNames);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OnlyThePacksThatDecideSomethingAreMarkedAsDeciding()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // Plantoids gates a species class, its portraits, its traits and its shipset.
        Assert.True(Single(database.Dlc, d => d.Name == "Plantoids Species Pack").Decides);

        // Utopia is a real expansion, but nothing an empire is built from asks about it.
        Assert.False(Single(database.Dlc, d => d.Name == "Utopia").Decides);
        Assert.False(Single(database.Dlc, d => d.Name == "Original Game Soundrack").Decides);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheTwoSpeciesPacksWithSuffixedSpritesGetTheirIcons()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // These declare GFX_plantoidsspeciespack_small and _big but no bare name, so looking only
        // for the bare one left the pack that gates the most options without a badge.
        foreach (var name in (string[])["Plantoids Species Pack", "Humanoids Species Pack"])
        {
            Assert.False(string.IsNullOrEmpty(Single(database.Dlc, d => d.Name == name).Icon), name);
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OnlyTheLeadersThatMayRuleAreOfferedAsRulers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        Assert.Equal(4, database.LeaderClasses.Count);

        // An envoy is a leader but never an empire's ruler, and the game says so outright.
        Assert.False(Single(database.LeaderClasses, c => c.Key == "envoy").CanRule);
        Assert.Equal(3, database.LeaderClasses.Count(c => c.CanRule));
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
    /// <summary>
    /// What a plan is made of, read off the real game.
    /// </summary>
    /// <remarks>
    /// A plan asks the game's own files questions a design never had to: how many perks come before
    /// this one, whether a government reform could take that civic on, whether an empire will have
    /// researched something by the time it gets there. Each number below was counted in the
    /// installation rather than read off the extractor.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void APlanCanBeMadeOfWhatTheGameActuallyHolds()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        Assert.Equal(49, database.AscensionPerks.Count);
        Assert.Equal(32, database.TraditionTrees.Count);
        Assert.Equal(234, database.Traditions.Count);

        // Two civics to start with and a third from tech_galactic_administration, which is the slot
        // players reform their government to fill and the reason a plan has civics at all.
        Assert.Equal(2, database.Defines.CivicPoints);
        Assert.Equal(3, database.Defines.PlannedCivicPoints);

        // The game's own modification field. Ninety-six say no outright, and a few dozen more refuse
        // only removal - civic_anglers and its kin, whose remove block is "always = no".
        Assert.Equal(96, database.Civics.Count(c => Never(c.CanAddLater)));
        Assert.Equal(124, database.Civics.Count(c => Never(c.CanRemoveLater)));

        // "num_ascension_perks > 1", which is what makes a perk unable to be first or second. By
        // what it compares rather than as a whole record: it also carries the game's own sentence
        // for failing it, which is wording rather than rule and which the picker shows.
        var counts = database.AscensionPerks
            .SelectMany(p => p.Possible.AndNested())
            .OfType<CountRequirement>()
            .ToList();

        Assert.Contains(
            counts,
            c => c.Of == SelectionCategory.AscensionPerk
                && c.Comparison == CountComparison.Above
                && c.Value == 1);

        // And its sibling is not counted at all. How many trees were open when a perk was taken is
        // a fact about the order a game happened in, and a plan settles its traditions in one go
        // rather than one between each perk - so reading it refused every ascension perk to anyone
        // who had finished planning their traditions.
        Assert.DoesNotContain(counts, c => c.Of == SelectionCategory.TraditionTree);

        Assert.Contains(
            database.AscensionPerks.SelectMany(p => p.Possible.AndNested()).OfType<UnknownRequirement>(),
            u => u.Name == "num_tradition_categories");

        // A technology is something this empire will have by the time it takes the perk, so it is
        // read as an assumption rather than as a refusal. Answering it false is what made World
        // Shaper, the Colossus and the Archaeo-Engineers impossible to plan at all.
        Assert.Contains(
            database.AscensionPerks.SelectMany(p => p.Possible.AndNested()).OfType<UnknownRequirement>(),
            u => u.Name == "has_technology");

        // A country flag is the same kind of thing and gets the same answer - Galactic Wonders asks
        // whether a megastructure has been built, which no design could ever say yes to.
        Assert.Contains(
            database.AscensionPerks.SelectMany(p => p.Possible.AndNested()).OfType<UnknownRequirement>(),
            u => u.Name == "has_country_flag");

        // What an empire definitionally is not stays flatly false, which is the line: a design may
        // come to have a technology, and never comes to be a fallen empire or a pirate.
        var everything = database.Requirements().SelectMany(r => r.AndNested()).OfType<UnknownRequirement>().ToList();

        Assert.DoesNotContain(everything, u => u.Name == "is_fallen_empire");
        Assert.DoesNotContain(everything, u => u.Name == "is_pirate");
        Assert.DoesNotContain(everything, u => u.Name == "is_pre_ftl_empire");
    }

    /// <summary>
    /// Planning one ascension still takes the others off the list, with the flags unread.
    /// </summary>
    /// <remarks>
    /// The half that a blanket "assume anything unreadable" would quietly destroy, checked against
    /// the game's own script rather than a fixture. The Purity and Mutation trees are gated on a NOR
    /// listing every other ascension and a country flag among them, so the exclusion has to survive
    /// one of its terms being unanswerable - and has to keep firing on the terms that are not.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnUnreadableFlagDoesNotCostTheAscensionExclusions()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var rules = new EmpireRules(database);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        bool Offered(string tree, params string[] perks) =>
            rules.GetTraditionTreeOptions(context, [], perks).Single(o => o.Key == tree).Visible;

        // Nothing planned: the flag in the NOR is unreadable, and the trees are still on offer.
        Assert.True(Offered("tradition_purity"));
        Assert.True(Offered("tradition_mutation"));

        // A cybernetic ascension planned: the NOR now has a term that definitely holds, and both
        // go, exactly as taking that perk in the game would take them.
        Assert.False(Offered("tradition_purity", "ap_the_flesh_is_weak"));
        Assert.False(Offered("tradition_mutation", "ap_the_flesh_is_weak"));
    }

    /// <summary>Whether a condition compiled to a flat no.</summary>
    private static bool Never(Requirement requirement) =>
        requirement is AlwaysRequirement { Value: false };
    /// <summary>
    /// Nothing a plan can name is left with nothing to say.
    /// </summary>
    /// <remarks>
    /// Fifteen perks and seventy-eight traditions used to show a name and an empty list, for three
    /// separate reasons: the plain triggered_modifier was read as a block the game hides, which is
    /// a rule about traits; the tradition_swap was not read at all; and the scripted unlocks were
    /// dropped rather than described. Interstellar Dominion was the plainest case - no always-on
    /// modifier anywhere in it, its whole effect in three triggered blocks.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EverythingAPlanCanNameSaysSomething()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        static bool Silent(EffectSet e) =>
            e.Modifiers.Count == 0 && e.Conditional.Count == 0 && e.TagKeys.Count == 0
            && e.TooltipKey is not { Length: > 0 } && e.DescriptionKey is not { Length: > 0 };

        var perks = database.AscensionPerks.Where(p => Silent(p.Effects)).Select(p => p.Key).ToList();

        Assert.True(perks.Count <= 1, "Perks with nothing to say: " + string.Join(", ", perks));

        // Interstellar Dominion: three mutually exclusive triggered blocks and no always-on one.
        var dominion = database.AscensionPerks.Single(p => p.Key == "ap_interstellar_dominion");

        Assert.Empty(dominion.Effects.Modifiers);
        Assert.Equal(3, dominion.Effects.Conditional.Count);

        // Nihilistic Acquisition, whose whole effect is a line of script the game names.
        var raiding = database.AscensionPerks.Single(p => p.Key == "ap_nihilistic_acquisition");

        Assert.Contains("allow_raiding", raiding.Effects.TagKeys);
    }

    /// <summary>
    /// A tradition replaced by a swap gives what the swap gives, and only then.
    /// </summary>
    /// <remarks>
    /// Prosperity is the case that showed the old reading was wrong: station output normally, and to
    /// a nomadic empire three entirely different modifiers instead. Read as always-on, a nomad was
    /// shown the one it does not get and none of the three it does.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASwappedTraditionGivesWhatTheSwapGives()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var adopt = Database.Value.Traditions.Single(t => t.Key == "tr_prosperity_adopt").Effects;

        // Nothing unconditional: the base is now one of the alternatives rather than a floor.
        Assert.Empty(adopt.Modifiers);

        var baseline = adopt.Conditional
            .Single(c => c.Modifiers.ContainsKey("station_gatherers_produces_mult"));

        // And it applies only where no swap has claimed the tradition.
        Assert.IsType<NotRequirement>(baseline.When);

        // The nomadic alternatives bring their own, and never the base's.
        var nomadic = adopt.Conditional.Where(c => !ReferenceEquals(c, baseline)).ToList();

        Assert.NotEmpty(nomadic);
        Assert.All(
            nomadic,
            c => Assert.DoesNotContain("station_gatherers_produces_mult", c.Modifiers.Keys));
    }
    /// <summary>
    /// An ascension tree cannot be opened until the plan names the perk that opens it.
    /// </summary>
    /// <remarks>
    /// Where the game says so is the surprise: nothing on the tree asks for the perk, and the
    /// tradition that adopts it does - "the flesh is weak, and the technology, unless your origin
    /// already put you there". Read only the tree and every ascension is offered from the start,
    /// which is not what the game does.
    ///
    /// A tree is opened for it to be second at soonest. An ascension perk costs a slot and a slot
    /// comes from finishing a tradition tree, so a plan that has opened nothing has taken no perk
    /// and no ascension tree is ever the first one.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnAscensionTreeWaitsForItsPerk()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var rules = new EmpireRules(database);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        bool Open(string[] trees, params string[] perks) =>
            rules.GetTraditionTreeOptions(context, trees, perks)
                .Single(o => o.Key == "tradition_cybernetics").Enabled;

        string[] first = ["tradition_prosperity"];

        Assert.False(Open(first));
        Assert.True(Open(first, "ap_the_flesh_is_weak"));

        // And not before the plan could have taken the perk at all.
        Assert.False(Open([], "ap_the_flesh_is_weak"));

        // The ordinary trees ask nothing of a plan and are open from the first moment.
        Assert.True(rules.GetTraditionTreeOptions(context, [], [])
            .Single(o => o.Key == "tradition_prosperity").Enabled);
    }

    /// <summary>
    /// The six trees whose gate is a country flag wait for the perk that flag stands for.
    /// </summary>
    /// <remarks>
    /// Purity never mentions Biomorphosis. It asks for a country flag, which the perk reaches three
    /// events later by starting a situation whose completion awards the tree - and a flag is the one
    /// thing a design can never answer, so all six were open to an empire that had taken nothing at
    /// all. The machine three are asked of a machine intelligence, because being visible to one is
    /// their own potential doing its job rather than this rule.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnAscensionTreeGatedOnAFlagWaitsForThePerkBehindIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;

        Skip.IfNot(
            database.Dlc.Any(d => d.Name == "BioGenesis" && d.Installed) &&
            database.Dlc.Any(d => d.Name == "The Machine Age" && d.Installed),
            "The trees this is about ship with BioGenesis and The Machine Age.");

        var rules = new EmpireRules(database);
        var plain = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        var thinking = EmpireDesignsFile.CreateEmpty().Add("Machines");
        thinking.Authority = "auth_machine_intelligence";
        var machine = rules.CreateContext(thinking);

        var gated = new[]
        {
            ("tradition_purity", "ap_engineered_evolution", "REQUIRES_FINISHED_EVOLUTION", plain),
            ("tradition_cloning", "ap_engineered_evolution", "REQUIRES_FINISHED_EVOLUTION", plain),
            ("tradition_mutation", "ap_engineered_evolution", "REQUIRES_FINISHED_EVOLUTION", plain),
            ("tradition_nanotech", "ap_synthetic_age", "REQUIRES_FINISHED_TRANSFORMATION", machine),
            ("tradition_modularity", "ap_synthetic_age", "REQUIRES_FINISHED_TRANSFORMATION", machine),
            ("tradition_virtuality", "ap_synthetic_age", "REQUIRES_FINISHED_TRANSFORMATION", machine),
        };

        // One tree already opened, so the plan has earned the slot the perk is spent from. No
        // ascension tree is ever the first one, for that reason.
        string[] first = ["tradition_prosperity"];

        foreach (var (tree, perk, said, context) in gated)
        {
            OptionState Offered(params string[] perks) =>
                rules.GetTraditionTreeOptions(context, first, perks).Single(o => o.Key == tree);

            var closed = Offered();

            Assert.True(closed.Visible, $"{tree} is not offered at all.");
            Assert.False(closed.Enabled, $"{tree} opens before {perk} is planned.");

            // The game wrote the sentence for this, and it names the situation the perk starts -
            // "has finished the Biomorphosis situation" - so the reason is its key on its own.
            Assert.Contains(said, closed.Reasons);

            var opened = Offered(perk);

            // Visible as well as enabled. The trees rule each other out through these same flags,
            // so a substitution that reached the exclusions would hide this one here instead.
            Assert.True(opened.Visible, $"{tree} disappears once {perk} is planned.");
            Assert.True(opened.Enabled, $"{tree} stays shut with {perk} planned.");
        }
    }

    /// <summary>
    /// A flag standing for a perk is read that way only where a tradition asks to be adopted.
    /// </summary>
    /// <remarks>
    /// The tempting fix was to read those flags as their perk everywhere they appear, and it is
    /// wrong: each of these trees rules out its siblings through the very same flags, reached as
    /// <c>has_cloning_ascension</c> and its like, where the flag means the branch that was taken
    /// rather than the perk that led to it. Substituted there, Purity's own exclusion became "must
    /// not have Biomorphosis" and the tree vanished the moment the perk was planned - the same hole
    /// as before, dug the other way. So outside the gate the flag has to stay unanswerable.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AFlagStandsForItsPerkOnlyInTheGateThatAsksForIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;

        // Every gate names its perk, which is the whole of the substitution.
        var gates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tr_purity_adopt"] = "ap_engineered_evolution",
            ["tr_cloning_adopt"] = "ap_engineered_evolution",
            ["tr_mutation_adopt"] = "ap_engineered_evolution",
            ["tr_nanotech_adopt"] = "ap_synthetic_age",
            ["tr_modularity_adopt"] = "ap_synthetic_age",
            ["tr_virtuality_adopt"] = "ap_synthetic_age",
        };

        foreach (var (tradition, perk) in gates)
        {
            var gate = database.Traditions.Single(t => t.Key == tradition).Possible;

            Assert.Contains(
                gate.AndNested(),
                r => r is SelectionRequirement
                {
                    Category: SelectionCategory.AscensionPerk,
                } selection && selection.Key == perk);

            Assert.DoesNotContain(gate.AndNested(), r => r is UnknownRequirement);
        }

        // And the exclusions are untouched, which is the other half of the rule: Purity still
        // rules out its siblings by a flag it cannot answer, and still names no perk of its own.
        var purity = database.TraditionTrees.Single(t => t.Key == "tradition_purity").Potential;

        Assert.Contains(
            purity.AndNested(),
            r => r is UnknownRequirement { Name: "has_country_flag" });

        Assert.DoesNotContain(
            purity.AndNested(),
            r => r is SelectionRequirement
            {
                Category: SelectionCategory.AscensionPerk,
                Key: "ap_engineered_evolution",
            });
    }

    /// <summary>
    /// A tree gated on a perk cannot be opened before the plan could have taken that perk.
    /// </summary>
    /// <remarks>
    /// Reported from the running app: a plan whose perks read One Vision, Hydrocentric,
    /// Biomorphosis offered Purity for the very first tradition slot. Purity asks for Biomorphosis,
    /// Biomorphosis is the third perk, and an ascension perk slot is what finishing a tradition
    /// tree grants - so the earliest the plan could hold three perks is after three trees, and
    /// Purity is the fourth tree at soonest.
    ///
    /// Both halves are checked, because they fail differently: what the picker offers decides
    /// whether it can be put there in the first place, and the order check decides whether it can
    /// be dragged there afterwards.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ATreeCannotBeOpenedBeforeThePerkThatUnlocksIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;
        var rules = new EmpireRules(database);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        string[] perks = ["ap_one_vision", "ap_hydrocentric", "ap_engineered_evolution"];

        bool Offered(params string[] trees) =>
            rules.GetTraditionTreeOptions(context, trees, perks)
                .Single(o => o.Key == "tradition_purity").Enabled;

        // Nothing planned yet: no tree finished, so no perk taken, so no Biomorphosis.
        Assert.False(Offered(), "Purity is offered for the first tradition slot.");

        Assert.False(Offered("tradition_prosperity"), "Purity is offered for the second slot.");

        Assert.False(
            Offered("tradition_prosperity", "tradition_discovery"),
            "Purity is offered for the third slot.");

        // Three trees in, three perk slots earned, and the third of them is Biomorphosis.
        Assert.True(
            Offered("tradition_prosperity", "tradition_discovery", "tradition_expansion"),
            "Purity is refused for the fourth slot, where the plan has earned Biomorphosis.");

        // And it cannot be dragged back in front of them afterwards.
        string[] legal = ["tradition_prosperity", "tradition_discovery", "tradition_expansion", "tradition_purity"];
        string[] tooSoon = ["tradition_purity", "tradition_prosperity", "tradition_discovery", "tradition_expansion"];

        Assert.True(rules.IsLegalTreeOrder(context, legal, perks));
        Assert.False(rules.IsLegalTreeOrder(context, tooSoon, perks));
    }

    /// <summary>
    /// A perk that hands over a tradition tree needs the plan to have room for it.
    /// </summary>
    /// <remarks>
    /// Biomorphosis does not unlock Purity for selection - it gives it. Its situation ends by
    /// running <c>add_tradition</c> for whichever of Purity, Cloning and Mutation the player picks
    /// there, and the whole branch is guarded on <c>num_tradition_categories</c> being under seven.
    /// With every slot full the game skips the grant and the perk keeps only its other half.
    ///
    /// A plan is a whole statement of intent, so one taking the perk with nowhere to put the tree
    /// is throwing it away - which is stricter than the game and is the point of planning. A
    /// granted tree the plan already names needs no room of its own: it is the tree.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void APerkThatGrantsATreeNeedsRoomForIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;
        var rules = new EmpireRules(database);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        // The three it hands over, and not Genetics - which asks for the same perk and is chosen
        // like any other tree once it is held.
        Assert.DoesNotContain("tradition_genetics", rules.TreesGrantedBy("ap_engineered_evolution"));

        Assert.Equal(
            ["tradition_cloning", "tradition_mutation", "tradition_purity"],
            rules.TreesGrantedBy("ap_engineered_evolution").Order(StringComparer.Ordinal));

        Assert.Equal(
            ["tradition_modularity", "tradition_nanotech", "tradition_virtuality"],
            rules.TreesGrantedBy("ap_synthetic_age").Order(StringComparer.Ordinal));

        // A perk that hands over nothing asks for nothing.
        Assert.Empty(rules.TreesGrantedBy("ap_one_vision"));

        var slots = database.Defines.TraditionSlots;

        string[] ordinary =
        [
            "tradition_prosperity", "tradition_discovery", "tradition_expansion",
            "tradition_harmony", "tradition_supremacy", "tradition_diplomacy",
            "tradition_statecraft",
        ];

        var full = ordinary.Take(slots).ToList();

        Assert.False(
            rules.HasRoomForGrantedTree(rules.TreesGrantedBy("ap_engineered_evolution"), full),
            "Biomorphosis is allowed with every tradition slot spent on something else.");

        // One of the three already planned is the tree, and it has its slot.
        var withPurity = full.Take(slots - 1).Append("tradition_purity").ToList();

        Assert.True(
            rules.HasRoomForGrantedTree(rules.TreesGrantedBy("ap_engineered_evolution"), withPurity),
            "Biomorphosis is refused although the plan already names the tree it grants.");

        // And the perk itself says so where there is no room.
        var blocked = rules.GetAscensionPerkOptions(context, [], full)
            .Single(o => o.Key == "ap_engineered_evolution");

        Assert.False(blocked.Enabled);
        Assert.NotEmpty(blocked.Reasons);
    }

    /// <summary>
    /// The last tradition slot belongs to the tree a planned perk still owes the plan.
    /// </summary>
    /// <remarks>
    /// With one slot left, a perk waiting to hand over a tree, and none of its trees planned, that
    /// slot is the only place the grant can land. Spending it on anything else loses the tree.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheLastSlotIsKeptForATreeAPerkStillOwes()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;
        var rules = new EmpireRules(database);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        string[] ordinary =
        [
            "tradition_prosperity", "tradition_discovery", "tradition_expansion",
            "tradition_harmony", "tradition_supremacy", "tradition_diplomacy",
        ];

        // One short of full, with Biomorphosis planned and none of its trees taken.
        var nearlyFull = ordinary.Take(database.Defines.TraditionSlots - 1).ToArray();
        string[] perks = ["ap_one_vision", "ap_hydrocentric", "ap_engineered_evolution"];

        var offered = rules.GetTraditionTreeOptions(context, nearlyFull, perks);

        Assert.True(
            offered.Single(o => o.Key == "tradition_purity").Enabled,
            "The tree the perk owes is refused for the last slot.");

        Assert.False(
            offered.Single(o => o.Key == "tradition_statecraft").Enabled,
            "The last slot is offered to a tree that would lose the plan its grant.");
    }

    /// <summary>
    /// The personalities read, and the ones a design could actually be given.
    /// </summary>
    /// <remarks>
    /// Twenty of the fifty-one belong to fallen empires, pre-FTL societies and the like. They cost
    /// nothing to rule out - they ask <c>is_country_type</c> for something a design never is - but
    /// it is worth pinning that they are ruled out, because the two heaviest personalities in the
    /// game are among the ones that could go wrong.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ThePersonalitiesAreReadWithTheirWeights()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;

        Assert.Equal(51, database.Personalities.Count);

        // Every one says what it takes and what it weighs.
        Assert.All(database.Personalities, p => Assert.True(p.Weight > 0, $"{p.Key} weighs nothing."));

        var honorbound = database.Personalities.Single(p => p.Key == "honorbound_warriors");

        Assert.Equal(50, honorbound.Weight);
        Assert.Empty(honorbound.Additions);

        // The additions are read, and there are seven of them here.
        Assert.Equal(7, database.Personalities.Single(p => p.Key == "erudite_explorers").Additions.Count);
        Assert.Equal(10, database.Personalities.Single(p => p.Key == "erudite_explorers").Weight);
    }

    /// <summary>
    /// Every personality a design could be given has words shipped for it.
    /// </summary>
    /// <remarks>
    /// Named under a prefix rather than under their own key, which is the one thing about these
    /// that is not the usual convention - and a name nothing seeded is a name the pruner cuts.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryPersonalityADesignCanGetIsNamed()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;

        var text = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!))
            .ExtractLocalisation(reachableFrom: database);

        // All but one, and the one is a fallen empire's: the galactic defence force, which the
        // game itself never names because nothing ever shows it to a player.
        var nameless = database.Personalities
            .Where(p => !text.ContainsKey(p.NameKey))
            .Select(p => p.Key)
            .ToList();

        Assert.Equal(["galactic_defense_force"], nameless);

        // And a real empire is offered only named ones. A blank design is offered none at all,
        // which is right - nearly every personality asks after an ethic, and a design with no
        // ethics answers none of them.
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("Test");

        design.Authority = "auth_democratic";
        design.SetEthics(["ethic_fanatic_egalitarian", "ethic_xenophile"]);

        var offered = new EmpireRules(database).DerivePersonalities(
            new EmpireRules(database).CreateContext(design));

        Assert.NotEmpty(offered);
        Assert.All(offered, o => Assert.True(
            text.ContainsKey(o.Personality.NameKey),
            $"{o.Personality.Key} has no name in the shipped text."));
    }

    /// <summary>
    /// A design is offered the personalities it allows, with shares that add to one.
    /// </summary>
    /// <remarks>
    /// And never a fallen empire's, nor the two that ask for an ascension perk. Those two -
    /// Became the Crisis and the hyperthermia empire - weigh ten thousand each, so if a plan's
    /// perks ever reached this the empire would show one personality at ninety-nine per cent and
    /// the truth nowhere.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ADesignIsOfferedOnlyThePersonalitiesItCouldBeGiven()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;
        var rules = new EmpireRules(database);

        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("Test");

        design.Authority = "auth_democratic";
        design.SetEthics(["ethic_fanatic_militarist", "ethic_spiritualist"]);

        var offered = rules.DerivePersonalities(rules.CreateContext(design));

        Assert.NotEmpty(offered);
        Assert.Equal(1.0, offered.Sum(o => o.Share), 6);

        // Ordered likeliest first.
        Assert.Equal(offered.Select(o => o.Share).OrderByDescending(s => s), offered.Select(o => o.Share));

        // Fanatic militarist and spiritualist is exactly what this one asks for.
        Assert.Contains(offered, o => o.Personality.Key == "honorbound_warriors");

        var keys = offered.Select(o => o.Personality.Key).ToList();

        Assert.DoesNotContain("became_the_crisis", keys);
        Assert.DoesNotContain("hyperthermia_empire", keys);

        // Nothing belonging to an empire the player is not.
        Assert.DoesNotContain("fallen_empire_materialist", keys);
    }

    /// <summary>
    /// A world is drawn as the picture it names, which is not always its own.
    /// </summary>
    /// <remarks>
    /// Twenty-one classes borrow another's - a machine world is painted as pc_ai, a hive world as
    /// pc_infested, and six ringworlds share three pictures between them. The art is filed under
    /// the name given rather than under the class, so reading the class's own key found nothing:
    /// a machine world had no sky and no landscape at all, and the empire's city was left standing
    /// on nothing.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AWorldIsPaintedAsThePictureItNames()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Database.Value.PlanetClasses.ToDictionary(p => p.Key, StringComparer.Ordinal);

        Assert.Contains("pc_ai_sky", worlds["pc_machine"].Sky);
        Assert.Contains("pc_infested_sky", worlds["pc_hive"].Sky);

        // And the borrowed landscape comes with it. A shattered ring had none of its own.
        Assert.Contains("pc_ringworld_sky", worlds["pc_shattered_ring_habitable"].Sky);
        Assert.NotEmpty(worlds["pc_shattered_ring_habitable"].Scenery);

        // A world that names no picture is still drawn as itself.
        Assert.Contains("pc_continental_sky", worlds["pc_continental"].Sky);
    }

    /// <summary>
    /// A world that is already built has no empire's city painted over it.
    /// </summary>
    /// <remarks>
    /// The game says so on twenty of them, and they are the ones that are a built thing already: a
    /// machine world, a hive world, a habitat. One goes the other way - an ecumenopolis is built to
    /// the horizon whatever its population, and fixes its level rather than reading one.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AWorldThatIsAlreadyBuiltCarriesNoCity()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Database.Value.PlanetClasses.ToDictionary(p => p.Key, StringComparer.Ordinal);

        Assert.False(worlds["pc_machine"].ShowsCity);
        Assert.False(worlds["pc_hive"].ShowsCity);
        Assert.False(worlds["pc_habitat"].ShowsCity);

        // The ordinary worlds are unchanged.
        Assert.True(worlds["pc_continental"].ShowsCity);
        Assert.True(worlds["pc_shattered_ring_habitable"].ShowsCity);

        Assert.Equal(6, worlds["pc_city"].FixedCityLevel);
    }

    /// <summary>
    /// A perk or a tree that will not be taken always says why.
    /// </summary>
    /// <remarks>
    /// Almost every condition in the game is wrapped in a sentence the game wrote for it, and that
    /// wording is what a blocked row shows. Two perks have none, and eight of the fourteen
    /// traditions that open a tree have none either - among them every ascension, whose gate is the
    /// perk that unlocks it. Those rows simply would not be taken and would not say what was
    /// wanted, which reads as a broken control rather than a rule.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ABlockedPerkOrTreeAlwaysSaysWhy()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var rules = new EmpireRules(Database.Value);
        var context = rules.CreateContext(EmpireDesignsFile.CreateEmpty().Add("Test"));

        static IEnumerable<string> Silent(IEnumerable<OptionState> options) =>
            options.Where(o => o.Visible && !o.Enabled && o.Reasons.Count == 0).Select(o => o.Key);

        var perks = Silent(rules.GetAscensionPerkOptions(context, [], [])).ToList();
        var trees = Silent(rules.GetTraditionTreeOptions(context, [], [])).ToList();

        Assert.True(perks.Count == 0, "Perks blocked without a reason: " + string.Join(", ", perks));
        Assert.True(trees.Count == 0, "Trees blocked without a reason: " + string.Join(", ", trees));

        // And the reason for an ascension tree names the perk that opens it, since the game says
        // nothing there itself.
        var cybernetics = rules.GetTraditionTreeOptions(context, [], [])
            .Single(o => o.Key == "tradition_cybernetics");

        Assert.False(cybernetics.Enabled);
        Assert.Contains(
            cybernetics.Reasons,
            r => RuleReasons.Split(r).Subject == "ap_the_flesh_is_weak");
    }

    /// <summary>
    /// Everything a picker can draw has words for itself, in the language the app is shipping.
    /// </summary>
    /// <remarks>
    /// The failure this catches is silent and looks like a typo: a key with no text is drawn as the
    /// key with its underscores taken out, so a reader is told a tradition is called "Tr
    /// Adaptability Adopt Delayed". Seventy-three traditions read that way, because the description
    /// key was assumed to follow the game's <c>_delayed</c> convention and only a hundred and
    /// sixty-one of them do.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EverythingDrawnHasTextShippedForIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var text = ShippedText();
        Skip.If(text is null, "Extracted text is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        var missing = new List<string>();

        void Check(string what, string? key)
        {
            if (key is { Length: > 0 } && !text!.ContainsKey(key))
            {
                missing.Add($"{what} ({key})");
            }
        }

        foreach (var tradition in database.Traditions)
        {
            Check(tradition.Key, tradition.NameKey);
            Check(tradition.Key, tradition.DescriptionKey);
        }

        foreach (var perk in database.AscensionPerks)
        {
            Check(perk.Key, perk.NameKey);
        }

        foreach (var civic in database.Civics)
        {
            Check(civic.Key, civic.NameKey);
        }

        // And the wordings the swaps put in place of those, which nothing else asks the pruner for.
        foreach (var (owner, variants) in database.Traditions.Select(t => (t.Key, t.Variants))
                     .Concat(database.Civics.Select(c => (c.Key, c.Variants)))
                     .Concat(database.AscensionPerks.Select(p => (p.Key, p.Variants))))
        {
            foreach (var variant in variants)
            {
                Check($"{owner} swapped", variant.NameKey);
                Check($"{owner} swapped", variant.DescriptionKey);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} thing(s) would be drawn as a tidied-up key: " +
            string.Join("; ", missing.Take(8)));
    }

    /// <summary>
    /// A swap changes what an option is called, and the empire it belongs to is shown that name.
    /// </summary>
    /// <remarks>
    /// The numbers a swap changes were read already; the words were not, and there are more of
    /// those. A hive mind reading its own tradition trees was shown the wording written for
    /// somebody else throughout.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASwapRenamesTheOptionForTheEmpireItBelongsTo()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;
        var rules = new EmpireRules(database);

        // Enough of them that a future reading which quietly dropped the field would fail here.
        Assert.True(
            database.Traditions.Count(t => t.Variants.Count > 0) >= 60,
            $"Only {database.Traditions.Count(t => t.Variants.Count > 0)} traditions carry a swapped wording.");

        // A wilderness empire is one whose founder species is of that class, which is what the
        // game's own condition asks - not the origin, though the two go together in a real empire.
        var wilderness = EmpireDesignsFile.CreateEmpty().Add("Wild");
        wilderness.Authority = "auth_hive_mind";
        wilderness.SetEthics(["ethic_gestalt_consciousness"]);
        wilderness.Species.Class = "WILDERNESS";

        var ordinary = EmpireDesignsFile.CreateEmpty().Add("Hive");
        ordinary.Authority = "auth_hive_mind";
        ordinary.SetEthics(["ethic_gestalt_consciousness"]);
        ordinary.Species.Class = "MAM";

        var civic = database.Civics.Single(c => c.Key == "civic_hive_natural_neural_network");

        Assert.Equal(
            "civic_wilderness_natural_neural_network",
            rules.VariantOf(civic.Variants, rules.CreateContext(wilderness))?.NameKey);

        // And the same civic keeps the hive's own name for a hive that is not of the wilderness,
        // which is what says the condition is being read rather than the first swap always winning.
        Assert.Equal(
            "civic_hive_natural_neural_network",
            rules.VariantOf(civic.Variants, rules.CreateContext(ordinary))?.NameKey);
    }

    /// <summary>
    /// A government that doubles its own weight against a civic is judged at the doubled weight.
    /// </summary>
    /// <remarks>
    /// Thirteen do, and the government decides the empire's title and every name the generator
    /// would offer it - so reading only the base number is not a cosmetic loss.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AGovernmentsWeightCanTurnOnACivic()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var conditional = database.GovernmentTypes.Where(g => g.Factors.Count > 0).ToList();

        Assert.True(conditional.Count >= 13, $"Only {conditional.Count} governments weigh conditionally.");

        // Star Empire is the plain case: an ethic's weight ordinarily, twice that for an empire with
        // Distinguished Admiralty.
        var star = database.GovernmentTypes.Single(g => g.Key == "gov_star_empire");

        Assert.Single(star.Factors);
        Assert.Equal(2, star.Factors[0].Factor);
    }

    /// <summary>
    /// The modifiers a design can show are displayed as the game displays them.
    /// </summary>
    /// <remarks>
    /// Two thirds settle themselves by their ending and most of the rest by the numbers the game
    /// gives them. These are the ones that mislead: habitability is a proportion and says so in the
    /// game's own <c>"Habitability: $VALUE|0=-%$"</c>, while loyalty is a flat amount and says so
    /// just as plainly. A single value of 1 among thirty fractions used to decide the whole family
    /// was flat, which had Gaia and Machine worlds reading "+1" beside Ocean's "+20%".
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ModifiersAreShownTheWayTheGameShowsThem()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        (string Key, bool Percentage)[] expected =
        [
            ("pc_gaia_habitability", true),
            ("pc_ocean_habitability", true),
            ("pc_ai_habitability", true),
            ("army_health", true),
            ("species_leader_exp_gain", true),
            ("monthly_loyalty", false),
            ("monthly_loyalty_from_subjects", false),
            ("country_leader_pool_size", false),
            ("add_attunement_the_cradle_of_souls", false),
        ];

        foreach (var (key, percentage) in expected)
        {
            Assert.True(
                database.Modifiers.TryGetValue(key, out var info),
                $"{key} is not among the modifiers a design can show.");

            Assert.True(
                info!.IsPercentage == percentage,
                $"{key} is drawn as {(info.IsPercentage ? "a percentage" : "a flat amount")}.");
        }

        // Every world class the same way as every other, which is the check that would have caught
        // this: the family disagreeing with itself is what put it on screen two ways.
        var habitability = database.Modifiers
            .Where(m => m.Key.EndsWith("_habitability", StringComparison.Ordinal))
            .ToList();

        Assert.True(habitability.Count >= 20, $"Only {habitability.Count} habitability modifiers.");
        Assert.All(habitability, m => Assert.True(m.Value.IsPercentage, $"{m.Key} is drawn flat."));
    }

    /// <summary>
    /// The leader traits a game hands out are not carried, being a fifth of the download.
    /// </summary>
    /// <remarks>
    /// Nothing reads one. The ruler's picker and the validator both ask for the starting traits, no
    /// empire in the game's own files or the player's holds one, and two hundred and thirty-four of
    /// them have no name in any language the game ships.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OnlyTheTraitsADesignCanHoldAreCarried()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        Assert.DoesNotContain(database.Traits, t => t.Kind == TraitKind.Leader);

        // The ones a design does hold are all still there, which is the half that could go wrong:
        // the classification decides what is dropped, so a trait misread as a leader's would vanish
        // from the picker rather than merely from the download.
        Assert.Equal(34, database.Traits.Count(t => t.Kind == TraitKind.StartingRuler));
        Assert.True(
            database.Traits.Count(t => t.Kind == TraitKind.Species) >= 360,
            $"Only {database.Traits.Count(t => t.Kind == TraitKind.Species)} species traits survived.");
    }

    /// <summary>
    /// No modifier a design can show reaches the screen on a coin toss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four ways of settling one are the game's own script, the <c>_mult</c> or <c>_add</c>
    /// ending, the extractor's hand-written table, and the numbers the game writes it with agreeing
    /// among themselves. A modifier settled by none of those is displayed on a default, and the
    /// default is a coin toss between "+20%" and "+0.2".
    /// </para>
    /// <para>
    /// The six this caught are all counters written only as 1, 2 or -1 - the values that say nothing,
    /// because a proportion doubles with a 1 and a count increments with one. A content pack adding
    /// another such modifier fails here rather than quietly drawing it wrong, which is the whole
    /// point: the guarantee is exhaustiveness, not cleverness.
    /// </para>
    /// </remarks>

    /// <summary>
    /// The three packs that are a single species portrait each still are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They have no badge sprite - <c>PackIcon</c> looks for one named after the pack and the game
    /// ships none - so the content bar drew them as their initials. Each borrows the face of the
    /// portrait it adds instead, which only works because each gates exactly one.
    /// </para>
    /// <para>
    /// This is the half of that arrangement the real installation can answer. Whether a face is
    /// actually attached depends on the portrait having been drawn, which happens after extraction,
    /// so <c>LendFacesGivesAPackTheOnePortraitItGates</c> covers the rest against a fixture.
    /// </para>
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ThePacksWithNoBadgeGateOnePortraitEach()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var bare = database.Dlc
            .Where(d => d.Decides && !d.HasOwnIcon)
            .Select(d => d.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["Rick The Cube Species Portrait", "Stargazer Species Portrait", "Vipra the Vapor Species Portrait"],
            bare);

        foreach (var pack in bare)
        {
            var gated = database.PortraitSets
                .SelectMany(s => s.Portraits)
                .Where(e => e.Playable is DlcRequirement dlc && dlc.Name == pack)
                .Select(e => e.Key)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.True(
                gated.Count == 1,
                $"{pack} gates {gated.Count} portraits ({string.Join(", ", gated)}), so there is no " +
                "one face for it to borrow.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void NothingIsLeftToAGuess()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var unsettled = database.Modifiers
            .Where(m => !m.Value.Settled)
            .Select(m => m.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unsettled.Count == 0,
            $"{unsettled.Count} modifier(s) would be drawn on a default, with nothing saying whether " +
            $"they are proportions: {string.Join(", ", unsettled.Take(10))}. Settle each in " +
            "ModifierCatalog.Settled with the evidence for it.");
    }


    /// <summary>
    /// Every modifier is coloured the way the game colours it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game declares <c>good</c> only for the few dozen modifiers its script files invent;
    /// everything defined in code - which is nearly everything a design shows - carries no statement
    /// at all, and <see cref="ModifierCatalog"/> has to infer it from the name. So the game is asked
    /// a different way: its own English descriptions colour values by hand, and a modifier written
    /// as <c>§G-10%§!</c> or <c>§R+10%§!</c> is one the game itself treats as bad.
    /// </para>
    /// <para>
    /// Only where the game is unanimous, and only where it says anything at all. About a hundred and
    /// forty modifiers are written this way; twenty-six of them are consistently inverted, none is
    /// written both ways, and the rest are the ordinary direction. The silent majority are not
    /// judged here - there is no evidence about them and inventing some would be worse than the
    /// guess.
    /// </para>
    /// <para>
    /// This is what found the original fault: Empire Size from Pops drew green when positive, which
    /// is backwards - the game shows Psionic Theory's ten per cent reduction in green. It is kept so
    /// that a patch adding another such modifier fails here rather than reading the wrong way round
    /// on a card.
    /// </para>
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryModifierIsColouredTheWayTheGameColoursIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var wrong = ColouredByTheGame()
            .Where(stated => database.Modifiers.TryGetValue(stated.Key, out var info)
                && info.IsGood != stated.Value)
            .Select(stated => $"{stated.Key} should be {(stated.Value ? "good" : "bad")}")
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            wrong.Count == 0,
            $"{wrong.Count} modifier(s) are coloured against the game's own descriptions: " +
            $"{string.Join(", ", wrong)}. Settle each in ModifierCatalog.LooksBad.");
    }

    /// <summary>
    /// What the game's own descriptions say about which direction is the good one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A description writes a modifier as <c>$MOD_SOMETHING$: §G+10%§!</c> - the token, then a
    /// colour code, then a sign. Green-and-plus or red-and-minus is the ordinary direction;
    /// green-and-minus or red-and-plus is the inverted one. Anything a file writes both ways is
    /// dropped rather than guessed at, and there are none at present.
    /// </para>
    /// <para>
    /// The sign has to be printed against a printed number. Sixty-four of these write a variable
    /// instead - <c>§G+$@telekinesis_amenities_mult|0%$</c> - where the sign in the text is the
    /// localiser's and the sign of the value is the script's, and in that very case they disagree:
    /// the variable is -0.2. Read literally it says psionic amenities usage is a good thing to have
    /// more of, which is the opposite of what the same file says about every other amenities usage
    /// modifier. So a sign that is not followed by a digit is not evidence.
    /// </para>
    /// </remarks>
    private static IReadOnlyDictionary<string, bool> ColouredByTheGame()
    {
        var folder = Path.Combine(InstallRoot!, "localisation", "english");
        var stated = new Dictionary<string, List<bool>>(StringComparer.Ordinal);

        var pattern = new System.Text.RegularExpressions.Regex(
            @"\$(MOD_[A-Z0-9_]+)\$[^$]{0,40}?\u00a7(?<colour>[GR])\s*(?<sign>[+-])[0-9]",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(10));

        foreach (var file in Directory.EnumerateFiles(folder, "*.yml", SearchOption.AllDirectories))
        {
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(File.ReadAllText(file)))
            {
                // MOD_EMPIRE_SIZE_POPS_MULT is the token for empire_size_pops_mult.
                var key = match.Groups[1].Value[4..].ToLowerInvariant();
                var green = match.Groups["colour"].Value == "G";
                var up = match.Groups["sign"].Value == "+";

                if (!stated.TryGetValue(key, out var seen))
                {
                    stated[key] = seen = [];
                }

                seen.Add(green == up);
            }
        }

        return stated
            .Where(m => m.Value.Distinct().Count() == 1)
            .ToDictionary(m => m.Key, m => m.Value[0], StringComparer.Ordinal);
    }


    /// <summary>
    /// A civic refused for a civic the empire holds says which one.
    /// </summary>
    /// <remarks>
    /// Reanimated Armies writes its exclusions as two lists of civics in one block. The second
    /// carries a sentence of the game's own about Sovereign Guardianship; the first names Citizen
    /// Service and carries nothing. So an empire holding both was told why, an empire holding only
    /// Citizen Service was told nothing at all, and releasing the guardianship left the option
    /// refused with the explanation gone and the refusal still there.
    ///
    /// Written against the real files because that is where the shape came from - two sibling
    /// clauses, one labelled and one not - and a fixture would only be testing the fixture.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ACivicRefusedForAnotherCivicSaysWhichOne()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var design = EmpireDesignsFile.CreateEmpty().Add("Test");
        design.Species.Class = "MAM";
        design.Authority = "auth_democratic";
        design.SetEthics(["ethic_militarist"]);
        design.SetCivics(["civic_citizen_service"]);

        var rules = new EmpireRules(database);
        var reanimators = rules.GetCivicOptions(rules.CreateContext(design))
            .Single(o => o.Key == "civic_reanimated_armies");

        Assert.False(reanimators.Enabled);
        Assert.Contains(
            RuleReasons.For(RuleReasons.Excluded, "civic_citizen_service"),
            reanimators.Reasons);
    }

    /// <summary>
    /// A swap that replaces an option's numbers with a sentence is shown as the sentence.
    /// </summary>
    /// <remarks>
    /// Not a missing sentence but wrong numbers. Expert Negotiation sells specimens more dearly;
    /// for a homicidal empire the swap brings no modifier at all and one line of prose describing
    /// something else entirely. Reading only the numbers meant such an empire was shown the modifier
    /// it does not get and nothing of what it does.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASwapThatReplacesTheNumbersWithWordsSaysTheWords()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var tradition = database.Traditions.Single(t => t.Key == "tr_archivism_expert_negociation");

        var swapped = tradition.Effects.Conditional
            .Single(c => c.TooltipKey == "tr_archivism_expert_negociation_homicidal_tt");

        Assert.True(swapped.TooltipReplacesModifiers, "The sentence should stand in for the numbers.");
        Assert.Empty(swapped.Modifiers);

        // And the base's own modifier is now conditional on no swap having claimed it, so the
        // homicidal empire is not shown it.
        Assert.Empty(tradition.Effects.Modifiers);

        Assert.Contains(
            tradition.Effects.Conditional,
            c => c.Modifiers.ContainsKey("country_specimen_selling_cost_mult"));

        // Enough of them that a reading which dropped the field would fail here rather than quietly.
        var replacing = database.Traditions
            .SelectMany(t => t.Effects.Conditional)
            .Count(c => c.TooltipReplacesModifiers);

        Assert.True(replacing >= 50, $"Only {replacing} swaps replace their numbers with a sentence.");
    }

    /// <summary>
    /// A definition that keeps part of itself in a shared fragment is read with that part in place.
    /// </summary>
    /// <remarks>
    /// <c>inline_script</c> is an include with parameters, and what it carries is not decoration.
    /// The nine automatic habitability traits keep their <c>hidden = yes</c> in one, so every one of
    /// them was offered in a picker the game does not show them in; nineteen traditions keep their
    /// hive and machine renamings in another, so a gestalt empire read the wording written for
    /// somebody else. Both were invisible from the outside - the definitions parse perfectly well
    /// without the fragment, they just say less than the game reads.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ADefinitionIsReadWithItsSharedFragmentsInPlace()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // hidden = yes and initial = no live only in traits/auto_preference_planet_class.
        var automatic = database.Traits
            .Where(t => t.Key.StartsWith("trait_auto_pc_", StringComparison.Ordinal))
            .ToList();

        Assert.True(automatic.Count >= 9, $"Only {automatic.Count} automatic preference traits.");
        Assert.All(automatic, t => Assert.True(t.Hidden, $"{t.Key} would be offered in the picker."));
        Assert.All(automatic, t => Assert.False(t.Initial, $"{t.Key} claims to be an opening choice."));

        // And the renaming a gestalt empire is shown, which lives in paragon/tradition_swap_desc_*.
        var tradition = database.Traditions.Single(t => t.Key == "tr_aptitude_the_empire_needs_you");

        Assert.Contains(tradition.Variants, v => v.NameKey == "tr_aptitude_the_empire_needs_you_hive");
        Assert.Contains(tradition.Variants, v => v.NameKey == "tr_aptitude_the_empire_needs_you_machine");

        // The icon scripts stay unexpanded, being layers rather than fields, and the composer that
        // walks them still finds what it draws. Every ruler trait the designer offers is drawn that
        // way, so all thirty-four losing their artwork at once is what this would look like.
        Assert.All(
            database.Traits.Where(t => t.Kind == TraitKind.StartingRuler),
            t => Assert.NotNull(t.Icon));
    }

    /// <summary>
    /// A government says what it calls the ruler's heir, not only the ruler.
    /// </summary>
    /// <remarks>
    /// Both were sitting in the same block and only the ruler's was read, so the designer offered
    /// Emperor and Chief Executive in the heir's box where the game means Crown Prince and Secundus,
    /// and showed no default behind it while the game had one.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AGovernmentNamesTheHeirAsWellAsTheRuler()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var named = database.GovernmentTypes.Where(g => g.HeirTitleKey is { Length: > 0 }).ToList();

        Assert.True(named.Count >= 31, $"Only {named.Count} governments name an heir title.");

        // And they are their own vocabulary rather than the rulers' - which is the half that made
        // the dropdown wrong rather than merely empty.
        var rulers = database.GovernmentTypes
            .Select(g => g.RulerTitleKey)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(named, g => rulers.Contains(g.HeirTitleKey!));

        var text = ShippedText();
        Skip.If(text is null, "Extracted text is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        Assert.All(named, g => Assert.True(
            text!.ContainsKey(g.HeirTitleKey!),
            $"{g.Key} names the heir title {g.HeirTitleKey}, which has no text shipped for it."));
    }

    /// <summary>
    /// A swap that names its own drawbacks is read, so the empire it applies to reads its own.
    /// </summary>
    /// <remarks>
    /// Arc Welders lists one set of drawbacks and another for a nomad; Life-Seeded another for a
    /// machine. A third swap names the key its option already has, which is why this asserts on the
    /// two that differ rather than on a count.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASwapCanNameItsOwnDrawbacks()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        var text = ShippedText();
        Skip.If(text is null, "Extracted text is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        (string Civic, string Expected)[] cases =
        [
            ("origin_arc_welders", "origin_tooltip_arc_builders_nomadic_negative_effects"),
            ("origin_life_seeded", "origin_tooltip_life_seeded_machine_negative_effects"),
        ];

        foreach (var (key, expected) in cases)
        {
            var civic = database.Civics.Single(c => c.Key == key);

            Assert.Contains(civic.Variants, v => v.PenaltyKey == expected);

            // Different from the option's own, or reading it would change nothing.
            Assert.NotEqual(expected, civic.Effects.PenaltyKey);

            // And shipped, or the empire would be shown the key with its underscores taken out.
            Assert.True(text!.ContainsKey(expected), $"{expected} has no text shipped for it.");
        }
    }

    /// <summary>
    /// A condition about a trait is answered by the traits the species has, not the ones written down.
    /// </summary>
    /// <remarks>
    /// A habitability preference is forced by the homeworld and deliberately never written into a
    /// design - <c>GetWrittenForcedTraits</c> excludes exactly that source - so asking the written
    /// list whether an ocean species has <c>trait_pc_ocean_preference</c> answered no for every
    /// empire in the game. Hydrocentric's whole condition is that question, so the perk was hidden
    /// from every empire that qualifies for it, and hidden rather than blocked, which is the kind
    /// that leaves nothing on screen to wonder about.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ATraitTheHomeworldForcedStillAnswersForItself()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;
        var rules = new EmpireRules(database);

        var design = EmpireDesignsFile.CreateEmpty().Add("Ocean");
        design.Species.Class = "MAM";
        design.Authority = "auth_democratic";
        design.SetEthics(["ethic_xenophile", "ethic_pacifist"]);
        design.PlanetClass = "pc_ocean";

        var context = rules.CreateContext(design, database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal));

        // The design says nothing about a preference, and the empire has one all the same.
        Assert.DoesNotContain("trait_pc_ocean_preference", context.Traits);
        Assert.Contains("trait_pc_ocean_preference", context.EffectiveTraits);

        // So a condition asking about it is answered yes, and the perk built on that question is
        // offered rather than hidden.
        Assert.True(
            context.Has(SelectionCategory.Traits, "trait_pc_ocean_preference"),
            "A forced habitability preference does not answer has_trait.");

        var hydrocentric = rules.GetAscensionPerkOptions(context, [], [])
            .SingleOrDefault(o => o.Key == "ap_hydrocentric");

        Assert.True(hydrocentric is not null, "Hydrocentric is not among the perks at all.");
        Assert.True(hydrocentric!.Visible, "Hydrocentric is hidden from an ocean empire that qualifies.");

        // And an empire with no such preference still does not see it, or the gate would mean nothing.
        var inland = EmpireDesignsFile.CreateEmpty().Add("Inland");
        inland.Species.Class = "MAM";
        inland.Authority = "auth_democratic";
        inland.SetEthics(["ethic_xenophile", "ethic_pacifist"]);
        inland.PlanetClass = "pc_desert";

        Assert.False(
            rules.GetAscensionPerkOptions(
                    rules.CreateContext(inland, database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal)),
                    [],
                    [])
                .Single(o => o.Key == "ap_hydrocentric").Visible,
            "Hydrocentric is offered to an empire with no ocean preference.");
    }

    /// <summary>The text the app ships, which is the pruned set rather than the game's whole one.</summary>
    private static Dictionary<string, string>? ShippedText()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "Sem.Web", "wwwroot", "gamedata", "loc", "en.json");

            if (File.Exists(path))
            {
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllBytes(path));
            }
        }

        return null;
    }
}
