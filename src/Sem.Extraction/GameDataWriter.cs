using System.Text.Json;
using Sem.Extraction.Extractors;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction;

/// <summary>What a full extraction produced.</summary>
/// <param name="Database">The database, with image paths filled in.</param>
/// <param name="LocalisationEntries">How many pieces of display text were kept.</param>
/// <param name="DatabaseBytes">Size of the written database.</param>
/// <param name="LocalisationBytes">Size of the written text.</param>
/// <param name="Images">What came of converting the game's images.</param>
/// <param name="Portraits">What came of drawing the portraits.</param>
/// <param name="Ships">What came of drawing one ship for each appearance set.</param>
/// <param name="MissingImages">Images the data referred to that the installation does not have.</param>
/// <param name="ScriptFailures">Script files that would not read, with the reason.</param>
public sealed record ExtractionResult(
    GameDatabase Database,
    int LocalisationEntries,
    int DatabaseBytes,
    int LocalisationBytes,
    BakeReport Images,
    PortraitBakeReport Portraits,
    ShipBakeReport Ships,
    IReadOnlyList<string> MissingImages,
    IReadOnlyList<string> ScriptFailures)
{
    /// <summary>What came of drawing the wardrobe, or nothing where it was not asked for.</summary>
    public PortraitBakeReport? Wardrobe { get; init; }

    /// <summary>How many records each wiki pack came to, and how big each file is.</summary>
    public IReadOnlyList<(string Domain, int Records, int Bytes)> WikiPacks { get; init; } = [];

    /// <summary>How many outfits it came to, across how many layers.</summary>
    public (int Outfits, int Layers) WardrobeSize { get; init; }
}

/// <summary>
/// Runs a whole extraction and writes everything the designer needs into one directory.
/// </summary>
/// <remarks>
/// Both hosts use this, which is the point of it: the desktop app filling its cache and the build
/// preparing the web site are the same job, and when they were written separately the desktop
/// quietly stopped drawing portraits.
/// </remarks>
public static class GameDataWriter
{
    /// <summary>The database file, relative to the output directory.</summary>
    public const string DatabaseFileName = "gamedb.json";

    /// <summary>The wardrobe file, relative to the output directory.</summary>
    public const string WardrobeFileName = "wardrobe.json";

    /// <summary>Where the wiki's own files live, one per domain.</summary>
    public const string WikiDirectory = "wiki";

    /// <summary>The file one wiki domain is written to, relative to the output directory.</summary>
    /// <param name="domain">Which domain, such as <c>leader-traits</c>.</param>
    /// <returns>The relative path, with a forward slash, as both hosts ask for it.</returns>
    public static string WikiPackFileName(string domain) => $"{WikiDirectory}/{domain}.json";

    /// <summary>Reads an installation and writes the database, its text and its images.</summary>
    /// <param name="installRoot">The game to read.</param>
    /// <param name="outputDirectory">Where everything is written.</param>
    /// <param name="file">The guarded writer everything goes through.</param>
    /// <param name="progress">Told what is happening, for a host that shows it.</param>
    /// <param name="wardrobe">
    /// Whether to draw every outfit, hairstyle and skin as well. Thousands of pictures and the bulk
    /// of a run, so it is asked for rather than assumed - but both hosts that show a dressed ruler
    /// need it, and it lived in the command-line tool alone until the desktop was found drawing a
    /// flat thumbnail instead. That is the same way the portraits were once lost; see the remark on
    /// this class.
    /// </param>
    public static ExtractionResult Write(
        string installRoot,
        string outputDirectory,
        SafeFile file,
        IProgress<string>? progress = null,
        bool wardrobe = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(file);

        var content = LayeredContent.ForInstall(installRoot);
        var extractor = new GameDataExtractor(content);

        var database = extractor.Extract(progress);

        progress?.Report("Reading text");
        var localisation = extractor.ExtractLocalisation(reachableFrom: database);
        var localisationJson = JsonSerializer.SerializeToUtf8Bytes(
            localisation, GameDataJsonContext.Default.DictionaryStringString);

        file.WriteAllBytes(Path.Combine(outputDirectory, "loc", "en.json"), localisationJson);

        var assets = Path.Combine(outputDirectory, "assets");
        var images = new AssetBaker(content, file).Bake(extractor.Assets, assets, progress);

        // Portraits are models rather than pictures, so each one has to be drawn.
        var (portraits, portraitReport) = new PortraitBaker(content, file)
            .Bake(database.Portraits, assets, progress);

        // So are ships, and for the same reason: the game shows a shipset by spinning it.
        var shipBaker = new ShipBaker(content, file);
        var (sets, shipReport) = shipBaker.Bake(database.GraphicalCultures, assets, progress);

        // And the arkships, which the game also shows as live models — its own panel for them is
        // called 3d_icons_arkships. The only flat art anywhere near them is one sheet frame shared
        // by all nine.
        var (arkships, arkshipReport) = shipBaker.BakeArkships(database.Arkships, assets, progress);

        // Written last, once every image path it refers to is known.
        database = database with { Portraits = portraits, GraphicalCultures = sets, Arkships = arkships };

        // And the packs with no badge of their own borrow a face, which is a portrait's thumbnail
        // and so is only knowable now that the portraits have been drawn.
        database = database with { Dlc = GameDataExtractor.LendFaces(database) };
        var json = JsonSerializer.SerializeToUtf8Bytes(database, GameDataJsonContext.Default.GameDatabase);
        file.WriteAllBytes(Path.Combine(outputDirectory, DatabaseFileName), json);

        var (outfits, wardrobeReport) = wardrobe
            ? WriteWardrobe(content, database, outputDirectory, file, progress)
            : ([], null);

        // The wiki's own files, always written. They are small and cost nothing to build - no
        // pictures are drawn for them beyond the icons already baked - and a host that skipped them
        // would have a wiki page that could never fill itself.
        var packs = WriteWikiPacks(
            extractor, database, shipReport.Fleet, outputDirectory, file, progress);

        return new ExtractionResult(
            database,
            localisation.Count,
            json.Length,
            localisationJson.Length,
            images,
            portraitReport,
            shipReport with
            {
                Rendered = shipReport.Rendered + arkshipReport.Rendered,
                Bytes = shipReport.Bytes + arkshipReport.Bytes,
                Failures = [.. shipReport.Failures, .. arkshipReport.Failures],
            },
            extractor.Assets.Missing,
            extractor.ScriptFailures)
        {
            Wardrobe = wardrobeReport,
            WardrobeSize = (outfits.Count, outfits.Sum(o => o.Layers.Count)),
            WikiPacks = packs,
        };
    }

