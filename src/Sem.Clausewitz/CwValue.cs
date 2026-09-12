namespace Sem.Clausewitz;

/// <summary>The right-hand side of an assignment, or a bare element inside a block.</summary>
public abstract class CwValue
{
    private protected CwValue()
    {
    }

    /// <summary>
    /// Copies this value and everything under it. Tokens are immutable and therefore shared, so a
    /// clone keeps the original's formatting and writes out looking exactly like its source.
    /// </summary>
    public abstract CwValue Clone();
}

/// <summary>A single token value: an identifier, number, keyword or quoted string.</summary>
public sealed class CwScalar : CwValue
{
    /// <summary>Wraps one token as a scalar.</summary>
    /// <param name="token">A bare or quoted token; any other kind is refused.</param>
    /// <exception cref="ArgumentException">The token is neither bare nor quoted.</exception>
    public CwScalar(CwToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.Kind is not (CwTokenKind.BareToken or CwTokenKind.QuotedString))
        {
            throw new ArgumentException($"A scalar cannot be built from a {token.Kind} token.", nameof(token));
        }

        Token = token;
    }

    /// <summary>The underlying token, carrying the original text and formatting.</summary>
    public CwToken Token { get; }

    /// <summary>The value without quotes.</summary>
    public string Value => Token.Value;

    /// <summary>True when the source had this value in double quotes.</summary>
    public bool IsQuoted => Token.Kind == CwTokenKind.QuotedString;

    /// <summary>Creates a quoted scalar to be formatted by the writer.</summary>
    public static CwScalar Quoted(string value) =>
        new(CwToken.Synthetic(CwTokenKind.QuotedString, CwToken.Quote(value)));

    /// <summary>Creates an unquoted scalar to be formatted by the writer.</summary>
    public static CwScalar Bare(string value) =>
        new(CwToken.Synthetic(CwTokenKind.BareToken, value));

    /// <inheritdoc />
    public override CwValue Clone() => new CwScalar(Token);

    /// <summary>The value without its quotes.</summary>
    public override string ToString() => Value;
}

/// <summary>A brace-delimited block. Its children may be assignments, bare values, or both.</summary>
public sealed class CwBlock : CwValue
{
    private readonly List<CwNode> _nodes;

    /// <summary>Builds a block from its braces and what they hold.</summary>
    /// <param name="open">The opening brace, carrying its original formatting.</param>
    /// <param name="nodes">What the block holds, in file order.</param>
    /// <param name="close">The closing brace, absent for a block the file never closed.</param>
    public CwBlock(CwToken open, IEnumerable<CwNode> nodes, CwToken? close)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(nodes);

        Open = open;
        Close = close;
        _nodes = [.. nodes];
    }

    /// <summary>Creates an empty block to be formatted by the writer.</summary>
    public CwBlock()
        : this(CwToken.Synthetic(CwTokenKind.LeftBrace, "{"), [], CwToken.Synthetic(CwTokenKind.RightBrace, "}"))
    {
    }

    /// <summary>The opening brace token.</summary>
    public CwToken Open { get; }

    /// <summary>
    /// The closing brace token, or null when the file ended before this block was closed. Vanilla
    /// Stellaris ships one such file, so lenient parsing has to represent it without inventing a
    /// brace that would change the bytes on the way out.
    /// </summary>
    public CwToken? Close { get; }

    /// <summary>False when the source ended before this block was closed.</summary>
    public bool IsClosed => Close is not null;

    /// <summary>
    /// The block's children in source order. Order and duplicates are both significant: Stellaris
    /// repeats keys such as <c>ethic</c> and <c>trait</c>, and relies on list order elsewhere.
    /// </summary>
    public IReadOnlyList<CwNode> Nodes => _nodes;

    /// <summary>Appends a child.</summary>
    public void Add(CwNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Add(node);
    }

    /// <summary>Inserts a child at a position.</summary>
    public void Insert(int index, CwNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Insert(index, node);
    }

    /// <summary>Removes a child.</summary>
    public bool Remove(CwNode node) => _nodes.Remove(node);

    /// <summary>Removes the child at a position.</summary>
    public void RemoveAt(int index) => _nodes.RemoveAt(index);

    /// <summary>Removes every child.</summary>
    public void Clear() => _nodes.Clear();

    /// <inheritdoc />
    public override CwValue Clone() => new CwBlock(Open, _nodes.Select(n => n.Clone()), Close);
}
