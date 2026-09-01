using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// Typed access to a <see cref="CwBlock"/> that edits the block in place rather than replacing it.
/// </summary>
/// <remarks>
/// <para>
/// The model is a view over the parsed tree, not a copy of it. Reading never modifies anything, so
/// loading and saving a file the user did not touch reproduces it exactly; writing changes only the
/// nodes involved, so changing one trait changes one line. Fields this project does not know about
/// simply stay in the tree, which is what stops a future game patch, or a mod, from costing
/// someone their empires.
/// </para>
/// <para>
/// New fields are inserted in the order the game writes them, so a design edited here is
/// indistinguishable from one the game saved.
/// </para>
/// </remarks>
public abstract class CwView
{
    private readonly IReadOnlyList<string> _fieldOrder;

    /// <summary>The block this view holds, once it is known to exist.</summary>
    private CwBlock? _block;

    /// <summary>Where to find or make the block, for a view that was given a place rather than one.</summary>
    private readonly CwView? _parent;
    private readonly string? _key;

    protected CwView(CwBlock block, IReadOnlyList<string>? fieldOrder = null)
    {
        ArgumentNullException.ThrowIfNull(block);

        _block = block;
        _fieldOrder = fieldOrder ?? [];
    }

    /// <summary>
    /// A view over one of another block's fields, which is not created until something is written.
    /// </summary>
    /// <remarks>
    /// An empire missing a field the model knows about — an older file, a mod's, a hand-edited one —
    /// used to gain an empty block from being looked at, because the property that reads it made it
    /// on the way. Merely listing the empires wrote into every design that lacked one, and the
    /// promise that a file nobody touched comes back byte for byte was broken by reading it.
    /// </remarks>
    /// <param name="parent">The block this one sits inside.</param>
    /// <param name="key">The field it is stored under.</param>
    /// <param name="fieldOrder">The order the game writes this block's fields in.</param>
    protected CwView(CwView parent, string key, IReadOnlyList<string>? fieldOrder = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(key);

        _parent = parent;
        _key = key;
        _fieldOrder = fieldOrder ?? [];
    }

    /// <summary>
    /// The underlying block, made now if the design has not got one. Exposed so callers can reach
    /// fields the model does not model, which is something only a writer needs.
    /// </summary>
    public CwBlock Block => _block ??= _parent!.GetOrAddBlock(_key!);

    /// <summary>
    /// The block as the design holds it, or null when the design has not got one. Every read goes
    /// through this, so that reading writes nothing.
    /// </summary>
    private CwBlock? Existing => _block ??= _parent?.GetBlock(_key!);

    /// <summary>
    /// Whether two views stand on the same block.
    /// </summary>
    /// <remarks>
    /// The way to tell one view from another, since a view is built fresh every time the property
    /// holding it is read and two of them are never the same object. Asking creates neither side:
    /// this is a question, and <see cref="Block"/> would answer it by making the block it was asked
    /// about. A view whose block does not exist yet matches nothing, including another absent one -
    /// there is no block for them to share.
    /// </remarks>
    public bool SameAs(CwView? other) =>
        other is not null && Existing is { } block && ReferenceEquals(block, other.Existing);

