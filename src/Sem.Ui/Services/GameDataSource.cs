using System.Net.Http.Json;
using System.Text.Json;
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
}

/// <summary>
/// Fetches extracted data over HTTP.
/// </summary>
/// <remarks>
/// Serves both hosts. The web app fetches from where the site is published; the desktop app maps
/// its local cache to a hostname the embedded browser can reach, so neither needs its own loader.
/// </remarks>
public sealed class HttpGameDataSource(HttpClient client, string baseUrl = "gamedata") : IGameDataSource
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly string _baseUrl = baseUrl.TrimEnd('/');
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GameData? _loaded;

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

            var database = await _client.GetFromJsonAsync<GameDatabase>(
                $"{_baseUrl}/gamedb.json", Options, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The game database could not be read.");

            var localisation = await _client.GetFromJsonAsync<Dictionary<string, string>>(
                $"{_baseUrl}/loc/en.json", Options, cancellationToken).ConfigureAwait(false)
                ?? [];

            _loaded = new GameData(database, localisation, $"{_baseUrl}/assets");
            return _loaded;
        }
        finally
        {
            _gate.Release();
        }
    }
}
