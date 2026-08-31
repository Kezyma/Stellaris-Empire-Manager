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
        ? Unescape(Text[1..^1])
        : Text;

    /// <summary>
    /// Wraps a value in quotes so that reading it back gives the same value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every synthesised string goes through here. Written without it, an empire named
    /// <c>The "Peacock" Dynamics</c> produced <c>key="The "Peacock" Dynamics"</c>, which is a
    /// different and largely nonsensical document — and since a name goes into the designs file as
    /// well as into a link, that was the player's own file being written malformed.
    /// </para>
    /// <para>
    /// Only a quote is escaped, and only a backslash that would otherwise change what the quote
    /// after it means. Paradox script has no general escape mechanism — <c>gfx\models\ship.mesh</c>
    /// is a path and not an escape of anything — so treating every backslash as one would corrupt
    /// far more than it fixed.
    /// </para>
    /// </remarks>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var escaped = new System.Text.StringBuilder(value.Length + 2).Append('"');

        for (var i = 0; i < value.Length; i++)
        {
            var protectable = value[i] == '\\' && (i + 1 == value.Length || value[i + 1] == '"');

            if (protectable || value[i] == '"')
            {
                escaped.Append('\\');
            }

            escaped.Append(value[i]);
        }

        return escaped.Append('"').ToString();
    }

    /// <summary>Puts back what <see cref="Quote"/> protected.</summary>
    private static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var value = new System.Text.StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            // Anything else after a backslash is left alone, backslash and all, because in this
            // format it is not an escape and never was.
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
            {
                i++;
            }

            value.Append(text[i]);
        }

        return value.ToString();
    }
}
