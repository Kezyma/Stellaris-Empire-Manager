using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// The headings each of the wiki's shelves can be narrowed by.
/// </summary>
/// <remarks>
/// The same table <see cref="EmpireFacet"/> is for the empire lists, over the same
/// <see cref="Facet{TRow}"/>, so the whole of "any within a heading, all across them" comes for free
/// and adding a heading is still a line here.
///
/// None of them declares a fixed shelf. That is for a heading whose options are wider than what the
/// rows happen to hold - and each of these lists holds every one of a thing the game defines, so the
/// rows are the shelf.
/// </remarks>
public static class WikiFacet
{
    /// <summary>Headings about what an empire must be, or must not be.</summary>
    public const string Requirements = "Requirements";

    /// <summary>Headings about what it gives.</summary>
    /// <remarks>
    /// Its own tab because it is a different kind of question. Everything else asks who may have
    /// this; these ask what having it does, and a reader looking for unity is not narrowing by the
    /// same thing as a reader looking for something a hive mind can take.
    /// </remarks>
    public const string Effects = "Effects";

    /// <summary>
    /// The one question asked beside the search rather than behind the card's fold.
    /// </summary>
    /// <remarks>
    /// Whether a player can take it at all, which is what a reader asks first and the card is shut
    /// when the page opens. It stays a heading, so the narrowing is unchanged; only where it is
    /// asked moved.
    ///
    /// Ownership is not here any more. The content pack bar at the top of every page already says
    /// which packs to judge by, and a second control saying the same thing differently was two
    /// answers to one question - so the pack chips reflect that bar, and the heading is in the card
    /// with the rest for anybody who wants to narrow by it.
    /// </remarks>
    public static IReadOnlySet<string> AskedElsewhere { get; } =
        new HashSet<string>(StringComparer.Ordinal) { Reach };

    /// <summary>What the playability toggle writes to.</summary>
    public const string Reach = "reach";

    /// <summary>What the content pack toggle writes to.</summary>
    public const string OwnedKey = "owned";

    /// <summary>
    /// The two headings every shelf has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two headings and not one, because they are two questions. Whether any player can ever take a
    /// thing is a fixed property of the game and reads the same for everybody; whether your packs
    /// allow it is about your machine.
    /// </para>
    /// <para>
    /// Written above the three lists that spread it, and it has to be: a static field is filled in
    /// the order it is declared, and read from below them this was reading a list that did not exist
    /// yet.
    /// </para>
    /// </remarks>
    private static readonly Facet<WikiRow>[] Both =
    [
        Asked(Reach, "Anyone can take", r => r.Playable),
        Asked(OwnedKey, "In your packs", r => r.Owned),
    ];

    /// <summary>The civics and the origins, which are the same records and narrow the same way.</summary>
    public static IReadOnlyList<Facet<WikiRow>> Civics { get; } =
    [
        .. Both,
        Many("authority", "Authority", r => r.Wanting(SelectionCategory.Authority)),
        Many("ethic", "Ethics", r => r.Wanting(SelectionCategory.Ethics)),
        Many("archetype", "Species", r => r.Wanting(SelectionCategory.SpeciesArchetype)),
        Many("needs", "Other civics", r => r.Wanting(SelectionCategory.Civics)),
        Many("pack", "Content pack", r => r.PackChoices),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// The ethics, which the game gates on nothing at all.
    /// </summary>
    /// <remarks>
    /// No pack heading, because not one of the seventeen is behind a pack; no requirement headings,
    /// because they state no conditions. What is left is what an ethic costs and where it sits in
    /// its pair, which are its facts.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> Ethics { get; } =
    [
        .. Both,
        Many("cost", "Cost", r => Fact(r, "Cost")),
        Many("intensity", "Intensity", r => Fact(r, "Intensity")),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>The authorities, which are eight records with a great deal said about each.</summary>
    public static IReadOnlyList<Facet<WikiRow>> Authorities { get; } =
    [
        .. Both,
        Many("elections", "Elections", r => Fact(r, "Elections")),
        Many("heir", "Heir", r => Fact(r, "Heir")),
        Many("forces", "Forces", r => Fact(r, "Forces")),
        Many("pack", "Content pack", r => r.PackChoices),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// One of a row's own facts, as the choices a heading offers.
    /// </summary>
    /// <remarks>
    /// A fact that names things offers those; one that says a word offers the word, keyed on itself
    /// so that two rows saying "Oligarchic" are one tick rather than two.
    /// </remarks>
    private static IReadOnlyList<EmpireChoice> Fact(WikiRow row, string heading) =>
        row.Fact(heading) switch
        {
            null => [],
            { Chips.Count: > 0 } named => named.Chips,
            { Text: { Length: > 0 } said } => [new EmpireChoice(said, said, null, null)],
            _ => [],
        };

    /// <summary>A heading a row may hold any number of.</summary>
    /// <remarks>
    /// Forwards to <see cref="Facet{TRow}"/>'s own, and exists for the default tab: without it every
    /// line above would name the tab it is already on.
    /// </remarks>
    private static Facet<WikiRow> Many(
        string key,
        string label,
        Func<WikiRow, IReadOnlyList<EmpireChoice>> values,
        string group = Requirements) =>
        Facet<WikiRow>.Many(key, label, values, fixedOptions: null, group);

    /// <summary>A heading whose answer is yes or no.</summary>
    private static Facet<WikiRow> Asked(
        string key,
        string label,
        Func<WikiRow, bool> held,
        string group = Requirements) =>
        Facet<WikiRow>.Asked(key, label, held, group);
}
