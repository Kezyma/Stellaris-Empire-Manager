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
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

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
    ///
    /// Said once however often it happens. An inline script is resolved at each of its call sites
    /// rather than cached like a parsed file, so one missing include filed the same sentence four
    /// times - and a reader counting lines would have read four defects into one.
    /// </remarks>
    public void RecordFailure(string relativePath, string reason)
    {
        var failure = $"{relativePath}: {reason}";

        if (_reported.Add(failure))
        {
            _failures.Add(failure);
        }
    }

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
                // Through the same door as everything else. This one cannot repeat itself - a file
                // is parsed once and the result cached, failure included - but there being two ways
                // to record a failure is how one of them ends up without the other's rules.
                RecordFailure(relativePath, ex.Message);
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
                    if (node.Block is { } body)
                    {
                        Inline(body, 0);
                    }

                    yield return new ScriptEntry(key, node, path, order++);
                }
            }
        }
    }

    /// <summary>
    /// Writes the game's shared script fragments into the definitions that call for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>inline_script</c> is an include with parameters: the caller names a file under
    /// <c>common/inline_scripts</c> and supplies values for the <c>$NAME$</c> blanks in it, and the
    /// game reads the result as though it had been written in place. Read without expanding, a
    /// definition is missing whatever the fragment was carrying - and what it carries is not always
    /// decoration. Nine species traits keep their <c>hidden = yes</c> and <c>initial = no</c> in
    /// one, so all nine were being offered in a picker the game does not show them in; nineteen
    /// traditions keep their hive and machine renamings in another, so gestalt empires read the
    /// wording written for somebody else.
    /// </para>
    /// <para>
    /// The icon scripts are left where they are. Those are the ones carrying an <c>ICON</c>
    /// argument, and they describe stacked layers rather than fields of the definition -
    /// <see cref="Extractors.TraitIconComposer"/> walks them itself and needs the call rather than
    /// its contents. Splicing them in would put loose <c>layer</c> blocks into a trait and take the
    /// call away from the one thing that reads it.
    /// </para>
    /// </remarks>
    private void Inline(CwBlock body, int depth)
    {
        if (depth >= MaxInlineDepth)
        {
            return;
        }

        for (var at = 0; at < body.Nodes.Count; at++)
        {
            var node = body.Nodes[at];

            if (node.Key != "inline_script")
            {
                if (node.Block is { } nested)
                {
                    Inline(nested, depth + 1);
                }

                continue;
            }

            // Either a bare path, or a block naming the script and answering its blanks.
            var arguments = node.Block;

            if (arguments?.GetString("ICON") is { Length: > 0 })
            {
                continue;
            }

            var script = arguments?.GetString("script") ?? node.ScalarValue;

            if (script is not { Length: > 0 } || Fragment(script, arguments) is not { } fragment)
            {
                continue;
            }

            body.RemoveAt(at);

            var written = 0;

            foreach (var inner in fragment.Nodes)
            {
                body.Insert(at + written, inner);
                written++;
            }

            // Back over what was just written, so a fragment that calls another is read too, and
            // so the loop does not step past the first node of what it inserted.
            at--;
        }
    }

    /// <summary>Reads one fragment with its blanks filled in, or nothing where there is no such file.</summary>
    private CwDocument? Fragment(string script, CwBlock? arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var argument in arguments?.Nodes ?? [])
        {
            if (argument.Key is { Length: > 0 } name && name != "script" &&
                argument.ScalarValue is { } value)
            {
                values[name] = value;
            }
        }

        var path = $"common/inline_scripts/{Fill(script, values)}.txt";

        if (!Content.Contains(path))
        {
            RecordFailure(path, "an inline script that is not in the installation");
            return null;
        }

        try
        {
            var text = Fill(System.Text.Encoding.UTF8.GetString(Content.Read(path)), values);

            return CwDocument.Parse(System.Text.Encoding.UTF8.GetBytes(text), CwParseOptions.Lenient);
        }
        catch (Exception ex) when (ex is CwSyntaxException or IOException)
        {
            RecordFailure(path, ex.Message);
            return null;
        }
    }

    /// <summary>Fills in every <c>$NAME$</c> an answer was given for.</summary>
    private static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0 || !text.Contains('$', StringComparison.Ordinal))
        {
            return text;
        }

        foreach (var (name, value) in values)
        {
            text = text.Replace($"${name}$", value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>How far one fragment may call another before this stops following.</summary>
    private const int MaxInlineDepth = 8;

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
