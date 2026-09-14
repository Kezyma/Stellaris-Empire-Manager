using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// The headings the empire lists can be narrowed by.
/// </summary>
/// <remarks>
/// A table rather than a control per heading, because there are thirty-odd of them and every one is
/// the same control asking the same question of a different field. Adding one is a line here.
/// </remarks>
public static class EmpireFacet
{
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
    public static IReadOnlyList<Facet<EmpireRow>> All { get; } =
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
    /// The headings the filter card leaves to somebody else.
    /// </summary>
    /// <remarks>
    /// Only the preset one, which is the question a reader asks first and most often - mine or the
    /// shelf - so <c>AskedToggle</c> asks it beside the search rather than behind the card's fold.
    /// It stays a heading, and every row still answers it; only where it is asked has changed.
    /// </remarks>
    public static IReadOnlySet<string> AskedElsewhere { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "preset" };

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
    /// A heading an empire holds exactly one of, or none.
    /// </summary>
    /// <remarks>
    /// These three forward to <see cref="Facet{TRow}"/>'s own, and exist for the defaults: without
    /// them every one of the lines above would have to name its tab and say it has no fixed shelf,
    /// which is four dozen lines of saying nothing.
    /// </remarks>
    private static Facet<EmpireRow> One(
        string key,
        string label,
        Func<EmpireRow, EmpireChoice?> value,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions = null,
        string group = Empire) =>
        Facet<EmpireRow>.One(key, label, value, fixedOptions, group);

    /// <summary>A heading an empire may hold any number of.</summary>
    private static Facet<EmpireRow> Many(
        string key,
        string label,
        Func<EmpireRow, IReadOnlyList<EmpireChoice>> values,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions = null,
        string group = Empire) =>
        Facet<EmpireRow>.Many(key, label, values, fixedOptions, group);

    /// <summary>A heading whose answer is yes or no.</summary>
    private static Facet<EmpireRow> Asked(
        string key,
        string label,
        Func<EmpireRow, bool> held,
        string group = Empire) =>
        Facet<EmpireRow>.Asked(key, label, held, group);
}
