using Sem.Designs;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// One empire flattened into the values a table sorts, filters and shows.
/// </summary>
/// <remarks>
/// <para>
/// Built once and held, because every one of these costs a rules context and the table redraws on
/// every keystroke in the search box. The cards do not need this - a card shows what one empire is
/// and asks the design directly - but a column of governments is ninety derivations, and sorting by
/// one is ninety more per click.
/// </para>
/// <para>
/// Everything an empire chose from a list is a choice rather than a string, even where only one of
/// them can be held. A choice carries the key the design stores, the name it is called by, its icon
/// and what it does - which is the difference between a cell that reads "Fanatic Militarist" and one
/// that shows the game's own icon and says what it costs when you point at it, and between a filter
/// that matches on a word and one that matches on the thing.
/// </para>
/// </remarks>
public sealed record EmpireRow
{
    /// <summary>The empire itself, which is what opening a row edits.</summary>
    public required EmpireDesign Design { get; init; }

    /// <summary>
    /// The game's own entry this was read from, and nothing for one of the player's.
    /// </summary>
    /// <remarks>
    /// Which list a row belongs to, and what opening it has to be given: one of the game's is copied
    /// from its stored text rather than selected, so the summary has to travel with the row.
    /// </remarks>
    public PrescriptedEmpireSummary? Preset { get; init; }

    public required string Name { get; init; }

    /// <summary>The flag, for the cell that names the empire.</summary>
    public EmpireFlag? Flag { get; init; }

    /// <summary>
    /// Every word of the empire somebody typed rather than picked, run together.
    /// </summary>
    /// <remarks>
    /// What the search box reads. A file of empires named for their species, or for their ruler, or
    /// carrying a note in a biography, is the ordinary case - and a box that only read the empire's
    /// own name found none of them. Built once here rather than assembled per keystroke.
    /// </remarks>
    public required string Text { get; init; }

    // ---------------------------------------------------------------- the things that were chosen

    public EmpireChoice? Government { get; init; }

    public EmpireChoice? Authority { get; init; }

    public required IReadOnlyList<EmpireChoice> Ethics { get; init; }

    public required IReadOnlyList<EmpireChoice> Civics { get; init; }

    public EmpireChoice? Origin { get; init; }

    /// <summary>What the founders are, as the game classes them: humanoid, lithoid, machine.</summary>
    public EmpireChoice? SpeciesClass { get; init; }

    public required IReadOnlyList<EmpireChoice> Traits { get; init; }

    /// <summary>What kind of world it starts on, which an origin can change from what is stored.</summary>
    public EmpireChoice? PlanetClass { get; init; }

    public EmpireChoice? StartingSystem { get; init; }

    public EmpireChoice? Shipset { get; init; }

    public EmpireChoice? Advisor { get; init; }

    public EmpireChoice? RulerClass { get; init; }

    public required IReadOnlyList<EmpireChoice> RulerTraits { get; init; }

    // ---------------------------------------------------------------- the things that were typed

    public required string SpeciesName { get; init; }

    public required string PlanetName { get; init; }

    public required string RulerName { get; init; }

    public required string ShipPrefix { get; init; }

