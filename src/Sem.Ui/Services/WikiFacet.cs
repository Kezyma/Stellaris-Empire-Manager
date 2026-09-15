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

        // What the record has always held and the page never drew. "Which of these can I reform
        // into" is the question a plan is made of, and until now the only way to answer it was to
        // try.
        Many("reform", "Reform", r => Fact(r, "Reform")),

        // Who the game rewords it for, which is how a hive mind or a nomad finds the entry that is
        // actually about them.
        Many("wording", "Also called", r => Fact(r, "Also called")),
        Many("worded", "Reworded for", r => Fact(r, "Reworded for")),
        Many("forces", "Forces", r => Fact(r, "Forces")),
        Many("starts", "Starts on", r => Fact(r, "Starts on")),
        Many("system", "Starting system", r => Fact(r, "Starting system")),
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

    /// <summary>The species classes, which carry no effects and so have no bonus heading.</summary>
    public static IReadOnlyList<Facet<WikiRow>> Species { get; } =
    [
        .. Both,
        Many("archetype", "Archetype", r => Fact(r, "Archetype")),
        Many("trait", "Always has", r => Fact(r, "Always has")),
        Many("shipset", "Flies", r => Fact(r, "Flies")),
    ];

    /// <summary>
    /// The species traits, narrowed by what they cost and who may take them.
    /// </summary>
    /// <remarks>
    /// Cost is a heading here rather than only a column because it is the question a reader arrives
    /// with: the whole of picking traits is spending a budget, so "what can I get for two points"
    /// is the search, and "which of these give points back" is the other half of it.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> SpeciesTraits { get; } =
    [
        .. Both,
        Many("cost", "Cost", r => Fact(r, "Cost")),
        Many("category", "Category", r => Fact(r, "Category")),
        Many("archetype", "Archetype", r => Fact(r, "Archetype")),
        Many("class", "Only for", r => Fact(r, "Only for")),
        Many("origin", "Origin", r => Fact(r, "Origin")),
        Many("homeworld", "Homeworld", r => Fact(r, "Homeworld")),
        Many("pack", "Content pack", r => r.PackChoices),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// The worlds, narrowed by the two things that decide whether one is any use.
    /// </summary>
    /// <remarks>
    /// The preference is not a heading. It names a different trait on every world, so a heading of
    /// sixty-nine ticks each matching one row is a list of the rows with extra steps.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> Planets { get; } =
    [
        .. Both,
        Many("climate", "Climate", r => Fact(r, "Climate")),
        Many("start", "Start here", r => Fact(r, "Start here")),
        Many("opened", "Opened by", r => Fact(r, "Opened by")),
        Many("settled", "Colonisable", r => Fact(r, "Colonisable")),
        Many("pack", "Content pack", r => r.PackChoices),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// The shipsets, which do nothing and so have no bonus heading.
    /// </summary>
    /// <remarks>
    /// Whether a set flies ships of its own is the division worth offering: a reader looking for a
    /// fleet is not served by the two sets that dress cities and borrow somebody else's ships.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> Shipsets { get; } =
    [
        .. Both,
        Many("fleet", "Fleet", r => Fact(r, "Fleet")),
        Many("cities", "Cities", r => Fact(r, "Cities")),
        Many("pack", "Content pack", r => r.PackChoices),
    ];

    /// <summary>
    /// The personalities, narrowed by the empire that would be played as one.
    /// </summary>
    /// <remarks>
    /// Almost every one of these is gated on ethics and nothing else, which makes the ethics heading
    /// the whole of the page: "which of these is my militarist neighbour likely to be" is the
    /// question a reader arrives with.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> Personalities { get; } =
    [
        .. Both,
        Many("ethic", "Ethics", r => r.Wanting(SelectionCategory.Ethics)),
        Many("authority", "Authority", r => r.Wanting(SelectionCategory.Authority)),
        Many("civic", "Civics", r => r.Wanting(SelectionCategory.Civics)),

        // What it will do, which narrows better than anything else here: "which of my neighbours
        // enslaves" is a question with an answer, and the ethics heading only says who might.
        Many("behaviour", "Behaviour", r => Fact(r, "Behaviour")),
        Many("weapons", "Weapons", r => Fact(r, "Weapons")),

        // What tips the draw toward one, which is the other half of "who will I meet": the ethics
        // heading says who may be played as this, and this says who is likely to be.
        Many("pulls", "More likely", r => Fact(r, "More likely")),
    ];

    /// <summary>
    /// The governments, narrowed by what a design has to be to be called one.
    /// </summary>
    /// <remarks>
    /// The titles are not headings. A hundred and seventy governments name a hundred and seventy
    /// rulers between them, so a Ruler heading is the list again; the search box is what finds a
    /// reader the government that calls its ruler an Archon.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> Governments { get; } =
    [
        .. Both,
        Many("authority", "Authority", r => r.Wanting(SelectionCategory.Authority)),
        Many("ethic", "Ethics", r => r.Wanting(SelectionCategory.Ethics)),
        Many("civic", "Civics", r => r.Wanting(SelectionCategory.Civics)),
    ];

    /// <summary>The ascension perks, narrowed by the path they belong to.</summary>
    public static IReadOnlyList<Facet<WikiRow>> AscensionPerks { get; } =
    [
        .. Both,
        Many("path", "Path", r => Fact(r, "Path")),
        Many("tier", "Tier", r => Fact(r, "Tier")),
        Many("worded", "Reworded for", r => Fact(r, "Reworded for")),
        Many("ethic", "Ethics", r => r.Wanting(SelectionCategory.Ethics)),
        Many("civic", "Civics", r => r.Wanting(SelectionCategory.Civics)),
        Many("pack", "Content pack", r => r.PackChoices),
        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// The leader traits, narrowed by who may hold them and what sort they are.
    /// </summary>
    /// <remarks>
    /// No reach heading, unlike every other shelf. None of these is out of reach - a leader trait is
    /// earned in a game rather than offered in a list, so "can a player take this" is a question
    /// about the wrong thing - and a toggle whose answer is Yes seven hundred times is a control
    /// that takes width to say nothing.
    /// </remarks>
    public static IReadOnlyList<Facet<WikiRow>> LeaderTraits { get; } =
    [
        Many("class", "Class", r => Fact(r, "Class")),
        Many("sort", "Sort", r => Fact(r, "Sort")),
        Many("rarity", "Rarity", r => Fact(r, "Rarity")),
        Many("tier", "Tier", r => Fact(r, "Tier")),
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
