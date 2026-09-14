using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// Whether a player could ever be offered a civic or origin, and what shuts them out if not.
/// </summary>
/// <remarks>
/// <para>
/// The game defines a great many more civics than it will ever let anybody pick. Fallen empires,
/// pre-FTL societies, enclaves, caravaneers and event-granted states have their own, and they sit in
/// the same files as the rest with nothing but a condition to tell them apart.
/// </para>
/// <para>
/// The wiki shows them, which is why this exists. A wiki that hid them would be answering a
/// different question from the one a reader has when they find the name of a civic somewhere and
/// come looking for it - and saying <em>why</em> one is out of reach is most of what makes it worth
/// showing.
/// </para>
/// <para>
/// Deliberately not in <c>Sem.Rules</c>, which asks a different question. That layer evaluates a
/// condition against one design and answers whether <em>this</em> empire may take it. This asks
/// whether <em>any</em> empire ever could, which means evaluating with almost everything unknown -
/// and the two would get in each other's way sharing a name.
/// </para>
/// </remarks>
/// <param name="EverOffered">Whether some player empire could be offered it.</param>
/// <param name="CountryTypes">
/// The kinds of country it demands instead, where that is what shuts the player out. Empty both for
/// one within reach and for one closed by something other than the kind of country.
/// </param>
public sealed record CivicReach(bool EverOffered, IReadOnlyList<string> CountryTypes)
{
    /// <summary>What the game calls an ordinary playable empire, and the only one a design can be.</summary>
    /// <remarks>
    /// The same answer <c>DesignContext.Has</c> gives, quoted rather than reinvented: "an empire
    /// being designed is always an ordinary playable country".
    /// </remarks>
    public const string Player = "default";

    /// <summary>
    /// Reads the whole shelf at once, and says of each one whether a player could ever reach it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All of them together rather than one at a time, because the answer for one can depend on the
    /// answer for another. Four civics are gated on already holding themselves - the game grants
    /// them by event, so <c>civic_galactic_sovereign</c> is offered to an empire that has just
    /// become Galactic Emperor and to nobody else - and three origins are gated on holding one of
    /// their own group of three, which is the same trap with a longer rope.
    /// </para>
    /// <para>
    /// So it is built up rather than filtered down. Nothing is within reach until something shows it
    /// is: start from a design holding none of them, take everything the conditions now permit, and
    /// go round again with those in hand until a pass adds nothing. A civic you can only take once
    /// you have taken it is never added on any pass, and neither is a ring of three that each want
    /// one of the others.
    /// </para>
    /// <para>
    /// Which is also how the editor behaves, and why this can be checked against it: the civic list
    /// is drawn again after every pick, so one civic wanting another is reachable in two steps and a
    /// civic wanting itself is reachable in none.
    /// </para>
    /// </remarks>
    /// <param name="civics">Every civic and origin the game defines.</param>
    /// <returns>The verdict for each, by key.</returns>
    public static IReadOnlyDictionary<string, CivicReach> Across(IReadOnlyList<CivicDefinition> civics)
    {
        ArgumentNullException.ThrowIfNull(civics);

        HashSet<string> reached = new(StringComparer.Ordinal);

        for (var adding = true; adding;)
        {
            adding = false;

            foreach (var civic in civics)
            {
                if (!reached.Contains(civic.Key) && !Refused(civic, reached, knowingTheCountry: true))
                {
                    reached.Add(civic.Key);
                    adding = true;
                }
            }
        }

        return civics.ToDictionary(c => c.Key, c => Verdict(c, reached), StringComparer.Ordinal);
    }

    private static CivicReach Verdict(CivicDefinition civic, HashSet<string> reached)
    {
        if (reached.Contains(civic.Key))
        {
            return new CivicReach(true, []);
        }

        // Which country types to name, if any. Asked by refusing it a second time with the country
        // type forgotten: if it is still refused without that, the country type is not what closed
        // it and naming one would be a sentence about the wrong thing. civic_great_khans_vision is
        // the live case - it permits an ordinary empire's country type and then requires itself, so
        // a card reading its country clause would announce it as belonging to awakened marauders.
        if (Refused(civic, reached, knowingTheCountry: false))
        {
            return new CivicReach(false, []);
        }

        return new CivicReach(false, [.. Wanted(civic)]);
    }

