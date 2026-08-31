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
    /// Both the quote and the backslash are escaped, because the lexer reads a backslash as taking
    /// the character after it whatever that character is. Escaping only some of them left the rest
    /// pairing with whatever followed: <c>a\\b</c> came back as <c>a\b</c>, one backslash shorter on
    /// every save, and a value ending <c>\\</c> ate its own closing quote and ran the string into
    /// the rest of the file.
    /// </para>
    /// <para>
    /// This applies to values this library writes, which are names, keys and descriptions. Paths
    /// like <c>gfx\models\ship.mesh</c> live in the game's own script, which is read and re-emitted
    /// from the bytes it arrived as rather than being written through here.
    /// </para>
    /// </remarks>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var escaped = new System.Text.StringBuilder(value.Length + 2).Append('"');

        foreach (var character in value)
        {
            if (character is '"' or '\\')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.Append('"').ToString();
    }

    /// <summary>
    /// Puts back exactly what <see cref="Quote"/> protected, and nothing else.
    /// </summary>
    /// <remarks>
    /// The pair has to be inverses, or a value loses a character every time it is read and written
    /// and loses another on the next save. A backslash before a quote or another backslash is
    /// dropped, matching what <see cref="Quote"/> writes; anything else after a backslash is left
    /// alone, backslash and all, because the game's own files hold lone backslashes in paths that
    /// escape nothing.
    /// </remarks>
    private static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var value = new System.Text.StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
            {
                i++;
            }

            value.Append(text[i]);
        }

        return value.ToString();
    }
}
