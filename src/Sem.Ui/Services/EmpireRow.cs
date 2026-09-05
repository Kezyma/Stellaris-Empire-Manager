using Sem.Designs;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// One empire flattened into the values a table sorts, filters and shows.
/// </summary>
/// <remarks>
/// <para>
/// Built once and held, because every one of these costs a rules context and the table redraws on
/// every keystroke in the filter box. The cards do not need this - a card shows what one empire is
/// and asks the design directly - but a column of governments is ninety derivations, and sorting by
/// one is ninety more per click.
/// </para>
/// <para>
/// The keys are kept beside the names because the two questions are different. A column shows what
/// a civic is called, in whatever language the game is being read in; a filter matches what it is,
/// which is the key the design stores. Matching on the name would break the moment two civics were
/// translated the same way, and would sort a filter's options by a word the design never holds.
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

    /// <summary>The flag and the founder's face, for the cell that names the empire.</summary>
    public EmpireFlag? Flag { get; init; }

    public string? Portrait { get; init; }

    public required string Government { get; init; }

    public EmpireChoice? Authority { get; init; }

    public required IReadOnlyList<EmpireChoice> Ethics { get; init; }

    public required IReadOnlyList<EmpireChoice> Civics { get; init; }

    public EmpireChoice? Origin { get; init; }

    /// <summary>What the founders are, as the game classes them: humanoid, lithoid, machine.</summary>
    public EmpireChoice? SpeciesClass { get; init; }

    public required string SpeciesName { get; init; }

    public required IReadOnlyList<EmpireChoice> Traits { get; init; }

    /// <summary>What kind of world it starts on, which an origin can change from what is stored.</summary>
    public required string PlanetClass { get; init; }

    public required string PlanetName { get; init; }

    public required string StartingSystem { get; init; }

    public required string Shipset { get; init; }

    public required string Advisor { get; init; }

    public required string RulerName { get; init; }

    public required string RulerClass { get; init; }

    public required IReadOnlyList<EmpireChoice> RulerTraits { get; init; }

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

        var rulerClass = database.LeaderClasses.FirstOrDefault(c => c.Key == design.Ruler.LeaderClass);

        return new EmpireRow
        {
            Design = design,
            Preset = preset,
            Name = session.Localizer.Name(design.Name, design.Key),
            Flag = design.Flag,
            Portrait = view.SpeciesPortrait,
            Government = view.Government,
            Authority = view.Authority is { } authority
                ? new EmpireChoice(
                    authority.Key,
                    session.Localizer.Text(authority.NameKey, Localizer.Prettify(authority.Key)),
                    authority.Icon,
                    null)
                : null,
            Ethics = [.. view.Ethics],
            Civics = [.. view.Civics],
            Origin = view.Origin is { } origin
                ? new EmpireChoice(
                    origin.Key,
                    session.Localizer.Text(origin.NameKey, Localizer.Prettify(origin.Key)),
                    origin.Icon,
                    null)
                : null,
            SpeciesClass = design.Species.Class is { Length: > 0 } kind
                ? new EmpireChoice(kind, session.Localizer.Text(kind), null, null)
                : null,
            SpeciesName = session.Localizer.Name(design.Species.Name, string.Empty),
            Traits = [.. view.Traits],
            PlanetClass = session.Localizer.Text(view.Context.EffectivePlanetClass),
            PlanetName = session.Localizer.Name(design.PlanetName, string.Empty),
            StartingSystem = view.StartingSystem,
            Shipset = view.CultureName(view.Shipset?.Key) ?? string.Empty,
            Advisor = view.Advisor is { } advisor
                ? session.Localizer.Text(advisor.NameKey, Localizer.Prettify(advisor.Key))
                : string.Empty,
            RulerName = session.Localizer.RulerName(design.Ruler, string.Empty),
            RulerClass = rulerClass is { } held
                ? session.Localizer.Text(held.NameKey, Localizer.Prettify(held.Key))
                : string.Empty,
            RulerTraits = [.. view.RulerTraits],
            ShipPrefix = session.Localizer.Name(design.ShipPrefix, string.Empty),
        };
    }
}

