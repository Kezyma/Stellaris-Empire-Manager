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
            // Nine, not eleven: a volcanic world and an arkship are each flagged initial, and each
            // is also flagged starting_planet = no. Neither is a world a player may simply pick —
            // an Infernal species class adds the first, and being nomadic puts you on the second.
            ("starting worlds", 9, database.PlanetClasses.Count(p => p.IsStartingWorld)),
            ("starting systems", 23, database.Initializers.Count),
            ("advisor voices", 27, database.AdvisorVoices.Count),
            // Sixty-six: the forty-one the game's own designer offers, plus twenty-five it hands out
            // during play and has artwork for. The selector names one more, synth_queen_room, which
            // no installation has a picture for.
            ("rooms", 66, database.Rooms.Count),
            ("rooms the designer offers", 41, database.Rooms.Count(r => r.IsOffered)),

            // Twenty-one named sets of country flags, carried by the game's own empires.
            ("empire flag sets", 21, database.EmpireFlagSets.Count),
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

        // Galactic Paragons stacks layers into an icon block, which the leader traits use. Nothing
        // here draws layers, so those fall back rather than reaching into the block for a value that
        // is only one of several stacked pictures.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_unknown.dds",
            sources["icons/traits/leader_trait_unplugged_cybernetic_positives_1.png"]);

        // Its species-trait twin writes the same artwork as a plain path, and that is honoured.
        Assert.Equal(
            "gfx/interface/icons/traits/trait_unplugged_positive_1.dds",
            sources["icons/traits/trait_unplugged_cybernetic_positives_1.png"]);
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
}
