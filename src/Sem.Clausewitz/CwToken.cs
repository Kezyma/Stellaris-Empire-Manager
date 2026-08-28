namespace Sem.Clausewitz;

/// <summary>Kinds of token produced by <see cref="CwLexer"/>.</summary>
public enum CwTokenKind
{
    /// <summary>An unquoted identifier, number, keyword or path.</summary>
    BareToken,

    /// <summary>A double-quoted string, including its quotes.</summary>
    QuotedString,

    /// <summary>An assignment or comparison operator: <c>=</c>, <c>==</c>, <c>!=</c>, <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c>, <c>&lt;=</c>, <c>?=</c>.</summary>
    Operator,

    /// <summary><c>{</c></summary>
    LeftBrace,

    /// <summary><c>}</c></summary>
    RightBrace,
}

/// <summary>
/// A single token together with the whitespace and comments that preceded it.
/// </summary>
/// <param name="Kind">What sort of token this is.</param>
/// <param name="Text">The token exactly as it appeared, including quotes on strings.</param>
/// <param name="LeadingTrivia">
/// The whitespace and comments immediately before the token, preserved verbatim. Writing a token
/// back out means writing this then <paramref name="Text"/>, which is what makes a parsed file
/// reproduce byte for byte. <see langword="null"/> marks a token this library created rather than
/// read, and tells the writer to generate appropriate formatting instead.
/// </param>
public sealed record CwToken(CwTokenKind Kind, string Text, string? LeadingTrivia)
{
    /// <summary>Creates a token with no source formatting, to be laid out by the writer.</summary>
    public static CwToken Synthetic(CwTokenKind kind, string text) => new(kind, text, LeadingTrivia: null);

    /// <summary>True when this token came from parsed source and carries its original formatting.</summary>
    public bool IsFromSource => LeadingTrivia is not null;

    /// <summary>
    /// The token's semantic value: quoted strings without their surrounding quotes, everything
    /// else unchanged.
    /// </summary>
    public string Value => Kind == CwTokenKind.QuotedString && Text.Length >= 2
        ? Text[1..^1]
        : Text;
}
