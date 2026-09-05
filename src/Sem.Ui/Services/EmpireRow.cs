using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Components;

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

    /// <summary>
    /// Where the empire sits in the list it came from, counting from one.
    /// </summary>
    /// <remarks>
    /// The file's order, not the table's. Sorted by government, the numbers run about the page in
    /// no order at all - which is the point of showing them: the column says where an empire is
    /// kept, and the table says what it is. Sorting by the number is how you get the file back.
    /// </remarks>
    public int Position { get; init; }

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

    /// <summary>
    /// The authority as the card draws it, which for a nomad is the authority and the nomad marker.
    /// </summary>
    /// <remarks>
    /// Two lists for one heading, because the column and the filter want different things. Nomadic
    /// is not an authority - the game puts it beside them because it rules out a good many civics -
    /// so it belongs in the cell, and offering it among the authorities to filter by would be
    /// offering a fifth thing that is not one of the four.
    /// </remarks>
    public required IReadOnlyList<EmpireChoice> AuthorityChips { get; init; }

    /// <summary>Whether the empire wanders rather than settling, which is a heading of its own.</summary>
    public bool Nomadic { get; init; }

    /// <summary>
    /// Whether its fleet is grown rather than built.
    /// </summary>
    /// <remarks>
    /// The game sorts its own shipset picker by this, and it is the one thing about a set that is
    /// not simply which pictures it uses: a bioship fleet plays differently.
    /// </remarks>
    public bool Bioship { get; init; }

    /// <summary>Whether the design may appear as an AI empire, and how often.</summary>
    public EmpireChoice? Spawn { get; init; }

    /// <summary>Whether the galaxy may generate it as a fallen empire.</summary>
    public bool Fallen { get; init; }

    /// <summary>The set of scripted country flags it claims, where it claims one.</summary>
    public EmpireChoice? FlagSet { get; init; }

    /// <summary>The room the ruler is shown standing in.</summary>
    public EmpireChoice? Room { get; init; }

    /// <summary>The founders' likeness, their gender, and the list their names come from.</summary>
    public EmpireChoice? Portrait { get; init; }

    public EmpireChoice? Gender { get; init; }

    public EmpireChoice? NameList { get; init; }

    /// <summary>The ruler's own likeness and gender, which need not be their people's.</summary>
    public EmpireChoice? RulerPortrait { get; init; }

    public EmpireChoice? RulerGender { get; init; }

    /// <summary>
    /// The second species an origin brought with it, where there is one.
    /// </summary>
    /// <remarks>
    /// Half of some empires and invisible in every column until now: a Syncretic Evolution empire
    /// is as much its servile species as its founders, and nothing on the page said so.
    /// </remarks>
    public bool HasSecondSpecies { get; init; }

    public EmpireChoice? SecondClass { get; init; }

    public required IReadOnlyList<EmpireChoice> SecondTraits { get; init; }

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
    /// <summary>What the game calls a fleet it grows rather than builds.</summary>
    private const string BioFleet = "bio_ship";

    /// <summary>One of the spawn setting's three states as a choice.</summary>
    private static EmpireChoice Chosen((string Value, string Name, string? Icon) state) =>
        new(state.Value, state.Name, state.Icon, null);

    /// <summary>A portrait as a choice, wearing the face it actually resolves to.</summary>
    /// <remarks>
    /// Keyed by what the design stores - the group, usually - so two empires that both say "human"
    /// are one thing to filter by, however differently the gender resolves them.
    /// </remarks>
    private static EmpireChoice? Likeness(DesignSession session, string? key, string? gender) =>
        key is { Length: > 0 }
            ? new EmpireChoice(
                key,
                session.Localizer.Text(key, Localizer.Prettify(key)),
                PortraitArtwork.For(session.Data.Database, key, gender),
                null)
            : null;

    /// <summary>A gender as a choice, wearing the game's own button sprite.</summary>
    private static EmpireChoice? Gendered(DesignSession session, string? gender)
    {
        // The first of the four is what a design holds when it says nothing, which the picker
        // states and is not worth writing down a second time here.
        var held = gender is { Length: > 0 } set ? set : GenderPicker.Offered[0].Key;
        var (label, icon) = GenderPicker.Held(held);

        return new EmpireChoice(
            held,
            label,
            session.Data.Database.Icons.GetValueOrDefault(icon),
            null);
    }

    /// <summary>
    /// A world, described by what living on it would do to the species that does.
    /// </summary>
    /// <remarks>
    /// A planet class says nothing about itself in the game's files - no description, no effects -
    /// so both come from the habitability trait it grants, which is where the homeworld editor gets
    /// them too. Without this the chip had a picture and a name and a panel with nothing in it.
    /// </remarks>
    private static EmpireChoice Habitable(
        DesignSession session,
        GameDatabase database,
        EmpireView view,
        PlanetClassDefinition world)
    {
        var preference = session.Rules.HabitabilityTraitFor(view.Context);
        var trait = preference is null ? null : database.Traits.FirstOrDefault(t => t.Key == preference);

        return new EmpireChoice(
            world.Key,
            session.Localizer.Text(world.Key),
            world.Icon,
            trait?.Effects ?? EffectSet.None)
        {
            Description = preference is { Length: > 0 } ? $"{preference}_desc" : null,
        };
    }

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
                    design.Key,
                    design.Ruler.HeirTitle is { } heir ? loc.Name(heir, string.Empty) : string.Empty,
                    design.SecondarySpecies is { } kin ? loc.Name(kin.Name, string.Empty) : string.Empty,
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

            AuthorityChips = [.. view.AuthorityChoice],
            Nomadic = design.IsNomadic == true,
            Bioship = shipset?.ShipCategory == BioFleet,

            // Always one of the three, never nothing: a design that says nothing about spawning is
            // one that may not spawn, which is what the game makes of a blank and what the toggle
            // on the card already shows. Read as nothing, every empire that had never been asked
            // was missing from the heading entirely.
            Spawn = Chosen(SpawnToggle.Held(session, design.SpawnEnabled)),

            Fallen = design.SpawnAsFallen == true,

            FlagSet = view.FlagSet is { } flags
                ? new EmpireChoice(flags.Key, EmpireView.FlagSetName(session, flags), null, null)
                : null,

            Room = view.Room is { } room
                ? new EmpireChoice(room.Key, loc.Text(room.Key, Localizer.Prettify(room.Key)), null, null)
                : null,

            Portrait = Likeness(session, design.Species.Portrait, design.Species.Gender),
            Gender = Gendered(session, design.Species.Gender),
            NameList = design.Species.NameList is { Length: > 0 } list
                ? new EmpireChoice(list, loc.Text(list, Localizer.Prettify(list)), null, null)
                : null,

            RulerPortrait = Likeness(
                session,
                PortraitArtwork.RulerPortrait(design),
                PortraitArtwork.RulerGender(design)),

            RulerGender = Gendered(session, design.Ruler.Gender),

            HasSecondSpecies = design.SecondarySpecies is not null,

            SecondClass = design.SecondarySpecies?.Class is { Length: > 0 } second
                ? new EmpireChoice(second, loc.Text(second), null, null)
                : null,

            SecondTraits = design.SecondarySpecies is { } other ? [.. view.TraitsOf(other)] : [],

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

            // A nomad's homeworld is its ship. The class underneath still reads pc_ark, because
            // that is what the game records, but a picture of the ark world says nothing about an
            // empire whose whole point is that it does not live on one.
            PlanetClass = design.IsNomadic == true && view.Arkship is { } ark
                ? new EmpireChoice(
                    ark.Key,
                    loc.Text(ark.NameKey, Localizer.Prettify(ark.Key)),
                    ark.Preview is { Length: > 0 } render ? render : ark.Icon,
                    EffectSet.None)
                {
                    // Written out by hand in the same entry as its prose, so composing an effect
                    // set from the ship size would put the same numbers on the panel twice.
                    Description = ark.DescriptionKey,
                }
                : world is { } home
                    ? Habitable(session, database, view, home)
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

            // The game names two shipsets and no more: BIOGENESIS_01 and _02, which Biogenesis
            // added, are the only two of fifty-two with a localisation entry - so every other set
            // is called by its key made readable, and there is nothing to look up that would say
            // otherwise. What each set does have is its own ship render and a description, and
            // both are worth more than a name we would have to invent.
            Shipset = shipset is { } fleet
                ? new EmpireChoice(
                    fleet.Key,
                    view.CultureName(fleet.Key) ?? Localizer.Prettify(fleet.Key),
                    fleet.ShipPreview,
                    EffectSet.None)
                {
                    Description = fleet.DescriptionKey,
                }
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
/// <param name="Fixed">
/// Every option there is, for a heading whose options are a setting rather than a shelf of game
/// data - and nothing for the rest, which are offered whatever the empires in front of the reader
/// actually hold.
/// </param>
public sealed record EmpireFacet(
    string Key,
    string Label,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>> Values,
    Func<DesignSession, IReadOnlyList<EmpireChoice>>? Fixed = null)
{
    /// <summary>Every heading with a list behind it, which is everything that is picked rather than typed.</summary>
    public static IReadOnlyList<EmpireFacet> All { get; } =
    [
        new("preset", "Preset", r => YesOrNo(r.Preset is not null)),
        new("government", "Government", r => Some(r.Government)),
        new("authority", "Authority", r => Some(r.Authority)),
        new("nomadic", "Nomadic", r => YesOrNo(r.Nomadic)),
        new("ethics", "Ethics", r => r.Ethics),
        new("civics", "Civics", r => r.Civics),
        new("origin", "Origin", r => Some(r.Origin)),
        new("class", "Species", r => Some(r.SpeciesClass)),
        new("portrait", "Portrait", r => Some(r.Portrait)),
        new("gender", "Gender", r => Some(r.Gender), Genders),
        new("namelist", "Name list", r => Some(r.NameList)),
        new("traits", "Traits", r => r.Traits),
        new("second", "Second species", r => YesOrNo(r.HasSecondSpecies)),
        new("secondclass", "Second species kind", r => Some(r.SecondClass)),
        new("secondtraits", "Second species traits", r => r.SecondTraits),
        new("homeworld", "Homeworld", r => Some(r.PlanetClass)),
        new("system", "Starting system", r => Some(r.StartingSystem)),
        new("room", "Room", r => Some(r.Room)),
        new("shipset", "Shipset", r => Some(r.Shipset)),
        new("bioship", "Bioships", r => YesOrNo(r.Bioship)),
        new("advisor", "Advisor voice", r => Some(r.Advisor)),
        new("rulerclass", "Ruler class", r => Some(r.RulerClass)),
        new("rulerportrait", "Ruler portrait", r => Some(r.RulerPortrait)),
        new("rulergender", "Ruler gender", r => Some(r.RulerGender), Genders),
        new("rulertraits", "Ruler traits", r => r.RulerTraits),
        new("spawn", "AI spawning", r => Some(r.Spawn), Spawning),
        new("fallen", "Fallen empire", r => YesOrNo(r.Fallen)),
        new("flagset", "Special flags", r => Some(r.FlagSet)),
    ];

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
    /// </remarks>
    public bool Several => Key is "ethics" or "civics" or "traits" or "rulertraits";

    internal static IReadOnlyList<EmpireChoice> Some(EmpireChoice? choice) =>
        choice is null ? [] : [choice];

    /// <summary>The four genders a design may hold, whichever of them anything in the list holds.</summary>
    private static IReadOnlyList<EmpireChoice> Genders(DesignSession session) =>
    [
        .. GenderPicker.Offered.Select(g => new EmpireChoice(
            g.Key, g.Label, session.Data.Database.Icons.GetValueOrDefault(g.Icon), null))
    ];

    /// <summary>The three states of the spawn setting, likewise.</summary>
    private static IReadOnlyList<EmpireChoice> Spawning(DesignSession session) =>
    [
        .. SpawnToggle.Offered(session).Select(s => new EmpireChoice(s.Value, s.Name, s.Icon, null))
    ];

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
        Picked("preset", false),
        Picked("government", true),

        // The one column that is not read straight off its heading: nomadic belongs in the cell
        // beside the authority, where the game puts it and where the card already draws it.
        new("authority", "Authority", true, r => r.AuthorityChips),

        Picked("nomadic", false),
        Picked("ethics", true),
        Picked("civics", true),
        Picked("origin", true),
        Picked("class", true),
        new("speciesname", "Species name", false, Line: r => r.SpeciesName),
        Picked("portrait", false),
        Picked("gender", false),
        Picked("namelist", false),
        Picked("traits", false),
        Picked("second", false),
        Picked("secondclass", false),
        Picked("secondtraits", false),
        Picked("homeworld", false),
        new("planet", "Planet", false, Line: r => r.PlanetName),
        Picked("system", false),
        Picked("room", false),
        Picked("shipset", false),
        Picked("bioship", false),
        Picked("advisor", false),
        new("ruler", "Ruler", false, Line: r => r.RulerName),
        Picked("rulerclass", false),
        Picked("rulerportrait", false),
        Picked("rulergender", false),
        Picked("rulertraits", false),
        new("prefix", "Ship prefix", false, Line: r => r.ShipPrefix),
        Picked("spawn", false),
        Picked("fallen", false),
        Picked("flagset", false),
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