    /// <summary>The kinds of country it asks for, once each, in a settled order.</summary>
    private static IEnumerable<string> Wanted(CivicDefinition civic) =>
        new[] { civic.Potential, civic.Playable, civic.Possible }
            .SelectMany(Selections.Required)
            .Where(s => s.Category == SelectionCategory.CountryType)
            .Select(s => s.Key)
            .Where(key => !string.Equals(key, Player, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Whether any of the three gates that decide whether an option is drawn turns a player away.
    /// </summary>
    /// <remarks>
    /// Potential is the tree the game's own designer reads to decide whether to draw an option at
    /// all; one that fails it is hidden rather than shown as blocked. Playable and Possible are asked
    /// too, because a civic no player empire can ever satisfy is out of reach whichever of the three
    /// refuses it - and all sixteen of the hidden origins are refused by Playable rather than by a
    /// country type, so reading only Potential would miss every one of them.
    /// </remarks>
    private static bool Refused(CivicDefinition civic, HashSet<string> reached, bool knowingTheCountry) =>
        Holds(civic.Potential, reached, knowingTheCountry) == false
        || Holds(civic.Playable, reached, knowingTheCountry) == false
        || Holds(civic.Possible, reached, knowingTheCountry) == false;

    /// <summary>
    /// Whether a condition holds for a player's empire: yes, no, or it depends on something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three answers rather than two, and that is the whole of the care this needs. Two things are
    /// known here - the kind of country, and which civics are within reach at all - and everything
    /// else is a choice the reader has not made, so it answers "it depends" and cannot refuse
    /// anything on its own. Refused means refused whatever they pick.
    /// </para>
    /// <para>
    /// The flat walk this replaces was wrong, and wrong in a way that looked right. Asking
    /// <c>AndNested().OfType&lt;SelectionRequirement&gt;()</c> for a country type finds every one in
    /// the tree at any depth and in any polarity - its own comment says "this condition and every
    /// one nested inside it", not "every one that must hold". The corpus has the counter-example and
    /// the rules layer names it: Corporate's condition is <c>NOT = { country_type = primitive }</c>,
    /// which says a player may have it and a flat walk reads as a gate against them.
    /// </para>
    /// </remarks>
    private static bool? Holds(Requirement? requirement, HashSet<string> reached, bool knowingTheCountry) =>
        requirement switch
        {
            null => null,
            AlwaysRequirement always => always.Value,

            SelectionRequirement { Category: SelectionCategory.CountryType } country =>
                knowingTheCountry ? string.Equals(country.Key, Player, StringComparison.Ordinal) : null,

            // A civic nothing has shown to be within reach is one the design cannot be holding, so
            // asking for it refuses and ruling it out permits. One already within reach is a choice
            // the reader may or may not have made, which is the ordinary answer of "it depends".
            SelectionRequirement { Category: SelectionCategory.Civics or SelectionCategory.Origin } civic =>
                reached.Contains(civic.Key) ? null : false,

            NotRequirement not => Holds(not.Item, reached, knowingTheCountry) switch
            {
                true => false,
                false => true,
                _ => null,
            },

            // False if any child is false. True only when every one of them is known to hold, which
            // is rare here and does no harm: an "it depends" anywhere makes the whole thing depend.
            AllRequirement all => all.Items.Any(i => Holds(i, reached, knowingTheCountry) == false)
                ? false
                : all.Items.All(i => Holds(i, reached, knowingTheCountry) == true) ? true : null,

            // The mirror: true as soon as one child is known to hold, and false only when every one
            // of them is known not to. An empty Any is a condition nothing can satisfy, which is how
            // the game's own NOR with no children reads.
            AnyRequirement any => any.Items.Any(i => Holds(i, reached, knowingTheCountry) == true)
                ? true
                : any.Items.All(i => Holds(i, reached, knowingTheCountry) == false) ? false : null,

            // Everything else is a question about choices nobody has made yet.
            _ => null,
        };
}
