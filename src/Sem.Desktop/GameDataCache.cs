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

    /// <summary>Points the cache at the installation it is to be built from.</summary>
    /// <param name="installRoot">Where the game is installed.</param>
    /// <param name="cacheRoot">
    /// Where to keep what is extracted, for a caller that needs to say.
    /// </param>
    /// <remarks>
    /// The root is a parameter for one reason: <see cref="IsUsable"/> decides whether a launch
    /// re-reads thirty-five thousand files or opens in a second, and answers through a sentence
    /// nobody could assert while the only answer was the machine's real application-data folder. A
    /// test that had to write there to arrange a case would be writing into the cache the developer
    /// actually uses. Left alone it is the real one, which is every caller in the app.
    /// </remarks>
    public GameDataCache(string installRoot, string? cacheRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

        _installRoot = PathNormalizer.Normalize(installRoot);

        Directory = Path.Combine(
            cacheRoot ?? WritePolicy.LocalCacheRoot(), "cache", KeyFor(_installRoot));
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
                reason = "built to an older data format";
                return false;
            }

            // And the same question the other way round: the shape can be unchanged while what was
            // extracted into it is not. Every database records the build that made it; nothing read
            // it, so a desktop that had extracted once kept that data through every later change to
            // what extraction produces - new emblems, new renders, new text - and reported itself
            // perfectly up to date.
            if (!root.TryGetProperty("extractorVersion", out var built) ||
                !string.Equals(built.GetString(), GameDataExtractor.ExtractorVersion, StringComparison.Ordinal))
            {
                reason = "built by an older version of this app";
                return false;
            }

            // And the wardrobe beside it, which older caches were built without. Checked as a file
            // rather than believed from the version, because a cache that predates the drawing is
            // otherwise perfectly valid by every other test and would never be rebuilt.
            if (!File.Exists(Path.Combine(Directory, GameDataWriter.WardrobeFileName)))
            {
                reason = "built before the ruler's wardrobe was drawn";
                return false;
            }

            // And the wiki's own files, for exactly the same reason: a cache from before a domain
            // existed passes every other test and would never be rebuilt, leaving the page about it
            // permanently empty on this machine with nothing anywhere to say why.
            if (!File.Exists(Path.Combine(
                    Directory, GameDataWriter.WikiPackFileName(LeaderTraitPack.Domain))) ||
                !File.Exists(Path.Combine(
                    Directory, GameDataWriter.WikiPackFileName(ShipsetPack.Domain))) ||
                !File.Exists(Path.Combine(
                    Directory, GameDataWriter.WikiPackFileName(PersonalityPack.Domain))))
            {
                reason = "built before the wiki had its own data";
                return false;
            }

            // A game patch changes what the designer must offer, so the data is rebuilt with it.
            // A cache from before this was written down cannot say, and is old enough to rebuild.
            var installed = ReadInstalledVersion();
            if (!root.TryGetProperty("gameVersion", out var cached))
            {
                reason = "built before the game version was written down";
                return false;
            }

            if (installed is not null &&
                !string.Equals(cached.GetString(), installed, StringComparison.Ordinal))
            {
                reason = $"the game was updated to {installed}";
                return false;
            }

            // And the files themselves, which move for things a version string never mentions: a
            // hotfix that did not bump it, a repaired install, a hand-edited define. Absent means
            // a cache from before this was recorded, which is not a reason to throw it away - the
            // version above has been agreeing with it all along, and it will be written on the
            // next rebuild whenever one is wanted for some other reason.
            if (root.TryGetProperty("installFingerprint", out var printed) &&
                printed.GetString() is { Length: > 0 } print &&
                InstallFingerprint.Of(_installRoot) is { Length: > 0 } now &&
                !string.Equals(print, now, StringComparison.Ordinal))
            {
                reason = "the game files changed";
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

    /// <summary>
    /// Reads the installation and fills the cache, wardrobe and all.
    /// </summary>
    /// <remarks>
    /// The wardrobe is what dresses the ruler. Without it the appearance panel and the lite card
    /// both fall back to a flat thumbnail, which is what this host did for as long as the drawing
    /// lived in the command-line tool. It is the bulk of the run, and it is the reason a first
    /// launch takes as long as it does.
    /// </remarks>
    public void Rebuild(SafeFile file, IProgress<string>? progress = null) =>
        GameDataWriter.Write(_installRoot, Directory, file, progress, wardrobe: true);

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