    /// <summary>
    /// Reads one empire into a row.
    /// </summary>
    /// <remarks>
    /// Through a view, which is the same thing the card and the showcase are drawn from - so a
    /// government in this table is the government on that card, derived by one piece of code. The
    /// view is thrown away afterwards: it holds a context, and ninety of those kept alive to answer
    /// questions nobody is asking is a great deal of memory for a list.
    /// </remarks>
    public static EmpireRow Read(
        DesignSession session,
        EmpireDesign design,
        PrescriptedEmpireSummary? preset = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(design);

        var view = new EmpireView(session, design);
        var database = session.Data.Database;
        var loc = session.Localizer;

        var name = loc.Name(design.Name, design.Key);
        var species = loc.Name(design.Species.Name, string.Empty);
        var planet = loc.Name(design.PlanetName, string.Empty);
        var ruler = loc.RulerName(design.Ruler, string.Empty);
        var prefix = loc.Name(design.ShipPrefix, string.Empty);

        var government = session.Rules.DeriveGovernment(view.Context);
        var world = database.PlanetClasses.FirstOrDefault(p => p.Key == view.Context.EffectivePlanetClass);
        var initializer = database.Initializers.FirstOrDefault(i => i.Key == design.Initializer);
        var rulerClass = database.LeaderClasses.FirstOrDefault(c => c.Key == design.Ruler.LeaderClass);
        var shipset = view.Shipset;
        var advisor = view.Advisor;

        return new EmpireRow
        {
            Design = design,
            Preset = preset,
            Name = name,
            Flag = design.Flag,

            Text = string.Join(
                " ",
                new[]
                {
                    name,
                    species,
                    loc.Name(design.Species.Plural, string.Empty),
                    loc.Name(design.Species.Adjective, string.Empty),
                    loc.Name(design.Adjective, string.Empty),
                    planet,
                    loc.Name(design.SystemName, string.Empty),
                    ruler,
                    design.Ruler.Title is { } title ? loc.Name(title, string.Empty) : string.Empty,
                    prefix,
                    design.Species.Biography ?? string.Empty,
                    design.Ruler.CustomBiography is { } lore ? loc.Name(lore, string.Empty) : string.Empty,
                }.Where(part => part.Length > 0)),

            Government = government is { } held
                ? new EmpireChoice(held.Key, loc.Text(held.NameKey, Localizer.Prettify(held.Key)), null, null)
                : null,

            Authority = view.Authority is { } authority
                ? new EmpireChoice(
                    authority.Key,
                    loc.Text(authority.NameKey, Localizer.Prettify(authority.Key)),
                    authority.Icon,
                    authority.Effects)
                : null,

            Ethics = [.. view.Ethics],
            Civics = [.. view.Civics],

            Origin = view.Origin is { } origin
                ? new EmpireChoice(
                    origin.Key,
                    loc.Text(origin.NameKey, Localizer.Prettify(origin.Key)),
                    origin.Icon,
                    origin.Effects)
                : null,

            SpeciesClass = design.Species.Class is { Length: > 0 } kind
                ? new EmpireChoice(kind, loc.Text(kind), null, null)
                : null,

            Traits = [.. view.Traits],

            PlanetClass = world is { } home
                ? new EmpireChoice(home.Key, loc.Text(home.Key), home.Icon, null)
                : null,

            // No icon: a starting system is a description of a place rather than a thing with a
            // picture, and the game gives these none.
            StartingSystem = initializer is { } start
                ? new EmpireChoice(
                    start.Key,
                    loc.Text(start.NameKey, Localizer.Prettify(start.Key)),
                    null,
                    null)
                : null,

            Shipset = shipset is { } fleet
                ? new EmpireChoice(
                    fleet.Key,
                    view.CultureName(fleet.Key) ?? Localizer.Prettify(fleet.Key),
                    null,
                    null)
                : null,

            Advisor = advisor is { } voice
                ? new EmpireChoice(
                    voice.Key,
                    loc.Text(voice.NameKey, Localizer.Prettify(voice.Key)),
                    voice.Icon,
                    null)
                : null,

            RulerClass = rulerClass is { } leads
                ? new EmpireChoice(
                    leads.Key,
                    loc.Text(leads.NameKey, Localizer.Prettify(leads.Key)),
                    leads.Icon,
                    null)
                : null,

            RulerTraits = [.. view.RulerTraits],

            SpeciesName = species,
            PlanetName = planet,
            RulerName = ruler,
            ShipPrefix = prefix,
        };
    }
}

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
public sealed record EmpireFacet(
    string Key,
    string Label,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>> Values)
{
    /// <summary>Every heading with a list behind it, which is everything that is picked rather than typed.</summary>
    public static IReadOnlyList<EmpireFacet> All { get; } =
    [
        new("government", "Government", r => Some(r.Government)),
        new("authority", "Authority", r => Some(r.Authority)),
        new("ethics", "Ethics", r => r.Ethics),
        new("civics", "Civics", r => r.Civics),
        new("origin", "Origin", r => Some(r.Origin)),
        new("class", "Species", r => Some(r.SpeciesClass)),
        new("traits", "Traits", r => r.Traits),
        new("homeworld", "Homeworld", r => Some(r.PlanetClass)),
        new("system", "Starting system", r => Some(r.StartingSystem)),
        new("shipset", "Shipset", r => Some(r.Shipset)),
        new("advisor", "Advisor voice", r => Some(r.Advisor)),
        new("rulerclass", "Ruler class", r => Some(r.RulerClass)),
        new("rulertraits", "Ruler traits", r => r.RulerTraits),
    ];

    /// <summary>Whether more than one can be held at once, which is what makes "all" worth offering.</summary>
    /// <remarks>
    /// An empire has one authority and any number of civics. Asking for all of two authorities is a
    /// question with no answer, so the headings that can only hold one are not offered the choice.
    /// </remarks>
    public bool Several => Key is "ethics" or "civics" or "traits" or "rulertraits";

    internal static IReadOnlyList<EmpireChoice> Some(EmpireChoice? choice) =>
        choice is null ? [] : [choice];
}

