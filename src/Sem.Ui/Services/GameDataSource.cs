using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>Everything the designer needs about a Stellaris installation.</summary>
/// <param name="Database">The extracted game data.</param>
/// <param name="Localisation">Display text, keyed as the game keys it.</param>
/// <param name="AssetBaseUrl">Where the extracted images are served from.</param>
public sealed record GameData(
    GameDatabase Database,
    IReadOnlyDictionary<string, string> Localisation,
    string AssetBaseUrl)
{
    /// <summary>The address of an extracted image, given its path in the database.</summary>
    public string AssetUrl(string? relativePath) =>
        string.IsNullOrEmpty(relativePath) ? string.Empty : $"{AssetBaseUrl}/{relativePath}";
}

/// <summary>Loads the extracted game data.</summary>
public interface IGameDataSource
{
    /// <summary>Loads the data, or returns what was loaded before.</summary>
    Task<GameData> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the pieces a portrait is drawn from, or nothing where this host publishes none.
    /// </summary>
    /// <remarks>
    /// Kept apart from the rest and fetched only when something asks. It is a couple of megabytes
    /// describing eight thousand pictures, and reading that in a browser is not free — nobody who
    /// never opens the ruler's appearance should pay for it.
    /// </remarks>
    Task<IReadOnlyList<PortraitOutfit>> LoadWardrobeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one of the wiki's own files, or nothing where this host publishes none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same arrangement as the wardrobe above, carried further. The wiki has a page for each of
    /// a great many things the empire designer has no use for - the game's leader traits alone are
    /// seven hundred records nothing in an empire can hold - so each domain gets a file of its own,
    /// fetched the first time somebody opens the page about it.
    /// </para>
    /// <para>
    /// Keyed rather than a method each, because there will be twenty of these and a field and a
    /// gate per domain does not scale. The caller hands in the shape to read, since the source knows
    /// how to fetch a file and nothing about what is in one.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPack">What the file holds.</typeparam>
    /// <param name="domain">Which file, such as <c>leader-traits</c>.</param>
    /// <param name="shape">How to read it.</param>
    /// <param name="cancellationToken">Abandons the fetch.</param>
    /// <returns>The pack, or null where it is absent or was built to another shape.</returns>
    Task<TPack?> LoadWikiPackAsync<TPack>(
        string domain,
        JsonTypeInfo<TPack> shape,
        CancellationToken cancellationToken = default)
        where TPack : class, IWikiPack;
}

/// <summary>
/// Fetches extracted data over HTTP.
/// </summary>
/// <remarks>
/// Serves both hosts. The web app fetches from where the site is published; the desktop app maps
/// its local cache to a hostname the embedded browser can reach, so neither needs its own loader.
/// </remarks>
public sealed class HttpGameDataSource(HttpClient client, string baseUrl = "gamedata")
    : IGameDataSource, IDisposable
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly string _baseUrl = baseUrl.TrimEnd('/');
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _wardrobeGate = new(1, 1);

    /// <summary>
    /// The wiki's files, by domain, held as the fetch rather than its result.
    /// </summary>
    /// <remarks>
    /// The task and not the answer, so that two pages asking at once share one request: written as
    /// a null check and then an assignment, both would find nothing and both would fetch. The lock
    /// is only around the table, which is never held across the await.
    /// </remarks>
    private readonly Dictionary<string, Task<object?>> _packs = new(StringComparer.Ordinal);

    private GameData? _loaded;
    private IReadOnlyList<PortraitOutfit>? _wardrobe;

    /// <inheritdoc />
    public async Task<GameData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded is not null)
        {
            return _loaded;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Checked again inside the gate: several components ask for this at once when the
            // designer first opens, and fetching it more than once would be wasteful.
            if (_loaded is not null)
            {
                return _loaded;
            }

            var database = await ReadAsync(
                "gamedb.json", GameDataJsonContext.Default.GameDatabase, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The game database could not be read.");

            // Refused rather than read around. A database of an older shape deserialises without
            // complaint and every field added since takes its default, so the designer would run on
            // rules it silently does not have. The desktop has always checked; this did not.
            if (database.SchemaVersion != GameDatabase.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"The game data was built for a different version of this app " +
                    $"(found {database.SchemaVersion}, expected {GameDatabase.CurrentSchemaVersion}).");
            }

            var localisation = await ReadAsync(
                "loc/en.json", GameDataJsonContext.Default.DictionaryStringString, cancellationToken)
                .ConfigureAwait(false) ?? [];

            _loaded = new GameData(database, localisation, $"{_baseUrl}/assets");
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

            // A host that did not publish one is not a fault: the desktop builds its cache without
            // the wardrobe, and the section that wants it says so rather than failing.
            _wardrobe = await ReadAsync(
                "wardrobe.json", GameDataJsonContext.Default.IReadOnlyListPortraitOutfit, cancellationToken)
                .ConfigureAwait(false) ?? [];

            return _wardrobe;
        }
        catch (HttpRequestException)
        {
            _wardrobe = [];
            return _wardrobe;
        }
        finally
        {
            _wardrobeGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<TPack?> LoadWikiPackAsync<TPack>(
        string domain,
        JsonTypeInfo<TPack> shape,
        CancellationToken cancellationToken = default)
        where TPack : class, IWikiPack
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(shape);

        Task<object?> fetch;

        lock (_packs)
        {
            if (!_packs.TryGetValue(domain, out var held))
            {
                held = Fetch();
                _packs[domain] = held;
            }

            fetch = held;
        }

        return Cast(fetch);

        async Task<object?> Fetch()
        {
            try
            {
                var pack = await ReadAsync($"wiki/{domain}.json", shape, cancellationToken)
                    .ConfigureAwait(false);

                // Refused rather than read around, the way a stale database is above: a pack of an
                // older shape deserialises without complaint and every field added since takes its
                // default, so the page would draw rules it silently does not have.
                return pack is null || pack.Stamp.SchemaVersion != TPack.ExpectedSchemaVersion
                    ? null
                    : pack;
            }
            catch (HttpRequestException)
            {
                // A host that did not publish one is not a fault, and neither is being offline. The
                // page that wants it says so rather than failing.
                return null;
            }
        }

        static async Task<TPack?> Cast(Task<object?> fetch) =>
            await fetch.ConfigureAwait(false) as TPack;
    }

    /// <summary>
    /// Fetches a file and reads it as JSON.
    /// </summary>
    /// <remarks>
    /// The whole response is taken as bytes before anything is parsed, rather than deserialised
    /// from the stream. In a browser the response stream goes through a bridge into JavaScript, and
    /// pulling a two-megabyte database through it a chunk at a time stalls indefinitely. Taking the
    /// bytes in one go and reading them in memory turns that into a fraction of a second.
    /// </remarks>
    private async Task<T?> ReadAsync<T>(
        string path,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        var url = _baseUrl.Length == 0 ? path : $"{_baseUrl}/{path}";
        var bytes = await _client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(bytes, typeInfo);
    }

    /// <summary>Releases the two gates that keep a concurrent load from fetching twice.</summary>
    /// <remarks>
    /// Registered as a scoped service, so one is built per page in the browser and per window on the
    /// desktop. A SemaphoreSlim holds a wait handle once anyone has waited on it, and nothing was
    /// ever handing those back.
    /// </remarks>
    public void Dispose()
    {
        _gate.Dispose();
        _wardrobeGate.Dispose();
    }
}