/// <summary>
/// One column of the empire table: what it is called, and what it reads out of a row.
/// </summary>
/// <remarks>
/// A table rather than markup per column, because three separate things have to agree about what
/// the columns are - the header row, the cells, and the picker that turns them on and off - and
/// three lists of eighteen is three chances to disagree.
/// </remarks>
/// <param name="Key">What the column is remembered under.</param>
/// <param name="Header">What it is called.</param>
/// <param name="Text">The cell, which is also what the column sorts by.</param>
/// <param name="OnByDefault">Whether it is shown before anybody has chosen.</param>
/// <param name="Chips">Whether the cell is a list of choices rather than a line of text.</param>
public sealed record EmpireColumn(
    string Key,
    string Header,
    Func<EmpireRow, string> Text,
    bool OnByDefault,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>>? Chips = null)
{
    /// <summary>Every column, in the order they are drawn.</summary>
    /// <remarks>
    /// The name is first and is not in the picker: a table of empires with no empire named is a
    /// grid of adjectives, and there would be no way to get the column back.
    /// </remarks>
    public static IReadOnlyList<EmpireColumn> All { get; } =
    [
        new("government", "Government", r => r.Government, true),
        new("authority", "Authority", r => r.Authority?.Name ?? string.Empty, true),
        new("ethics", "Ethics", r => Join(r.Ethics), true, r => r.Ethics),
        new("civics", "Civics", r => Join(r.Civics), true, r => r.Civics),
        new("origin", "Origin", r => r.Origin?.Name ?? string.Empty, true),
        new("class", "Species", r => r.SpeciesClass?.Name ?? string.Empty, true),
        new("speciesname", "Species name", r => r.SpeciesName, false),
        new("traits", "Traits", r => Join(r.Traits), false, r => r.Traits),
        new("homeworld", "Homeworld", r => r.PlanetClass, false),
        new("planet", "Planet", r => r.PlanetName, false),
        new("system", "Starting system", r => r.StartingSystem, false),
        new("shipset", "Shipset", r => r.Shipset, false),
        new("advisor", "Advisor voice", r => r.Advisor, false),
        new("ruler", "Ruler", r => r.RulerName, false),
        new("rulerclass", "Ruler class", r => r.RulerClass, false),
        new("rulertraits", "Ruler traits", r => Join(r.RulerTraits), false, r => r.RulerTraits),
        new("prefix", "Ship prefix", r => r.ShipPrefix, false),
    ];

    private static string Join(IReadOnlyList<EmpireChoice> choices) =>
        string.Join(", ", choices.Select(c => c.Name));
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
/// Any within a heading and all across them. Choosing two civics asks for empires with either, and
/// choosing a civic and an ethic asks for empires with both. That is what a set of checkboxes reads
/// as, and the other way round - two civics meaning "both" - makes each extra tick narrow the list
/// towards nothing, which is a filter that punishes you for exploring it.
/// </para>
/// </remarks>
public sealed class EmpireFilter
{
    /// <summary>Part of a name, matched anywhere in it and without regard to case.</summary>
    public string Search { get; set; } = string.Empty;

    public HashSet<string> Authorities { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Ethics { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Civics { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Origins { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Classes { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Traits { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether anything is being asked at all, which is what decides if Clear is offered.</summary>
    public bool Any =>
        Search.Length > 0
        || Authorities.Count > 0
        || Ethics.Count > 0
        || Civics.Count > 0
        || Origins.Count > 0
        || Classes.Count > 0
        || Traits.Count > 0;

    /// <summary>How many headings are being asked about, for the line that says so.</summary>
    public int Headings =>
        (Search.Length > 0 ? 1 : 0)
        + (Authorities.Count > 0 ? 1 : 0)
        + (Ethics.Count > 0 ? 1 : 0)
        + (Civics.Count > 0 ? 1 : 0)
        + (Origins.Count > 0 ? 1 : 0)
        + (Classes.Count > 0 ? 1 : 0)
        + (Traits.Count > 0 ? 1 : 0);

    public void Clear()
    {
        Search = string.Empty;
        Authorities.Clear();
        Ethics.Clear();
        Civics.Clear();
        Origins.Clear();
        Classes.Clear();
        Traits.Clear();
    }

    public bool Matches(EmpireRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return MatchesSearch(row)
            && Holds(Authorities, row.Authority)
            && HoldsAny(Ethics, row.Ethics)
            && HoldsAny(Civics, row.Civics)
            && Holds(Origins, row.Origin)
            && Holds(Classes, row.SpeciesClass)
            && HoldsAny(Traits, row.Traits);
    }

    /// <summary>
    /// Whether the words typed appear in the empire's name, or in its species' name.
    /// </summary>
    /// <remarks>
    /// Both, because a fleet of empires named for their species is the ordinary case and typing
    /// "Blorg" into a box that only reads empire names finds nothing at all.
    /// </remarks>
    private bool MatchesSearch(EmpireRow row) =>
        Search.Length == 0
        || row.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
        || row.SpeciesName.Contains(Search, StringComparison.CurrentCultureIgnoreCase);

    private static bool Holds(HashSet<string> wanted, EmpireChoice? held) =>
        wanted.Count == 0 || (held is not null && wanted.Contains(held.Key));

    private static bool HoldsAny(HashSet<string> wanted, IReadOnlyList<EmpireChoice> held) =>
        wanted.Count == 0 || held.Any(c => wanted.Contains(c.Key));
}
