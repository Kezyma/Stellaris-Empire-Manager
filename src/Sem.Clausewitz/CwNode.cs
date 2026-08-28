namespace Sem.Clausewitz;

/// <summary>
/// One entry in a block: either an assignment (<c>key = value</c>) or a bare element
/// (<c>"red"</c> inside a colour list).
/// </summary>
public sealed class CwNode
{
    /// <summary>Creates an assignment node.</summary>
    public CwNode(CwToken key, CwToken op, CwValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(value);

        KeyToken = key;
        OperatorToken = op;
        Value = value;
    }

    /// <summary>Creates a bare element node, as found in list-style blocks.</summary>
    public CwNode(CwValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The key token, or null when this is a bare element.</summary>
    public CwToken? KeyToken { get; private set; }

    /// <summary>The operator token, or null when this is a bare element.</summary>
    public CwToken? OperatorToken { get; }

    /// <summary>The value.</summary>
    public CwValue Value { get; set; }

    /// <summary>True when this node is <c>key = value</c> rather than a bare element.</summary>
    public bool IsAssignment => KeyToken is not null;

    /// <summary>The key without quotes, or null for a bare element.</summary>
    public string? Key => KeyToken?.Value;

    /// <summary>The operator text, normally <c>=</c>. Null for a bare element.</summary>
    public string? Operator => OperatorToken?.Text;

    /// <summary>The value as a block, or null when it is a scalar.</summary>
    public CwBlock? Block => Value as CwBlock;

    /// <summary>The value as a scalar, or null when it is a block.</summary>
    public CwScalar? Scalar => Value as CwScalar;

    /// <summary>The scalar value without quotes, or null when the value is a block.</summary>
    public string? ScalarValue => (Value as CwScalar)?.Value;

    /// <summary>Copies this node and everything under it, keeping the original's formatting.</summary>
    public CwNode Clone() => KeyToken is null
        ? new CwNode(Value.Clone())
        : new CwNode(KeyToken, OperatorToken!, Value.Clone());

    /// <summary>
    /// Renames this node, keeping its original position and surrounding whitespace so only the key
    /// itself changes in the written file.
    /// </summary>
    public void Rename(string key, bool quoted)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (KeyToken is null)
        {
            throw new InvalidOperationException("A bare element has no key to rename.");
        }

        KeyToken = new CwToken(
            quoted ? CwTokenKind.QuotedString : CwTokenKind.BareToken,
            quoted ? $"\"{key}\"" : key,
            KeyToken.LeadingTrivia);
    }

    /// <summary>Builds <c>key = value</c> with formatting left to the writer.</summary>
    public static CwNode Assignment(string key, CwValue value, bool quoteKey = false) =>
        new(
            CwToken.Synthetic(
                quoteKey ? CwTokenKind.QuotedString : CwTokenKind.BareToken,
                quoteKey ? $"\"{key}\"" : key),
            CwToken.Synthetic(CwTokenKind.Operator, "="),
            value);

    /// <summary>Builds <c>key = "value"</c> with formatting left to the writer.</summary>
    public static CwNode QuotedAssignment(string key, string value, bool quoteKey = false) =>
        Assignment(key, CwScalar.Quoted(value), quoteKey);

    /// <summary>Builds <c>key = value</c> with an unquoted value, for booleans, numbers and enums.</summary>
    public static CwNode BareAssignment(string key, string value, bool quoteKey = false) =>
        Assignment(key, CwScalar.Bare(value), quoteKey);

    public override string ToString() => IsAssignment
        ? $"{Key} {Operator} {(Value is CwBlock ? "{ ... }" : ScalarValue)}"
        : ScalarValue ?? "{ ... }";
}
