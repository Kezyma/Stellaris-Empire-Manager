using System.IO;
using System.Text.Json;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// Reads the extracted data straight off disk, for the host that built it.
/// </summary>
/// <remarks>
/// <para>
/// The desktop used to share the web's source, pointing an <c>HttpClient</c> at a hostname it
/// mapped with <c>SetVirtualHostNameToFolderMapping</c>. That mapping is the embedded browser
/// intercepting requests its own renderer makes; a managed HttpClient running in this process never
/// goes near it, so every load ended in "No such host is known (gamedata.sem:443)". It had never
/// worked, and nothing noticed because a second fault stopped the designer mounting at all.
/// </para>
/// <para>
/// Reading the files is also simply the right answer here: this host extracted them itself, a few
/// moments earlier, into a folder it chose. The images are still served through the virtual host,
/// because those are fetched by the page rather than by this process and that is exactly what the
/// mapping is for.
/// </para>
/// </remarks>
/// <param name="directory">The cache folder holding <c>gamedb.json</c>, <c>loc/</c> and the assets.</param>
/// <param name="assetBaseUrl">Where the page should fetch images from, as it can reach them.</param>
public sealed class FileGameDataSource(string directory, string assetBaseUrl) : IGameDataSource, IDisposable
{
    private readonly string _directory = !string.IsNullOrWhiteSpace(directory)
        ? directory
        : throw new ArgumentException("A cache directory is required.", nameof(directory));

    private readonly string _assetBaseUrl = assetBaseUrl ?? throw new ArgumentNullException(nameof(assetBaseUrl));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _wardrobeGate = new(1, 1);

    private Sem.Ui.Services.GameData? _loaded;
    private IReadOnlyList<PortraitOutfit>? _wardrobe;

    /// <inheritdoc />
    public async Task<Sem.Ui.Services.GameData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded is not null)
        {
            return _loaded;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Checked again inside the gate: several components ask for this at once when the
            // designer first opens, and reading it more than once would be wasteful.
            if (_loaded is not null)
            {
                return _loaded;
            }

            var database = await ReadAsync(
                "gamedb.json", GameDataJsonContext.Default.GameDatabase, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The game database could not be read.");

            // Refused rather than read around, as the web host refuses it: a database of an older
            // shape deserialises without complaint and every field added since takes its default, so
            // the designer would run on rules it silently does not have.
            if (database.SchemaVersion != GameDatabase.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The cached game data was built for a different version of this app "
                    + $"(found {database.SchemaVersion}, expected {GameDatabase.CurrentSchemaVersion}).");
            }

            var localisation = await ReadAsync(
                Path.Combine("loc", "en.json"),
                GameDataJsonContext.Default.DictionaryStringString,
                cancellationToken).ConfigureAwait(false) ?? [];

            _loaded = new Sem.Ui.Services.GameData(database, localisation, _assetBaseUrl);
            return _loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortraitOutfit>> LoadWardrobeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_wardrobe is not null)
        {
            return _wardrobe;
        }

        await _wardrobeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_wardrobe is not null)
            {
                return _wardrobe;
            }

            // A cache built without one is not a fault: the wardrobe is baked separately, and the
            // section that wants it says so rather than failing.
            _wardrobe = await ReadAsync(
                "wardrobe.json",
                GameDataJsonContext.Default.IReadOnlyListPortraitOutfit,
                cancellationToken).ConfigureAwait(false) ?? [];

            return _wardrobe;
        }
        finally
        {
            _wardrobeGate.Release();
        }
    }

    /// <summary>Reads one file from the cache as JSON, or nothing where it is not there.</summary>
    private async Task<T?> ReadAsync<T>(
        string relativePath,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_directory, relativePath);

        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the two gates that keep a concurrent load from reading twice.</summary>
    public void Dispose()
    {
        _gate.Dispose();
        _wardrobeGate.Dispose();
    }
}