    /// <summary>The first node with this key, or null.</summary>
    protected CwNode? FindNode(string key) =>
        Existing?.Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal));

    /// <summary>The first value for this key, unquoted, or null when absent.</summary>
    protected string? GetString(string key) => FindNode(key)?.ScalarValue;

    /// <summary>Every value for a repeated key, in source order.</summary>
    protected IReadOnlyList<string> GetStrings(string key) =>
    [
        .. (Existing?.Nodes ?? [])
            .Where(n => string.Equals(n.Key, key, StringComparison.Ordinal) && n.Scalar is not null)
            .Select(n => n.ScalarValue!)
    ];

    /// <summary>The first block value for this key, or null.</summary>
    protected CwBlock? GetBlock(string key) => FindNode(key)?.Block;

    /// <summary>Reads a <c>yes</c>/<c>no</c> field.</summary>
    protected bool? GetBool(string key) => GetString(key) switch
    {
        "yes" => true,
        "no" => false,
        _ => null,
    };

    /// <summary>Reads a whole-number field.</summary>
    protected int? GetInt(string key) =>
        int.TryParse(GetString(key), System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>
    /// Sets a field, or removes it when <paramref name="value"/> is null. An existing field keeps
    /// its original quoting style so edits do not churn unrelated formatting.
    /// </summary>
    protected void SetString(string key, string? value, bool quoted = true)
    {
        if (value is null)
        {
            RemoveAll(key);
            return;
        }

        var existing = FindNode(key);
        if (existing is not null)
        {
            var keepQuoted = existing.Scalar?.IsQuoted ?? quoted;
            existing.Value = keepQuoted ? CwScalar.Quoted(value) : CwScalar.Bare(value);
            return;
        }

        InsertInCanonicalOrder(CwNode.Assignment(key, quoted ? CwScalar.Quoted(value) : CwScalar.Bare(value)));
    }

    /// <summary>Sets a <c>yes</c>/<c>no</c> field, or removes it when null. Never quoted.</summary>
    protected void SetBool(string key, bool? value) =>
        SetString(key, value switch { true => "yes", false => "no", null => null }, quoted: false);

    /// <summary>Sets a whole-number field, or removes it when null. Never quoted.</summary>
    protected void SetInt(string key, int? value) =>
        SetString(key, value?.ToString(System.Globalization.CultureInfo.InvariantCulture), quoted: false);

    /// <summary>
    /// Replaces every occurrence of a repeated key. Existing entries are reused in place so that
    /// adding one trait to a species rewrites one line rather than all of them.
    /// </summary>
    protected void SetStrings(string key, IReadOnlyList<string> values, bool quoted = true)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Writing none of something into a block that is not there is not a write. Every loop below
        // would run over nothing, but reaching them through Block would have made the block first,
        // which is the same trap RemoveAll had: an empty list of traits added an empty species.
        if (values.Count == 0 && Existing is null)
        {
            return;
        }

        var existing = Block.Nodes
            .Where(n => string.Equals(n.Key, key, StringComparison.Ordinal))
            .ToList();

        for (var i = 0; i < Math.Min(existing.Count, values.Count); i++)
        {
            var keepQuoted = existing[i].Scalar?.IsQuoted ?? quoted;
            existing[i].Value = keepQuoted ? CwScalar.Quoted(values[i]) : CwScalar.Bare(values[i]);
        }

        for (var i = values.Count; i < existing.Count; i++)
        {
            Block.Remove(existing[i]);
        }

        if (values.Count <= existing.Count)
        {
            return;
        }

        // Append after the last existing entry so repeated keys stay grouped, as the game writes them.
        var insertAt = existing.Count > 0
            ? Block.Nodes.ToList().IndexOf(existing[^1]) + 1
            : -1;

        for (var i = existing.Count; i < values.Count; i++)
        {
            var node = CwNode.Assignment(key, quoted ? CwScalar.Quoted(values[i]) : CwScalar.Bare(values[i]));

            if (insertAt < 0)
            {
                InsertInCanonicalOrder(node);
            }
            else
            {
                Block.Insert(insertAt++, node);
            }
        }
    }

    /// <summary>
    /// The unkeyed values inside a list-style block, such as the civics list or a flag's colours.
    /// </summary>
    protected IReadOnlyList<string> GetBlockElements(string key) =>
        GetBlock(key) is { } block
            ? [.. block.Nodes.Where(n => !n.IsAssignment && n.Scalar is not null).Select(n => n.ScalarValue!)]
            : [];

    /// <summary>
    /// Replaces the unkeyed values inside a list-style block, reusing existing entries so the
    /// diff stays small.
    /// </summary>
    protected void SetBlockElements(string key, IReadOnlyList<string> values, bool quoted = true)
    {
        ArgumentNullException.ThrowIfNull(values);

        var block = GetOrAddBlock(key);
        var elements = block.Nodes.Where(n => !n.IsAssignment).ToList();

        for (var i = 0; i < Math.Min(elements.Count, values.Count); i++)
        {
            var keepQuoted = elements[i].Scalar?.IsQuoted ?? quoted;
            elements[i].Value = keepQuoted ? CwScalar.Quoted(values[i]) : CwScalar.Bare(values[i]);
        }

        for (var i = values.Count; i < elements.Count; i++)
        {
            block.Remove(elements[i]);
        }

        for (var i = elements.Count; i < values.Count; i++)
        {
            block.Add(new CwNode(quoted ? CwScalar.Quoted(values[i]) : CwScalar.Bare(values[i])));
        }
    }

    /// <summary>Returns the block for a key, creating it in canonical position when absent.</summary>
    protected CwBlock GetOrAddBlock(string key)
    {
        if (GetBlock(key) is { } existing)
        {
            return existing;
        }

        var created = new CwBlock();
        InsertInCanonicalOrder(CwNode.Assignment(key, created));
        return created;
    }

    /// <summary>
    /// Removes every node with this key.
    /// </summary>
    /// <remarks>
    /// A design that has not got the block has not got the field either, so there is nothing here to
    /// do — and doing it through <see cref="Block"/> made the block on the way, which is a design
    /// gaining a field from having one taken out of it. Clearing a species' biography did this.
    /// </remarks>
    protected void RemoveAll(string key)
    {
        if (Existing is not { } block)
        {
            return;
        }

        foreach (var node in block.Nodes.Where(n => string.Equals(n.Key, key, StringComparison.Ordinal)).ToList())
        {
            block.Remove(node);
        }
    }

    /// <summary>
    /// Inserts a node where the game would have written it, using the known field order. Unknown
    /// keys, and keys the order does not cover, are appended.
    /// </summary>
    private void InsertInCanonicalOrder(CwNode node)
    {
        var position = IndexInFieldOrder(node.Key);
        if (position < 0)
        {
            Block.Add(node);
            return;
        }

        // Sit after the last field that belongs at or before this one. The "at" matters for keys
        // the game repeats, such as trait and ethic: each new entry has to land after the previous
        // one, not ahead of it, or the list comes out reversed.
        for (var i = Block.Nodes.Count - 1; i >= 0; i--)
        {
            var order = IndexInFieldOrder(Block.Nodes[i].Key);
            if (order >= 0 && order <= position)
            {
                Block.Insert(i + 1, node);
                return;
            }
        }

        // Otherwise sit before the first field that belongs after it.
        for (var i = 0; i < Block.Nodes.Count; i++)
        {
            var order = IndexInFieldOrder(Block.Nodes[i].Key);
            if (order > position)
            {
                Block.Insert(i, node);
                return;
            }
        }

        Block.Add(node);
    }

    private int IndexInFieldOrder(string? key)
    {
        if (key is null)
        {
            return -1;
        }

        for (var i = 0; i < _fieldOrder.Count; i++)
        {
            if (string.Equals(_fieldOrder[i], key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