/// <summary>
/// One column of the empire table: what it is called, and what it reads out of a row.
/// </summary>
/// <remarks>
/// A table rather than markup per column, because three separate things have to agree about what
/// the columns are - the header row, the cells, and the picker that turns them on and off - and
/// three lists of seventeen is three chances to disagree.
/// </remarks>
/// <param name="Key">What the column is remembered under.</param>
/// <param name="Header">What it is called.</param>
/// <param name="OnByDefault">Whether it is shown before anybody has chosen.</param>
/// <param name="Choices">What the cell draws, where the cell is things that were picked.</param>
/// <param name="Line">What the cell says, where it is a line of text somebody typed.</param>
public sealed record EmpireColumn(
    string Key,
    string Header,
    bool OnByDefault,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>>? Choices = null,
    Func<EmpireRow, string>? Line = null)
{
    /// <summary>Every column, in the order they are drawn.</summary>
    /// <remarks>
    /// The name is first and is not in the picker: a table of empires with no empire named is a grid
    /// of adjectives, and there would be no way to get the column back.
    /// </remarks>
    public static IReadOnlyList<EmpireColumn> All { get; } =
    [
        Picked("government", true),
        Picked("authority", true),
        Picked("ethics", true),
        Picked("civics", true),
        Picked("origin", true),
        Picked("class", true),
        new("speciesname", "Species name", false, Line: r => r.SpeciesName),
        Picked("traits", false),
        Picked("homeworld", false),
        new("planet", "Planet", false, Line: r => r.PlanetName),
        Picked("system", false),
        Picked("shipset", false),
        Picked("advisor", false),
        new("ruler", "Ruler", false, Line: r => r.RulerName),
        Picked("rulerclass", false),
        Picked("rulertraits", false),
        new("prefix", "Ship prefix", false, Line: r => r.ShipPrefix),
    ];

    /// <summary>
    /// The column for a heading that is also a filter, named and read the same way as the filter.
    /// </summary>
    /// <remarks>
    /// Written once from the facet, so a column and the control that narrows it can never be called
    /// different things or disagree about what an empire holds.
    /// </remarks>
    private static EmpireColumn Picked(string key, bool onByDefault)
    {
        var facet = EmpireFacet.All.First(f => f.Key == key);

        return new EmpireColumn(facet.Key, facet.Label, onByDefault, facet.Values);
    }

    /// <summary>What the column sorts by, which for several choices is all of them run together.</summary>
    public string Text(EmpireRow row) =>
        Line is { } line ? line(row) : string.Join(", ", Choices!(row).Select(c => c.Name));
}

/// <summary>
/// What the reader has narrowed the lists down to.
/// </summary>
/// <remarks>
/// <para>
/// Held by the page and handed to both tables, so one set of filters narrows the player's empires
/// and the game's together - which is the point of them: the question "who has Fanatic Purifiers"
/// is not a question about one of the two lists.
/// </para>
/// <para>
/// Any within a heading by default, and all across them. Choosing two civics asks for empires with
/// either, and choosing a civic and an ethic asks for empires with both. That is what a set of
/// checkboxes reads as, and the other way round - two civics meaning "both" - makes each extra tick
/// narrow the list towards nothing, which is a filter that punishes you for exploring it. A heading
/// that can hold several can be switched to "all" for exactly the times that is the question.
/// </para>
/// </remarks>
public sealed class EmpireFilter
{
    /// <summary>Anything typed, matched against every word of the empire that was typed rather than picked.</summary>
    public string Search { get; set; } = string.Empty;

    private readonly Dictionary<string, Chosen> _headings = new(StringComparer.Ordinal);

    /// <summary>What is ticked under one heading, which the control for it edits in place.</summary>
    public HashSet<string> Keys(string facet) => Under(facet).Keys;

    /// <summary>Whether the heading wants every one of them rather than any.</summary>
    public bool RequiresAll(string facet) => Under(facet).All;

    public void SetRequiresAll(string facet, bool all) => Under(facet).All = all;

    private Chosen Under(string facet)
    {
        ArgumentException.ThrowIfNullOrEmpty(facet);

        if (!_headings.TryGetValue(facet, out var chosen))
        {
            chosen = new Chosen();
            _headings[facet] = chosen;
        }

        return chosen;
    }

    /// <summary>Whether anything is being asked at all, which is what decides if Clear is offered.</summary>
    public bool Any => Search.Length > 0 || _headings.Values.Any(c => c.Keys.Count > 0);

    /// <summary>How many headings are being asked about, for the line that says so.</summary>
    public int Headings =>
        (Search.Length > 0 ? 1 : 0) + _headings.Values.Count(c => c.Keys.Count > 0);

    public void Clear()
    {
        Search = string.Empty;

        foreach (var chosen in _headings.Values)
        {
            chosen.Keys.Clear();
        }
    }

    public bool Matches(EmpireRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (Search.Length > 0 && !row.Text.Contains(Search, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        foreach (var facet in EmpireFacet.All)
        {
            if (!_headings.TryGetValue(facet.Key, out var chosen) || chosen.Keys.Count == 0)
            {
                continue;
            }

            var held = facet.Values(row);

            var satisfied = chosen.All
                ? chosen.Keys.All(key => held.Any(c => c.Key == key))
                : held.Any(c => chosen.Keys.Contains(c.Key));

            if (!satisfied)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class Chosen
    {
        public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);

        public bool All { get; set; }
    }
}