    /// <summary>
    /// Writes the files the wiki fetches for itself, one per domain.
    /// </summary>
    /// <remarks>
    /// Each carries its records and the text they are written in, because <c>loc/en.json</c> cannot:
    /// that file is pruned to what the database reaches, and a pack is by definition about things
    /// the database does not carry. Not one of the seven hundred leader traits has a name in it.
    /// </remarks>
    private static List<(string Domain, int Records, int Bytes)> WriteWikiPacks(
        GameDataExtractor extractor,
        GameDatabase database,
        IReadOnlyList<ShipRender> fleet,
        string outputDirectory,
        SafeFile file,
        IProgress<string>? progress)
    {
        progress?.Report("Writing the wiki's own data");

        var all = extractor.ExtractLocalisation();
        var leaders = extractor.LeaderTraits;

        var pack = new LeaderTraitPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, LeaderTraitPack.CurrentSchemaVersion),
            Traits = leaders,
            Text = LocalisationPruner.Slice(
                leaders.SelectMany(Spoken),
                all,
                database.ScriptedText),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(pack, GameDataJsonContext.Default.LeaderTraitPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(LeaderTraitPack.Domain)), json);

        var ships = new ShipsetPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, ShipsetPack.CurrentSchemaVersion),

            Fleets =
            [
                .. fleet
                    .GroupBy(f => f.Set, StringComparer.Ordinal)
                    .Select(g => new ShipsetFleet(g.Key)
                    {
                        Ships = [.. g.Select(f => new ShipsetShip(f.ShipClass, f.Image))],
                    }),
            ],

            // What the classes are called. The game keeps these under their bare keys - corvette,
            // battleship - and nothing in the database reaches one, so the pruner has never had a
            // reason to keep them and the pack carries them itself.
            Text = LocalisationPruner.Slice(
                fleet.Select(f => f.ShipClass).Distinct(StringComparer.Ordinal),
                all,
                database.ScriptedText),
        };

        var shipJson = JsonSerializer.SerializeToUtf8Bytes(
            ships, GameDataJsonContext.Default.ShipsetPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(ShipsetPack.Domain)), shipJson);

        var personalities = new PersonalityPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, PersonalityPack.CurrentSchemaVersion),

            Personalities = extractor.Personalities,

            // The weapon types they name, which the game localises and the pruner has never had a
            // reason to keep - nothing in the database reaches one.
            Text = LocalisationPruner.Slice(
                extractor.Personalities.Select(d => d.Weapons).OfType<string>().Distinct(StringComparer.Ordinal),
                all,
                database.ScriptedText),
        };

        var personalityJson = JsonSerializer.SerializeToUtf8Bytes(
            personalities, GameDataJsonContext.Default.PersonalityPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(PersonalityPack.Domain)), personalityJson);

        // The government family, which is four files because the civics and the origins are one
        // collection the game tells apart by a flag - so the two pages about them read one pack.
        var family = extractor.Family;

        var ethics = new EthicPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, EthicPack.CurrentSchemaVersion),

            Ethics = family.Ethics,

            // The drift sentences, which the game writes for its own tooltip and nothing in the
            // database reaches - so the pruner has never had a reason to keep one.
            Text = LocalisationPruner.Slice(
                family.Ethics.SelectMany(e => e.Drift).Select(d => d.DescriptionKey),
                all,
                database.ScriptedText),
        };

        var ethicJson = JsonSerializer.SerializeToUtf8Bytes(
            ethics, GameDataJsonContext.Default.EthicPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(EthicPack.Domain)), ethicJson);

        var authorities = new AuthorityPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, AuthorityPack.CurrentSchemaVersion),
            Authorities = family.Authorities,
        };

        var authorityJson = JsonSerializer.SerializeToUtf8Bytes(
            authorities, GameDataJsonContext.Default.AuthorityPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(AuthorityPack.Domain)), authorityJson);

        var governments = new GovernmentPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, GovernmentPack.CurrentSchemaVersion),
            Governments = family.Governments,
        };

        var governmentJson = JsonSerializer.SerializeToUtf8Bytes(
            governments, GameDataJsonContext.Default.GovernmentPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(GovernmentPack.Domain)), governmentJson);

        var civics = new CivicPack
        {
            Stamp = new WikiPackStamp(
                GameDataExtractor.ExtractorVersion, CivicPack.CurrentSchemaVersion),

            // No text of its own. Everything this pack names - the civic it becomes, the packs its
            // AI gate asks for - the database already reaches, so the pruner has already kept it.
            Civics = family.Civics,
        };

        var civicJson = JsonSerializer.SerializeToUtf8Bytes(
            civics, GameDataJsonContext.Default.CivicPack);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WikiPackFileName(CivicPack.Domain)), civicJson);

        return
        [
            (LeaderTraitPack.Domain, leaders.Count, json.Length),
            (ShipsetPack.Domain, fleet.Count, shipJson.Length),
            (PersonalityPack.Domain, extractor.Personalities.Count, personalityJson.Length),
            (EthicPack.Domain, family.Ethics.Count, ethicJson.Length),
            (AuthorityPack.Domain, family.Authorities.Count, authorityJson.Length),
            (GovernmentPack.Domain, family.Governments.Count, governmentJson.Length),
            (CivicPack.Domain, family.Civics.Count, civicJson.Length),
        ];
    }

    /// <summary>
    /// Every key one leader trait is written in.
    /// </summary>
    /// <remarks>
    /// Its name and its prose, and then everything its effects name. A leader trait says most of
    /// what it does through a tooltip rather than through plain numbers - Galactic Paragons writes
    /// "custom_tooltip_with_modifiers = leader_trait_adventurous_spirit_effect" and leaves the
    /// sentence to localisation - so seeding from the name alone left the effects column reading
    /// the key back prettified.
    /// </remarks>
    /// <param name="trait">The trait.</param>
    /// <returns>The keys, some of which the game may not define.</returns>
    private static IEnumerable<string> Spoken(LeaderTraitDefinition trait)
    {
        yield return trait.NameKey;
        yield return trait.DescriptionKey;

        foreach (var key in Said(trait.Effects))
        {
            yield return key;
        }
    }

    /// <summary>The keys one set of effects names, its conditional parts included.</summary>
    private static IEnumerable<string> Said(EffectSet effects)
    {
        foreach (var key in new[] { effects.DescriptionKey, effects.TooltipKey, effects.PenaltyKey })
        {
            if (key is { Length: > 0 })
            {
                yield return key;
            }
        }

        foreach (var tag in effects.TagKeys)
        {
            yield return tag;
        }

        foreach (var part in effects.Conditional)
        {
            foreach (var key in new[] { part.TooltipKey })
            {
                if (key is { Length: > 0 })
                {
                    yield return key;
                }
            }

            foreach (var tag in part.TagKeys)
            {
                yield return tag;
            }
        }
    }

    /// <summary>
    /// Draws every outfit, hairstyle and skin each portrait can wear, and writes them beside the
    /// database.
    /// </summary>
    /// <remarks>
    /// Beside the database rather than inside it: the empire designer shows one face per portrait
    /// and should not read a wardrobe to do it.
    /// </remarks>
    private static (IReadOnlyList<PortraitOutfit> Outfits, PortraitBakeReport Report) WriteWardrobe(
        LayeredContent content,
        GameDatabase database,
        string outputDirectory,
        SafeFile file,
        IProgress<string>? progress)
    {
        progress?.Report("Drawing every outfit, hairstyle and skin");

        var (outfits, report) = new PortraitBaker(content, file)
            .BakeWardrobe(database.Portraits, Path.Combine(outputDirectory, "assets"), progress);

        file.WriteAllBytes(
            Path.Combine(outputDirectory, WardrobeFileName),
            JsonSerializer.SerializeToUtf8Bytes(
                outfits, GameDataJsonContext.Default.IReadOnlyListPortraitOutfit));

        return (outfits, report);
    }
}
