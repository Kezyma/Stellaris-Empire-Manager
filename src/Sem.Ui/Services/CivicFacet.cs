using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// The headings the wiki's civics and origins can be narrowed by.
/// </summary>
/// <remarks>
/// The same table <see cref="EmpireFacet"/> is for the empire lists, over the same
/// <see cref="Facet{TRow}"/>, so the whole of "any within a heading, all across them" comes for
/// free and adding a heading is still a line here.
///
/// None of them declares a fixed shelf. That is for a heading whose options are wider than what the
/// rows happen to hold - and this list holds every civic the game defines, so the rows are the shelf.
/// </remarks>
public static class CivicFacet
{
    /// <summary>Headings about what an empire must be for it.</summary>
    public const string Requirements = "Requirements";

    /// <summary>Headings about what it gives.</summary>
    /// <remarks>
    /// Its own tab because it is a different kind of question. Everything else asks who may have
    /// this; these ask what having it does, and a reader looking for unity is not narrowing by the
    /// same thing as a reader looking for something a hive mind can take.
    /// </remarks>
    public const string Effects = "Effects";

    /// <summary>Every heading, including the two asked outside the card.</summary>
    public static IReadOnlyList<Facet<CivicRow>> All { get; } =
    [
        // Two questions, and deliberately two controls. One is a fixed property of the game and
        // reads the same for everybody: a civic no player empire can ever be offered is out of
        // reach whatever you own. The other is about this machine. Run together they would put a
        // civic you simply have not bought beside one that belongs to a fallen empire, and a reader
        // comparing notes with somebody else would find the same page saying different things.
        Asked("reach", "Anyone can take", r => r.Reach.EverOffered),
        Asked("owned", "In your packs", r => r.Owned),

        Many("authority", "Authority", r => r.Wanting(SelectionCategory.Authority)),
        Many("ethic", "Ethics", r => r.Wanting(SelectionCategory.Ethics)),
        Many("archetype", "Species", r => r.Wanting(SelectionCategory.SpeciesArchetype)),
        Many("needs", "Other civics", r => r.Wanting(SelectionCategory.Civics)),
        Many("pack", "Content pack", r => r.PackChoices),

        Many("bonus", "Bonus", r => r.Bonuses, Effects),
    ];

    /// <summary>
    /// The headings the filter card leaves to the controls beside the search.
    /// </summary>
    /// <remarks>
    /// Both of the yes-or-no ones. They are what a reader asks first - can I have this - and the
    /// card is shut when the page opens. They stay headings, so the narrowing is unchanged.
    /// </remarks>
    public static IReadOnlySet<string> AskedElsewhere { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "reach", "owned" };

    /// <summary>A heading a civic may hold any number of.</summary>
    /// <remarks>
    /// Forwards to <see cref="Facet{TRow}"/>'s own, and exists for the default tab: without it every
    /// line above would name the tab it is already on.
    /// </remarks>
    private static Facet<CivicRow> Many(
        string key,
        string label,
        Func<CivicRow, IReadOnlyList<EmpireChoice>> values,
        string group = Requirements) =>
        Facet<CivicRow>.Many(key, label, values, fixedOptions: null, group);

    /// <summary>A heading whose answer is yes or no.</summary>
    private static Facet<CivicRow> Asked(
        string key,
        string label,
        Func<CivicRow, bool> held,
        string group = Requirements) =>
        Facet<CivicRow>.Asked(key, label, held, group);
}
