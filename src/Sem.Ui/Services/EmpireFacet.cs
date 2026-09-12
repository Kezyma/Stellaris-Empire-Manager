using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// One heading the lists can be narrowed by: what it is called, and what an empire holds under it.
/// </summary>
/// <remarks>
/// A table rather than a control per heading, because there are thirteen of them and every one is
/// the same control asking the same question of a different field. Adding one is a line here.
/// </remarks>
/// <param name="Key">What the choice is remembered under, and what the column shares with it.</param>
/// <param name="Label">What the heading is called in the filter card.</param>
/// <param name="Values">What an empire holds under it, which may be none, one or several.</param>
/// <param name="Fixed">
/// Every option there is, for a heading whose options are a setting rather than a shelf of game
/// data - and nothing for the rest, which are offered whatever the empires in front of the reader
/// actually hold.
/// </param>
/// <param name="Group">
/// Which tab of the filter card it sits on. Twenty-eight controls in one grid is a wall to read
/// rather than a card to use, and the four groups are the four things an empire is made of.
/// </param>
public sealed record EmpireFacet(
    string Key,
    string Label,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>> Values,
    Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? Fixed = null,
    string Group = "Empire")
{
    public const string Empire = "Empire";

    public const string Species = "Species";

    public const string Ruler = "Ruler";

    public const string Galaxy = "Galaxy";

    /// <summary>
    /// The six colour slots.
    /// </summary>
    /// <remarks>
    /// Their own tab because they are six headings that answer the same question about six different
    /// things, and scattered among the rest they read as six unrelated settings. Grouped, the
    /// arrangement of the slots is visible: three for the flag, one for the fleet, two for the map.
    /// </remarks>
    public const string Colours = "Colours";

    /// <summary>
    /// What the empire means to become, rather than what it is.
    /// </summary>
    /// <remarks>
    /// Its own tab because it is a different kind of question. Everything else here asks what an
    /// empire is now; these ask what its player wrote down about later, and mixing the two would
    /// put "Planned civics" beside "Civics" as though they were alternatives.
    /// </remarks>
    public const string Plan = "Plan";

    /// <summary>Every heading with a list behind it, which is everything that is picked rather than typed.</summary>
    public static IReadOnlyList<EmpireFacet> All { get; } =
    [
        new("preset", "Preset", r => YesOrNo(r.Preset is not null)),

        // Derived rather than chosen: a government is what an authority, some ethics and some
        // civics add up to, and the hundred and seventy the game defines are not a list anybody
        // picks from. What is offered is what these empires came to.
        new("government", "Government", r => Some(r.Government)),

        // The same kind of thing, and offered the same way: nobody picks a personality either, and
        // what is worth offering is the ones these empires could actually be given rather than all
        // fifty-one the game defines - twenty of which belong to fallen empires and pre-FTL
        // societies and can reach no design at all.
        new("personality", "AI personality", r => r.Personalities),

        new("authority", "Authority", r => Some(r.Authority), o => o.Authorities),
        new("nomadic", "Nomadic", r => YesOrNo(r.Nomadic)),
        new("ethics", "Ethics", r => r.Ethics, o => o.Ethics),
        new("civics", "Civics", r => r.Civics, o => o.Civics),
        new("origin", "Origin", r => Some(r.Origin), o => o.Origins),
        new("spawn", "AI spawning", r => Some(r.Spawn), o => o.Spawning),
        new("fallen", "Fallen empire", r => YesOrNo(r.Fallen)),
        new("flagset", "Special flags", r => Some(r.FlagSet), o => o.FlagSets),
        new("advisor", "Advisor voice", r => Some(r.Advisor), o => o.Advisors),

        new("primary", "Primary flag colour", r => Some(r.Primary), o => o.FlagColors, Colours),
        new("secondary", "Secondary flag colour", r => Some(r.Secondary), o => o.FlagColors, Colours),
        new("tertiary", "Tertiary colour", r => Some(r.Tertiary), o => o.FlagColors, Colours),
        new("shipcolor", "Ship colour", r => Some(r.ShipColor), o => o.FlagColors, Colours),
        new("mapborder", "Map border", r => Some(r.MapBorder), o => o.MapColors, Colours),
        new("mapfill", "Map fill", r => Some(r.MapFill), o => o.MapColors, Colours),

        new("class", "Species", r => Some(r.SpeciesClass), o => o.SpeciesClasses, Species),
        new("portrait", "Portrait", r => Some(r.Portrait), o => o.Portraits, Species),
        new("gender", "Gender", r => Some(r.Gender), o => o.Genders, Species),
        new("namelist", "Name list", r => Some(r.NameList), o => o.NameLists, Species),
        new("traits", "Traits", r => r.Traits, o => o.Traits, Species),
        new("second", "Second species", r => YesOrNo(r.HasSecondSpecies), Group: Species),
        new("secondclass", "Second species kind", r => Some(r.SecondClass), o => o.SpeciesClasses, Species),
        new("secondtraits", "Second species traits", r => r.SecondTraits, o => o.Traits, Species),

        new("rulerclass", "Ruler class", r => Some(r.RulerClass), o => o.RulerClasses, Ruler),
        new("rulerportrait", "Ruler portrait", r => Some(r.RulerPortrait), o => o.Portraits, Ruler),
        new("rulergender", "Ruler gender", r => Some(r.RulerGender), o => o.Genders, Ruler),
        new("rulertraits", "Ruler traits", r => r.RulerTraits, o => o.RulerTraits, Ruler),

        new("homeworld", "Homeworld", r => Some(r.PlanetClass), o => o.Homeworlds, Galaxy),
        new("system", "Starting system", r => Some(r.StartingSystem), o => o.StartingSystems, Galaxy),
        new("room", "Room", r => Some(r.Room), o => o.Rooms, Galaxy),
        new("shipset", "Shipset", r => Some(r.Shipset), o => o.Shipsets, Galaxy),
        new("bioship", "Bioships", r => YesOrNo(r.Bioship), Group: Galaxy),

        // The civics one reads the whole government a plan ends with, the greyed ones included:
        // a civic the empire cannot give up is part of what it will be, so a reader asking which
        // empires end up with it should be told about those too.
        new("plantraditions", "Planned traditions", r => r.PlanTrees, o => o.TraditionTrees, Plan),
        new("planperks", "Planned perks", r => r.PlanPerks, o => o.AscensionPerks, Plan),
        new("plancivics", "Planned civics", r => r.PlanCivics, o => o.PlannableCivics, Plan),
    ];

    /// <summary>
    /// The tabs, in the order they are drawn, which is the order the headings declare.
    /// </summary>
    /// <remarks>
    /// Written after the headings themselves, and it has to be: a static field is filled in the
    /// order it is declared, and read from above them this was reading a list that did not exist
    /// yet.
    /// </remarks>
    public static IReadOnlyList<string> Groups { get; } =
        [.. All.Select(f => f.Group).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Whether the heading has two answers and wants a dropdown rather than a list of ticks.
    /// </summary>
    /// <remarks>
    /// Yes, no, or neither. Ticking both is the same as ticking neither, which is a thing a set of
    /// tick boxes lets you do and a reader has to work out for themselves - so these say All, Yes
    /// and No and only one at a time.
    /// </remarks>
    public bool YesNo => Key is "preset" or "nomadic" or "second" or "bioship" or "fallen";

    /// <summary>Whether more than one can be held at once, which is what makes "all" worth offering.</summary>
    /// <remarks>
    /// An empire has one authority and any number of civics. Asking for all of two authorities is a
    /// question with no answer, so the headings that can only hold one are not offered the choice.
    ///
    /// Written out, which means it has to be revisited whenever a heading is added - and twice it
    /// was not. Second species traits went without the choice for as long as it existed, and so did
    /// all three of the plan's headings when they arrived.
    /// </remarks>
    public bool Several => Key is
        "ethics" or "civics" or "traits" or "rulertraits" or "secondtraits"
        or "plantraditions" or "planperks" or "plancivics"

        // An empire is given one of these but allows several, so asking for all of two is a
        // question with an answer: the empires either of which it might turn out to be.
        or "personality";

    internal static IReadOnlyList<EmpireChoice> Some(EmpireChoice? choice) =>
        choice is null ? [] : [choice];


    /// <summary>
    /// A heading whose answer is yes or no, as the one choice an empire holds under it.
    /// </summary>
    /// <remarks>
    /// Which makes it the same kind of heading as every other: ticking Yes asks for the empires that
    /// are, ticking No for the ones that are not, and ticking neither - or both - asks for all of
    /// them. Three answers out of the control the other eleven already use, rather than a twelfth
    /// kind of control that only these two would want.
    /// </remarks>
    private static IReadOnlyList<EmpireChoice> YesOrNo(bool held) =>
        [held ? Yes : No];

    private static readonly EmpireChoice Yes = new("yes", "Yes", null, null);

    private static readonly EmpireChoice No = new("no", "No", null, null);
}
