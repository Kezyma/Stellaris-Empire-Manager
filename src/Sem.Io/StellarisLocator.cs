using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sem.Io;

/// <summary>
/// Read-only discovery of a Stellaris installation and the player's game data folder.
/// </summary>
/// <remarks>
/// Nothing here writes. The game data folder is resolved through the installation's
/// <c>launcher-settings.json</c> rather than assumed, because Documents is frequently
/// redirected to OneDrive and a hardcoded path would silently miss the real files.
/// </remarks>
public static partial class StellarisLocator
{
    private const string SteamAppFolder = "steamapps/common/Stellaris";

    /// <summary>Locates the Stellaris installation, or returns null when it cannot be found.</summary>
    public static string? FindInstallRoot()
    {
        foreach (var library in EnumerateSteamLibraries())
        {
            var candidate = Path.Combine(library, SteamAppFolder.Replace('/', Path.DirectorySeparatorChar));
            if (IsInstallRoot(candidate))
            {
                return PathNormalizer.Normalize(candidate);
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="path"/> looks like a Stellaris installation.</summary>
    public static bool IsInstallRoot(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        File.Exists(Path.Combine(path, "launcher-settings.json")) &&
        Directory.Exists(Path.Combine(path, "common"));

    /// <summary>
    /// Locates the player's Stellaris data folder (saves, settings, empire designs), preferring
    /// the <c>gameDataPath</c> declared by the installation.
    /// </summary>
    public static string? FindUserDataRoot(string? installRoot = null)
    {
        installRoot ??= FindInstallRoot();

        if (installRoot is not null)
        {
            var declared = ReadDeclaredGameDataPath(installRoot);
            if (declared is not null && Directory.Exists(declared))
            {
                return PathNormalizer.Normalize(declared);
            }
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            "Stellaris");

        return Directory.Exists(fallback) ? PathNormalizer.Normalize(fallback) : null;
    }

    /// <summary>
    /// Reads the installation's declared game data path and expands the launcher's placeholders.
    /// </summary>
    public static string? ReadDeclaredGameDataPath(string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

        var settingsPath = Path.Combine(installRoot, "launcher-settings.json");
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        string? declared;
        try
        {
            using var stream = SafeFile.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            declared = document.RootElement.TryGetProperty("gameDataPath", out var value)
                ? value.GetString()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(declared))
        {
            return null;
        }

        var expanded = declared
            .Replace("%USER_DOCUMENTS%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
            .Replace("%USER_HOME%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)
            .Replace('/', Path.DirectorySeparatorChar);

        return Environment.ExpandEnvironmentVariables(expanded);
    }

    /// <summary>Enumerates Steam library roots declared in <c>libraryfolders.vdf</c>.</summary>
    public static IReadOnlyList<string> EnumerateSteamLibraries()
    {
        var libraries = new List<string>();

        foreach (var steamRoot in EnumerateSteamRoots())
        {
            AddDistinct(libraries, steamRoot);

            var manifest = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(manifest);
                foreach (Match match in VdfPathEntry().Matches(text))
                {
                    // VDF escapes backslashes; unescape before use.
                    AddDistinct(libraries, match.Groups[1].Value.Replace(@"\\", @"\", StringComparison.Ordinal));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An unreadable library manifest just means fewer candidates.
            }
        }

        return libraries;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        // Lets a caller point at a non-default Steam install without code changes.
        var configured = Environment.GetEnvironmentVariable("STEAM_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return configured;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var variable in (string[])["ProgramFiles(x86)", "ProgramFiles"])
            {
                var programFiles = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    yield return Path.Combine(programFiles, "Steam");
                }
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
    }

    private static void AddDistinct(List<string> libraries, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
        {
            return;
        }

        var normalized = PathNormalizer.Normalize(candidate);
        if (!libraries.Contains(normalized, StringComparer.FromComparison(PathNormalizer.Comparison)))
        {
            libraries.Add(normalized);
        }
    }

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathEntry();
}
