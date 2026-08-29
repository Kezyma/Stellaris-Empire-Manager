using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;

namespace Sem.Desktop;

/// <summary>
/// Keeps an extracted copy of a Stellaris installation, so the game files are read once rather
/// than every time the app opens.
/// </summary>
/// <remarks>
/// Reading an installation takes a few seconds and produces some fifteen megabytes of images.
/// Doing that on every launch would be a poor greeting, so the result is cached against the
/// installation it came from and rebuilt when the game is patched.
/// </remarks>
public sealed class GameDataCache
{
    private readonly string _installRoot;

    public GameDataCache(string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

        _installRoot = PathNormalizer.Normalize(installRoot);
        Directory = Path.Combine(WritePolicy.LocalCacheRoot(), "cache", KeyFor(_installRoot));
    }

    /// <summary>Where this installation's extracted data is kept.</summary>
    public string Directory { get; }

    /// <summary>The game database file within the cache.</summary>
    public string DatabasePath => Path.Combine(Directory, "gamedb.json");

    /// <summary>
    /// Whether the cache holds data this build can use for the game as it is now installed.
    /// </summary>
    public bool IsUsable(out string? reason)
    {
        if (!File.Exists(DatabasePath))
        {
            reason = "not extracted yet";
            return false;
        }

        try
        {
            using var stream = SafeFile.OpenRead(DatabasePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            // Property names follow the generated serialiser, which writes them in camel case.
            if (!root.TryGetProperty("schemaVersion", out var schema) ||
                schema.GetInt32() != GameDataExtractor.SchemaVersion)
            {
                reason = "built by an older version of this app";
                return false;
            }

            // A game patch changes what the designer must offer, so the data is rebuilt with it.
            var installed = ReadInstalledVersion();
            if (root.TryGetProperty("gameVersion", out var cached) &&
                installed is not null &&
                !string.Equals(cached.GetString(), installed, StringComparison.Ordinal))
            {
                reason = $"the game was updated to {installed}";
                return false;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            reason = "unreadable";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Reads the installation and fills the cache.</summary>
    public void Rebuild(SafeFile file, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        var content = LayeredContent.ForInstall(_installRoot);
        var extractor = new GameDataExtractor(content);

        var database = extractor.Extract(progress);
        file.WriteAllBytes(DatabasePath, JsonSerializer.SerializeToUtf8Bytes(database, GameDataJsonContext.Default.GameDatabase));

        progress?.Report("Reading text");
        var localisation = extractor.ExtractLocalisation(reachableFrom: database);
        file.WriteAllBytes(
            Path.Combine(Directory, "loc", "en.json"),
            JsonSerializer.SerializeToUtf8Bytes(localisation, GameDataJsonContext.Default.DictionaryStringString));

        new AssetBaker(content, file).Bake(extractor.Assets, Path.Combine(Directory, "assets"), progress);
    }

    private string? ReadInstalledVersion()
    {
        var settings = Path.Combine(_installRoot, "launcher-settings.json");
        if (!File.Exists(settings))
        {
            return null;
        }

        try
        {
            using var stream = SafeFile.OpenRead(settings);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("rawVersion", out var version) ? version.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// A short, stable folder name for an installation path, so two installations do not share a
    /// cache and the path itself stays out of the folder name.
    /// </summary>
    private static string KeyFor(string installRoot)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(installRoot.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
