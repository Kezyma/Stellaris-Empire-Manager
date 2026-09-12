using System.Text.Json;
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
        };
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
