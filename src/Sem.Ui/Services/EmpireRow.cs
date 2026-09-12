using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
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

    /// <summary>
    /// The personalities the game could give this empire, each carrying how likely it is.
    /// </summary>
    /// <remarks>
    /// Several, because the game draws rather than decides, and ordered with the likeliest first -
    /// so a column of these reads as an answer even where only the first cell's worth is visible,
    /// and sorting by it sorts by what the empire would most likely be played as.
    /// </remarks>
    public IReadOnlyList<EmpireChoice> Personalities { get; init; } = [];

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

    /// <summary>
    /// The six colour slots, each in the shade the thing it paints will actually be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map's two and the fleet's one are the <em>drawn</em> colour rather than the held one: an
    /// empire with the override off still has a border and a fill, and they come from the flag. A
    /// column that read the slots literally would say nothing for seven empires in eight and would
    /// be wrong about all of them - see <c>EmpireFlag.DrawnMapBorder</c>.
    /// </para>
    /// <para>
    /// The flag's three are held, because there is nothing to derive: what the slot says is what the
    /// background is tinted with.
    /// </para>
    /// </remarks>
    public EmpireChoice? Primary { get; init; }

    /// <inheritdoc cref="Primary"/>
    public EmpireChoice? Secondary { get; init; }

    /// <inheritdoc cref="Primary"/>
    public EmpireChoice? Tertiary { get; init; }

    /// <inheritdoc cref="Primary"/>
    public EmpireChoice? ShipColor { get; init; }

    /// <inheritdoc cref="Primary"/>
    public EmpireChoice? MapBorder { get; init; }

    /// <inheritdoc cref="Primary"/>
    public EmpireChoice? MapFill { get; init; }

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

    /// <summary>
    /// What the plan says the empire means to become, where it carries one.
    /// </summary>
    /// <remarks>
    /// Columns rather than filters. A plan is a thing a reader wants to see beside the empires that
    /// have one - which is the whole point of a table - and it is not a thing anyone narrows a list
    /// by, since almost no empire in a file has one at all.
    /// </remarks>
    public required IReadOnlyList<EmpireChoice> PlanTrees { get; init; }

    public required IReadOnlyList<EmpireChoice> PlanPerks { get; init; }

    public required IReadOnlyList<EmpireChoice> PlanCivics { get; init; }

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

    /// <summary>What the game calls a fleet it grows rather than builds.</summary>
    private const string BioFleet = "bio_ship";

    /// <summary>One of the spawn setting's three states as a choice.</summary>
    private static EmpireChoice Chosen((string Value, string Name, string? Icon) state) =>
        new(state.Value, state.Name, state.Icon, null);

    /// <summary>One of the game's named colours, drawn in the shade the caller asks for.</summary>
    /// <remarks>
    /// <para>
    /// Every name carries three: <c>red</c> is 158,22,22 on a flag, 151,14,18 on the map and
    /// 255,57,36 on a hull. A swatch shown in the wrong one is the wrong colour, and since 48 of the
    /// 72 have the same flag and map value it is an error that hides.
    /// </para>
    /// <para>
    /// Shared by the columns and the filters, so a colour in a cell and the same colour in the menu
    /// that narrows to it can never disagree.
    /// </para>
    /// </remarks>
    public static EmpireChoice? Coloured(
        DesignSession session,
        string? key,
        Func<FlagColorDefinition, (byte R, byte G, byte B)> shade)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(shade);

        if (key is not { Length: > 0 } named)
        {
            return null;
        }

        var color = session.Data.Database.FlagColor(named);

        return new EmpireChoice(named, Localizer.Prettify(named), null, null)
        {
            Swatch = color is null ? null : Swatches.Css(shade(color)),
        };
    }

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
        var trait = preference is null ? null : database.Trait(preference);

        return new EmpireChoice(
            world.Key,
            session.Localizer.Text(world.Key),
            world.Icon,
            trait?.Effects ?? EffectSet.None)
        {
            Description = preference is { Length: > 0 } ? $"{preference}_desc" : null,
        };
    }

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

        var government = session.Rules.GovernmentFor(view.Context);
        var world = database.PlanetClass(view.Context.EffectivePlanetClass);
        var initializer = database.Initializer(design.Initializer);
        var rulerClass = database.LeaderClass(design.Ruler.LeaderClass);
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

            // The card's own answer, taken rather than worked out again: it is memoised on the view
            // and reads the same context the government above was derived from.
            Personalities = view.Personalities,

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

            Primary = Coloured(session, design.Flag.Primary, Swatches.FlagShade),
            Secondary = Coloured(session, design.Flag.Secondary, Swatches.FlagShade),
            Tertiary = Coloured(session, design.Flag.Tertiary, Swatches.FlagShade),
            ShipColor = Coloured(session, design.Flag.DrawnShipColor, Swatches.FlagShade),
            MapBorder = Coloured(session, design.Flag.DrawnMapBorder, Swatches.MapShade),
            MapFill = Coloured(session, design.Flag.DrawnMapFill, Swatches.MapShade),

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
            PlanTrees = [.. view.PlanTrees],
            PlanPerks = [.. view.PlanPerks],

            PlanCivics = [.. view.PlanCivics],

            // Through the view, which names it against this row's own empire: a nomad's Arc Welders
            // is called something else by the game, and every row here is a different empire.
            Origin = view.OriginChoice.FirstOrDefault(),

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
