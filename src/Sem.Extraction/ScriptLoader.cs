using System.Globalization;
using Sem.Clausewitz;

namespace Sem.Extraction;

/// <summary>
/// Reads and parses the game's script files, caching each one and resolving the <c>@</c> variables
/// they share.
/// </summary>
/// <remarks>
/// Game content is parsed leniently: vanilla ships a file with an unclosed block, and one defect
/// in Paradox's data must not stop extraction. Files that fail outright are recorded and skipped
/// rather than thrown, for the same reason.
/// </remarks>
public sealed class ScriptLoader(LayeredContent content)
{
    private readonly Dictionary<string, CwDocument?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _failures = [];

    /// <summary>The content layers being read.</summary>
    public LayeredContent Content { get; } = content;

    /// <summary>Global <c>@</c> variables, from <c>common/scripted_variables</c>.</summary>
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    /// <summary>Files that could not be parsed, with the reason.</summary>
    public IReadOnlyList<string> Failures => _failures;

    /// <summary>
    /// Records a file that would not parse, for a reader that does its own parsing.
    /// </summary>
    /// <remarks>
    /// The prescripted-countries files are read through their own loader rather than through
    /// <see cref="Load"/>, and were dropping a whole file on a syntax error without telling anyone -
    /// so a patch that changed that syntax would quietly remove every built-in empire in it while
    /// the extract command still reported success. This is where the rest of the failures collect.
    /// </remarks>
    public void RecordFailure(string relativePath, string reason) =>
        _failures.Add($"{relativePath}: {reason}");

    /// <summary>Parses one file, or returns null when it is missing or unparseable.</summary>
    public CwDocument? Load(string relativePath)
    {
        if (_cache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        CwDocument? document = null;

        if (Content.Contains(relativePath))
        {
            try
            {
                document = CwDocument.Parse(Content.Read(relativePath), CwParseOptions.Lenient);

                // Files declare @ variables at the top for their own use, and the game treats them
                // globally, so collecting on load makes them available wherever they are referenced.
                CollectVariables(document);
            }
            catch (Exception ex) when (ex is CwSyntaxException or IOException)
            {
                _failures.Add($"{relativePath}: {ex.Message}");
            }
        }

        _cache[relativePath] = document;
        return document;
    }

    /// <summary>Parses every script file in a directory, in load order.</summary>
    public IEnumerable<(string Path, CwDocument Document)> LoadDirectory(
        string relativeDirectory,
        bool recursive = false)
    {
        foreach (var path in Content.EnumerateFiles(relativeDirectory, "*.txt", recursive))
        {
            if (Load(path) is { } document)
            {
                yield return (path, document);
            }
        }
    }

    /// <summary>
    /// The top-level entries of every file in a directory, in load order. Later entries with the
    /// same key override earlier ones, as the game resolves them.
    /// </summary>
    public IEnumerable<ScriptEntry> LoadEntries(string relativeDirectory, bool recursive = false)
    {
        foreach (var (path, document) in LoadDirectory(relativeDirectory, recursive))
        {
            var order = 0;
            foreach (var node in document.Nodes)
            {
                if (node.IsAssignment && node.Key is { Length: > 0 } key && !key.StartsWith('@'))
                {
                    yield return new ScriptEntry(key, node, path, order++);
                }
            }
        }
    }

    /// <summary>
    /// The definitions in a directory with overrides applied, as the game resolves them: a key
    /// defined more than once keeps the last definition, in the position of the first.
    /// </summary>
    /// <remarks>
    /// Content packs redefine keys the base game already declares, so reading every entry without
    /// resolving overrides produces duplicates and, worse, keeps the superseded version.
    /// </remarks>
    public IReadOnlyList<ScriptEntry> LoadDefinitions(string relativeDirectory, bool recursive = false)
    {
        var order = new List<string>();
        var latest = new Dictionary<string, ScriptEntry>(StringComparer.Ordinal);

        foreach (var entry in LoadEntries(relativeDirectory, recursive))
        {
            if (!latest.ContainsKey(entry.Key))
            {
                order.Add(entry.Key);
            }

            latest[entry.Key] = entry;
        }

        return [.. order.Select(key => latest[key])];
    }

    /// <summary>
    /// Loads the shared <c>@</c> variables. Must run before anything that reads numbers, since a
    /// weight or cost is often written as a variable rather than a literal.
    /// </summary>
    public void LoadVariables()
    {
        foreach (var (_, document) in LoadDirectory("common/scripted_variables"))
        {
            CollectVariables(document);
        }
    }

    /// <summary>Records the <c>@</c> variables a single file declares for its own use.</summary>
    public void CollectVariables(CwDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var node in document.Nodes)
        {
            if (node.Key is { Length: > 1 } key && key.StartsWith('@') && node.ScalarValue is { } value)
            {
                Variables[key] = value;
            }
        }
    }

    /// <summary>
    /// Reads a number, following an <c>@</c> variable when the value is one. Returns null when the
    /// value is missing or is not a number this can resolve.
    /// </summary>
    public double? ResolveNumber(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var resolved = value;

        // A variable can point at another variable; a few steps is plenty and stops any cycle.
        for (var depth = 0; depth < 8 && resolved.StartsWith('@'); depth++)
        {
            if (!Variables.TryGetValue(resolved, out var next))
            {
                return null;
            }

            resolved = next;
        }

        return double.TryParse(resolved, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    /// <summary>Reads a whole number, following <c>@</c> variables.</summary>
    public int? ResolveInt(string? value) =>
        ResolveNumber(value) is { } number ? (int)Math.Round(number) : null;
}

/// <summary>One top-level definition from a script file.</summary>
/// <param name="Key">The definition's key.</param>
/// <param name="Node">Its node, so the whole body is available.</param>
/// <param name="Path">The file it came from, relative to the content root.</param>
/// <param name="Order">Its position within that file, which decides ties between equal weights.</param>
public sealed record ScriptEntry(string Key, CwNode Node, string Path, int Order)
{
    /// <summary>The definition's body.</summary>
    public CwBlock Body => Node.Block ?? new CwBlock();
}
