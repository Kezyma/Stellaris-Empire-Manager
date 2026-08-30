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
public sealed record ExtractionResult(
    GameDatabase Database,
    int LocalisationEntries,
    int DatabaseBytes,
    int LocalisationBytes,
    BakeReport Images,
    PortraitBakeReport Portraits,
    ShipBakeReport Ships,
    IReadOnlyList<string> MissingImages);

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

    /// <summary>Reads an installation and writes the database, its text and its images.</summary>
    public static ExtractionResult Write(
        string installRoot,
        string outputDirectory,
        SafeFile file,
        IProgress<string>? progress = null)
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
        var (sets, shipReport) = new ShipBaker(content, file)
            .Bake(database.GraphicalCultures, assets, progress);

        // Written last, once every image path it refers to is known.
        database = database with { Portraits = portraits, GraphicalCultures = sets };
        var json = JsonSerializer.SerializeToUtf8Bytes(database, GameDataJsonContext.Default.GameDatabase);
        file.WriteAllBytes(Path.Combine(outputDirectory, DatabaseFileName), json);

        return new ExtractionResult(
            database,
            localisation.Count,
            json.Length,
            localisationJson.Length,
            images,
            portraitReport,
            shipReport,
            extractor.Assets.Missing);
    }
}
