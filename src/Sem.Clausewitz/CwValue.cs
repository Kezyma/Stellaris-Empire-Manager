namespace Sem.Clausewitz;

/// <summary>The right-hand side of an assignment, or a bare element inside a block.</summary>
public abstract class CwValue
{
    private protected CwValue()
    {
    }
}

/// <summary>A single token value: an identifier, number, keyword or quoted string.</summary>
public sealed class CwScalar : CwValue
{
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
        new(CwToken.Synthetic(CwTokenKind.QuotedString, $"\"{value}\""));

    /// <summary>Creates an unquoted scalar to be formatted by the writer.</summary>
    public static CwScalar Bare(string value) =>
        new(CwToken.Synthetic(CwTokenKind.BareToken, value));

    public override string ToString() => Value;
}

/// <summary>A brace-delimited block. Its children may be assignments, bare values, or both.</summary>
public sealed class CwBlock : CwValue
{
    private readonly List<CwNode> _nodes;

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
}
