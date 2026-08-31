namespace Sem.GameData;

/// <summary>
/// One of the words a list offers, and how often the game reaches for it.
/// </summary>
/// <param name="Word">The word itself, such as <c>Imperium</c>.</param>
/// <param name="Weight">
/// Its share of the list. Kept although nothing here rolls dice: it is the game's own statement of
/// which words are typical and which are unusual, and a suggestion list can offer the typical ones
/// first without having to guess at that.
/// </param>
public sealed record EmpireNamePart(string Word, int Weight);

/// <summary>
/// A named list of words an empire name may be built from.
/// </summary>
/// <remarks>
/// The game keeps a hundred and ninety-three of these, one per kind of empire and shade of empire:
/// <c>imperial_gen</c> holds Empire, Imperium, Hegemony, Autocracy, Combine and Hierarchy, while
/// <c>oligarchic_gen</c> holds twenty words of its own. A format references one by name.
/// </remarks>
/// <param name="Key">The name a format refers to it by, as in <c>&lt;imperial_gen&gt;</c>.</param>
/// <param name="Parts">The words, in the order the game declares them.</param>
public sealed record EmpireNamePartsList(string Key, IReadOnlyList<EmpireNamePart> Parts);

/// <summary>
/// One shape an empire's name can take, and the empires it suits.
/// </summary>
/// <remarks>
/// <para>
/// The template language is small. <c>{AofB{&lt;imperial_mil&gt; [This.GetCapitalSystemNameOrRandom]}}</c>
/// says: take the localisation format <c>AofB</c> — "$1$ of $2$" — fill its first blank from the
/// <c>imperial_mil</c> word list and its second from the empire's capital system, and the answer is
/// "Empire of Sol". Three formats are used in all, and three scripted calls, every one of which
/// names something a design already holds.
/// </para>
/// <para>
/// <see cref="When"/> is the game's own <c>random_weight</c>, which every one of these sets to zero
/// and then raises under conditions: a government, sometimes an ethic, and almost always a check
/// that the empire is not nomadic, not a pirate and not a fallen empire. Reading it is what keeps a
/// democracy from being offered the names of an imperium.
/// </para>
/// </remarks>
public sealed record EmpireNameFormat(string Format)
{
    /// <summary>The same name arranged the other way round, as in "Sol Empire".</summary>
    public string? PrefixFormat { get; init; }

    /// <summary>What to call the empire in the middle of a sentence.</summary>
    public string? Noun { get; init; }

    /// <summary>What the empire is called as an adjective, where the format says.</summary>
    public string? Adjective { get; init; }

    /// <summary>Which empires this shape belongs to.</summary>
    public Requirement When { get; init; } = new AlwaysRequirement(true);
}
