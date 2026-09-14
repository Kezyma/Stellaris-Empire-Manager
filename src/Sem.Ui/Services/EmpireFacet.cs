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
public sealed record EmpireFacet
{
    /// <summary>
    /// Private, so a heading can only be declared through one of the three below.
    /// </summary>
    /// <remarks>
    /// Which is the whole guarantee. Left public, a heading could be written out by hand with
    /// neither arity set and nothing would say so - and that omission is exactly the mistake this
    /// arrangement exists to prevent, twice made. Choosing a factory is choosing an answer.
    /// </remarks>
    private EmpireFacet(
        string key,
        string label,
        Func<EmpireRow, IReadOnlyList<EmpireChoice>> values,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions,
        string group)
    {
        Key = key;
        Label = label;
        Values = values;
        Fixed = fixedOptions;
        Group = group;
    }

    /// <summary>What the choice is remembered under, and what the column shares with it.</summary>
    public string Key { get; }

    /// <summary>What the heading is called in the filter card.</summary>
    public string Label { get; }

    /// <summary>What an empire holds under it, which may be none, one or several.</summary>
    public Func<EmpireRow, IReadOnlyList<EmpireChoice>> Values { get; }

    /// <summary>
    /// Every option there is, for a heading whose options are a setting rather than a shelf of game
    /// data - and nothing for the rest, which are offered whatever the empires in front of the
    /// reader actually hold.
    /// </summary>
    public Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? Fixed { get; }

    /// <summary>
    /// Which tab of the filter card it sits on. Twenty-eight controls in one grid is a wall to read
    /// rather than a card to use, and the four groups are the four things an empire is made of.
    /// </summary>
    public string Group { get; }

    /// <summary>Headings about the empire itself.</summary>
    public const string Empire = "Empire";

    /// <summary>Headings about its founders.</summary>
    public const string Species = "Species";

    /// <summary>Headings about whoever leads them.</summary>
    public const string Ruler = "Ruler";

    /// <summary>Headings about where and how it appears in a game.</summary>
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
        Asked("preset", "Preset", r => r.Preset is not null),

        // Derived rather than chosen: a government is what an authority, some ethics and some
        // civics add up to, and the hundred and seventy the game defines are not a list anybody
        // picks from. What is offered is what these empires came to.
        One("government", "Government", r => r.Government),

        // The same kind of thing, and offered the same way: nobody picks a personality either, and
        // what is worth offering is the ones these empires could actually be given rather than all
        // fifty-one the game defines - twenty of which belong to fallen empires and pre-FTL
        // societies and can reach no design at all.
        Many("personality", "AI personality", r => r.Personalities),

        One("authority", "Authority", r => r.Authority, o => o.Authorities),
        Asked("nomadic", "Nomadic", r => r.Nomadic),
        Many("ethics", "Ethics", r => r.Ethics, o => o.Ethics),
        Many("civics", "Civics", r => r.Civics, o => o.Civics),
        One("origin", "Origin", r => r.Origin, o => o.Origins),
        One("spawn", "AI spawning", r => r.Spawn, o => o.Spawning),
        Asked("fallen", "Fallen empire", r => r.Fallen),
        One("flagset", "Special flags", r => r.FlagSet, o => o.FlagSets),
        One("advisor", "Advisor voice", r => r.Advisor, o => o.Advisors),

        One("primary", "Primary flag colour", r => r.Primary, o => o.FlagColors, Colours),
        One("secondary", "Secondary flag colour", r => r.Secondary, o => o.FlagColors, Colours),
        One("tertiary", "Tertiary colour", r => r.Tertiary, o => o.FlagColors, Colours),
        One("shipcolor", "Ship colour", r => r.ShipColor, o => o.FlagColors, Colours),
        One("mapborder", "Map border", r => r.MapBorder, o => o.MapColors, Colours),
        One("mapfill", "Map fill", r => r.MapFill, o => o.MapColors, Colours),

        One("class", "Species", r => r.SpeciesClass, o => o.SpeciesClasses, Species),
        One("portrait", "Portrait", r => r.Portrait, o => o.Portraits, Species),
        One("gender", "Gender", r => r.Gender, o => o.Genders, Species),
        One("namelist", "Name list", r => r.NameList, o => o.NameLists, Species),
        Many("traits", "Traits", r => r.Traits, o => o.Traits, Species),
        Asked("second", "Second species", r => r.HasSecondSpecies, Species),
        One("secondclass", "Second species kind", r => r.SecondClass, o => o.SpeciesClasses, Species),
        Many("secondtraits", "Second species traits", r => r.SecondTraits, o => o.Traits, Species),

