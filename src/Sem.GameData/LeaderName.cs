namespace Sem.GameData;

/// <summary>
/// Puts a leader's name together out of the two parts a name list holds it in.
/// </summary>
/// <remarks>
/// <para>
/// The rule is not written down anywhere the game ships. It is the engine's, and this is what the
/// data shows it to be, read off thirty-one thousand name-list entries and every ruler in the
/// player's own files.
/// </para>
/// <para>
/// The pieces live here rather than in the localiser because two quite different callers need the
/// same answer: the localiser, reading a name a design stored, and the name lists, offering names a
/// player might pick. Those two disagreeing is how a ruler could be suggested under one name and
/// then displayed under another.
/// </para>
/// </remarks>
public static class LeaderName
{
    /// <summary>Separates a name's forms from one another.</summary>
    private const string VariantSeparator = "|||";

    /// <summary>The hole one part leaves for the other.</summary>
    private const string Placeholder = "$1$";

    /// <summary>
    /// Picks the form of a name that suits a gender, where the name offers more than one.
    /// </summary>
    /// <remarks>
    /// Written <c>"$1$ Aburia|||masc:$1$ Aburius"</c>: the form before the bars is the one used
    /// unless something else applies, and a tagged form after them replaces it for that tag. Four
    /// hundred and sixty-three entries do this and every one of them tags <c>masc</c> — no other tag
    /// occurs anywhere in the game's text — so a masculine ruler takes the tagged form and everyone
    /// else takes the plain one.
    /// </remarks>
    public static string Variant(string? text, string? gender)
    {
        if (text is not { Length: > 0 } || !text.Contains(VariantSeparator, StringComparison.Ordinal))
        {
            return text ?? string.Empty;
        }

        var forms = text.Split(VariantSeparator, StringSplitOptions.None);
        var wanted = gender is "male" ? "masc" : null;

        if (wanted is not null)
        {
            foreach (var form in forms.Skip(1))
            {
                if (form.StartsWith($"{wanted}:", StringComparison.Ordinal))
                {
                    return form[(wanted.Length + 1)..];
                }
            }
        }

        return forms[0];
    }

    /// <summary>
    /// Joins the two parts of a leader's name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usually they are two words and the name is both of them: Lucius Salazar. But a part may
    /// instead be a frame with a hole in it for the other — <c>"Feathers of $1$"</c> paired with
    /// <c>"Silver"</c>, or <c>"$1$ Aburia"</c> paired with <c>"Gaius"</c> — and then the filled
    /// frame is the whole name and there is nothing to join it to.
    /// </para>
    /// <para>
    /// The second part is checked first because that is where the frames mostly are: six hundred and
    /// thirty-six entries hold the hole at the front of a family name, against seventy-four that
    /// hold it at the end of a given one. Dropping the hole and joining the words instead, which is
    /// what happened before, reads correctly only for the seventy-four.
    /// </para>
    /// </remarks>
    public static string Compose(string? first, string? second, string? gender = null)
    {
        var one = Variant(first, gender);
        var two = Variant(second, gender);

        if (two.Contains(Placeholder, StringComparison.Ordinal))
        {
            return two.Replace(Placeholder, one, StringComparison.Ordinal).Trim();
        }

        if (one.Contains(Placeholder, StringComparison.Ordinal))
        {
            return one.Replace(Placeholder, two, StringComparison.Ordinal).Trim();
        }

        return string.Join(' ', new[] { one, two }.Where(part => part.Length > 0));
    }
}
