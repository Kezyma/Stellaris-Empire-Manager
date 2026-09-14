namespace Sem.GameData;

/// <summary>
/// The thing a modifier's key names, where its own label will not say.
/// </summary>
/// <remarks>
/// <para>
/// The shroud patrons are the case, and the only one so far. Their modifiers are labelled
/// <c>Add Attunement with [This.GetEaterColor]</c>, and that scripted value answers
/// <c>UNDISCOVERED_PATRON_ARTICLE</c> - "an Unknown Entity" - until a game has actually met the
/// patron. Faithful, and useless on a page: five separate modifiers all read as the same anonymous
/// line.
/// </para>
/// <para>
/// The key says which one it is, so the name can be put back. Here rather than beside the reader
/// because two projects have to agree about it and neither can see the other: the extractor decides
/// which localisation entries survive pruning, and the designer looks the entity's name up in what
/// survived. They did not agree. The mechanism for naming the patron was written, and the entry it
/// reads was pruned away - so the designer, the wiki and every tooltip in the app said "an Unknown
/// Entity" with the code to say otherwise sitting right there. Nothing could notice, because the
/// substitution fails silently by design: a modifier that names no entity is meant to be left alone.
/// </para>
/// </remarks>
public static class ModifierSubject
{
    /// <summary>
    /// How a patron's own key is dressed up into an attunement modifier's key.
    /// </summary>
    /// <remarks>
    /// Three shapes, all of which read as the same anonymous line and all of which name the patron
    /// in the key: <c>add_attunement_the_eater_of_worlds</c> is "Add Attunement with […]",
    /// <c>the_eater_of_worlds_attunement_mult</c> is "Attunement with […]", and
    /// <c>eater_of_worlds_monthly_attunement_add</c> is "Monthly Attunement with […]". Only the
    /// first was handled once, so an empire could show one named row and two anonymous ones for the
    /// same patron.
    /// </remarks>
    private static readonly (string Prefix, string Suffix)[] Shapes =
    [
        ("add_attunement_", ""),
        ("", "_attunement_mult"),
        ("", "_monthly_attunement_add"),
    ];

    /// <summary>
    /// The localisation keys a modifier's key might name its subject under.
    /// </summary>
    /// <remarks>
    /// Both spellings, because the monthly modifiers drop the article -
    /// <c>eater_of_worlds_monthly_attunement_add</c> against the entry <c>the_eater_of_worlds</c> -
    /// so the stem is offered with and without it. Whichever is a real entry wins; anything that is
    /// not a patron at all simply matches neither, which costs the caller nothing.
    /// </remarks>
    /// <param name="key">The modifier's key, as the game writes it.</param>
    /// <returns>The entries worth trying, which is usually none.</returns>
    public static IEnumerable<string> Candidates(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var (prefix, suffix) in Shapes)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal) ||
                !key.EndsWith(suffix, StringComparison.Ordinal) ||
                key.Length <= prefix.Length + suffix.Length)
            {
                continue;
            }

            var stem = key[prefix.Length..^suffix.Length];

            yield return stem;
            yield return $"the_{stem}";
        }
    }
}