        One("rulerclass", "Ruler class", r => r.RulerClass, o => o.RulerClasses, Ruler),
        One("rulerportrait", "Ruler portrait", r => r.RulerPortrait, o => o.Portraits, Ruler),
        One("rulergender", "Ruler gender", r => r.RulerGender, o => o.Genders, Ruler),
        Many("rulertraits", "Ruler traits", r => r.RulerTraits, o => o.RulerTraits, Ruler),

        One("homeworld", "Homeworld", r => r.PlanetClass, o => o.Homeworlds, Galaxy),
        One("system", "Starting system", r => r.StartingSystem, o => o.StartingSystems, Galaxy),
        One("room", "Room", r => r.Room, o => o.Rooms, Galaxy),
        One("shipset", "Shipset", r => r.Shipset, o => o.Shipsets, Galaxy),
        Asked("bioship", "Bioships", r => r.Bioship, Galaxy),

        // The civics one reads the whole government a plan ends with, the greyed ones included:
        // a civic the empire cannot give up is part of what it will be, so a reader asking which
        // empires end up with it should be told about those too.
        Many("plantraditions", "Planned traditions", r => r.PlanTrees, o => o.TraditionTrees, Plan),
        Many("planperks", "Planned perks", r => r.PlanPerks, o => o.AscensionPerks, Plan),
        Many("plancivics", "Planned civics", r => r.PlanCivics, o => o.PlannableCivics, Plan),
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
    public bool YesNo { get; private init; }

    /// <summary>Whether more than one can be held at once, which is what makes "all" worth offering.</summary>
    /// <remarks>
    /// <para>
    /// An empire has one authority and any number of civics. Asking for all of two authorities is a
    /// question with no answer, so the headings that can only hold one are not offered the choice.
    /// </para>
    /// <para>
    /// Declared by the heading rather than matched against a list of keys, which is what this and
    /// <see cref="YesNo"/> both used to be. A written-out list has to be revisited whenever a
    /// heading is added and twice it was not: second species traits went without the choice for as
    /// long as they existed, and so did all three of the plan's headings when they arrived. Worse,
    /// the tests that look like they guard it cannot - every one of them reads the same property
    /// under test, so a heading left out of a list is a heading they all agree about.
    /// </para>
    /// <para>
    /// So the arity moves into the declaration, through <see cref="One"/>, <see cref="Many"/> and
    /// <see cref="Asked"/>. Adding a heading means choosing one of the three, which is the decision
    /// that was being forgotten, and there is no longer a second place that can disagree. The same
    /// move <see cref="EmpireColumn"/> already made for its own keys.
    /// </para>
    /// </remarks>
    public bool Several { get; private init; }

    /// <summary>
    /// A heading an empire holds exactly one of, or none.
    /// </summary>
    /// <param name="key">What the choice is remembered under.</param>
    /// <param name="label">What it is called in the filter card.</param>
    /// <param name="value">The one it holds, where it holds one.</param>
    /// <param name="fixedOptions">Every option there is, for a heading whose options are a setting.</param>
    /// <param name="group">Which tab it sits on.</param>
    private static EmpireFacet One(
        string key,
        string label,
        Func<EmpireRow, EmpireChoice?> value,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions = null,
        string group = Empire) =>
        new(key, label, row => Some(value(row)), fixedOptions, group);

    /// <summary>A heading an empire may hold any number of.</summary>
    private static EmpireFacet Many(
        string key,
        string label,
        Func<EmpireRow, IReadOnlyList<EmpireChoice>> values,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions = null,
        string group = Empire) =>
        new(key, label, values, fixedOptions, group) { Several = true };

    /// <summary>A heading whose answer is yes or no.</summary>
    private static EmpireFacet Asked(
        string key,
        string label,
        Func<EmpireRow, bool> held,
        string group = Empire) =>
        new(key, label, row => YesOrNo(held(row)), fixedOptions: null, group) { YesNo = true };

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
