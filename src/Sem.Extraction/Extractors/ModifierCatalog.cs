using Sem.Clausewitz;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads what the game knows about how a modifier should be displayed.
/// </summary>
/// <remarks>
/// <para>
/// Whether a modifier is a proportion or a flat amount is not something its name or its label
/// reveals. The clearest proof is naval capacity, where the flat and proportional forms share a
/// single label string — one renders "+4" and the other "+4%" from identical text — so the
/// distinction cannot be coming from localisation.
/// </para>
/// <para>
/// Three sources, in descending order of authority. Around 130 modifiers state their own settings in
/// <c>common/scripted_modifiers</c>. The rest are built into the game, and their table can only be
/// obtained by running it once in debug mode, which writes a log this will read when it is there.
/// Failing both, the name is guessed from — and the number of guesses is reported, so the
/// inaccuracy is visible rather than silent.
/// </para>
/// </remarks>
public sealed class ModifierCatalog
{
    private readonly Dictionary<string, ModifierInfo> _known;

    private ModifierCatalog(Dictionary<string, ModifierInfo> known, string? logPath)
    {
        _known = known;
        SourceLog = logPath;
    }

    /// <summary>An empty catalogue, for callers with no installation to read.</summary>
    public static ModifierCatalog Empty { get; } =
        new(new Dictionary<string, ModifierInfo>(StringComparer.OrdinalIgnoreCase), null);

    /// <summary>The debug log this read, when one was available.</summary>
    public string? SourceLog { get; }

    /// <summary>How many modifiers stated their own display settings.</summary>
    public int Count => _known.Count;

    /// <summary>
    /// Where a modifier's log would be, if the game has been run in debug mode to produce one.
    /// </summary>
    public static string? LogPath(string? installRoot = null) =>
        StellarisLocator.FindUserDataRoot(installRoot) is { } userData
            ? Path.Combine(userData, "logs", "script_documentation", "modifiers.log")
            : null;

    /// <summary>Reads the settings an installation declares.</summary>
    public static ModifierCatalog Read(LayeredContent content, string? installRoot = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var known = new Dictionary<string, ModifierInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in content.EnumerateFiles("common/scripted_modifiers"))
        {
            CwDocument document;

            try
            {
                document = CwDocument.Parse(content.Read(path), CwParseOptions.Lenient);
            }
            catch (Exception ex) when (ex is CwSyntaxException or IOException)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                if (node.Key is { Length: > 0 } key && node.Block is { } body)
                {
                    known[key] = new ModifierInfo(
                        IsPercentage: body.GetBool("percentage"),
                        IsGood: body.GetBool("good"),
                        IsNeutral: body.GetBool("neutral"),
                        Decimals: int.TryParse(body.GetString("max_decimals"), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 2,
                        Declared: true);
                }
            }
        }

        var logPath = LogPath(installRoot);

        if (logPath is not null && File.Exists(logPath))
        {
            ReadLog(logPath, known);
            return new ModifierCatalog(known, logPath);
        }

        return new ModifierCatalog(known, null);
    }

    /// <summary>
    /// How a modifier should be displayed, inferring it when the game does not say.
    /// </summary>
    /// <param name="key">The modifier's name.</param>
    /// <param name="observedValues">
    /// The values the game's own script gives this modifier. These settle most of the cases a name
    /// alone cannot, because the two kinds are written differently: a proportion is a fraction of
    /// one, while a flat amount is a whole number of whatever it counts.
    /// </param>
    public ModifierInfo Describe(string key, IEnumerable<double>? observedValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_known.TryGetValue(key, out var known))
        {
            return known;
        }

        return new ModifierInfo(
            IsPercentage: InferPercentage(key, observedValues),
            IsGood: !LooksLikeACost(key),
            IsNeutral: false,
            Decimals: 2,
            Declared: false);
    }

    /// <summary>
    /// Works out whether a modifier is a proportion.
    /// </summary>
    /// <remarks>
    /// The suffixes are decisive where they appear, and about two thirds of modifiers carry one.
    /// For the rest the values decide: <c>faction_approval = 0.10</c> is ten percent while
    /// <c>country_leader_pool_size = 1</c> is one extra leader, and no rule based on the name can
    /// tell those apart — which is why the suffix alone gets both of them wrong.
    /// </remarks>
    private static bool InferPercentage(string key, IEnumerable<double>? observedValues)
    {
        if (key.EndsWith("_mult", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (key.EndsWith("_add", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = observedValues?.Where(v => v != 0).ToList();

        return values is { Count: > 0 } && values.All(v => v != Math.Truncate(v));
    }

    /// <summary>
    /// Whether a lower number is the better one, which is true of anything an empire pays.
    /// </summary>
    private static bool LooksLikeACost(string key) =>
        key.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("upkeep", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("_time", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the table the game writes when run in debug mode.
    /// </summary>
    /// <remarks>
    /// The log lists every modifier the engine knows with the settings it uses, which is the only
    /// complete answer. Entries already declared in script are left alone, since those are stated
    /// rather than reported.
    /// </remarks>
    private static void ReadLog(string path, Dictionary<string, ModifierInfo> known)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(';', StringSplitOptions.TrimEntries);

                if (parts.Length < 2 || parts[0] is not { Length: > 0 } key || known.ContainsKey(key))
                {
                    continue;
                }

                var percentage = parts.Any(p =>
                    p.Equals("percentage", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("percentage=yes", StringComparison.OrdinalIgnoreCase));

                var good = parts.Any(p =>
                    p.Equals("good", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("good=yes", StringComparison.OrdinalIgnoreCase));

                known[key] = new ModifierInfo(percentage, good, IsNeutral: false, Decimals: 2, Declared: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log is a convenience. Without it the guesses stand, and they are reported.
        }
    }
}
