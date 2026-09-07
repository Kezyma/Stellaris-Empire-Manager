using Sem.Designs;
using Sem.GameData;

namespace Sem.Rules;

/// <summary>
/// A snapshot of an empire design in the terms the rules are written in.
/// </summary>
/// <remarks>
/// Built fresh whenever a selection changes rather than updated in place. The rules are pure
/// functions of this, so there is no cached state to fall out of step with what the player picked.
/// </remarks>
public sealed class DesignContext
{
    private DesignContext(GameDatabase database)
    {
        Database = database;
    }

    /// <summary>The extracted game data the rules are evaluated against.</summary>
    public GameDatabase Database { get; }

    /// <summary>The empire's ethics.</summary>
    public IReadOnlySet<string> Ethics { get; private init; } = new HashSet<string>();

    /// <summary>The empire's authority.</summary>
    public string? Authority { get; private init; }

    /// <summary>
    /// The government the empire's choices add up to, such as <c>gov_star_empire</c>.
    /// </summary>
    /// <remarks>
    /// Not a choice and not stored in the design: it is derived from the authority, the ethics and
    /// the civics, so it is filled in by whoever built the context and is null until they have.
    /// The game's empire-name formats are almost all gated on it, which is what it is here for.
    /// </remarks>
    public string? Government { get; internal set; }

    /// <summary>The empire's civics.</summary>
    // Settable, unlike the ethics and traits beside it, because a plan asks what this empire would
    // look like after a government reform - which is the same design holding different civics.
    public IReadOnlySet<string> Civics { get; private set; } = new HashSet<string>();

    /// <summary>The empire's origin.</summary>
    public string? Origin { get; private init; }

    /// <summary>The founder species' class.</summary>
    public string? SpeciesClass { get; private init; }

    /// <summary>
    /// The founder species' portrait. Some traits accept a portrait in place of a species class,
    /// so this participates in the rules rather than being purely cosmetic.
    /// </summary>
    public string? Portrait { get; private init; }

    /// <summary>The founder species' archetype, worked out from its class.</summary>
    public string? SpeciesArchetype { get; private init; }

    /// <summary>The founder species' traits.</summary>
    public IReadOnlySet<string> Traits { get; private init; } = new HashSet<string>();

    /// <summary>
    /// Whether this context describes the empire's second species rather than its founder.
    /// </summary>
    /// <remarks>
    /// An origin that calls for two species gives them different traits — Syncretic Evolution forces
    /// its subject species to be Proles, not its founders — so which one is being asked about
    /// changes the answer.
    /// </remarks>
    public bool IsSecondarySpecies { get; private init; }

    /// <summary>The homeworld's planet class as the design records it.</summary>
    public string? PlanetClass { get; private init; }

    /// <summary>
    /// The homeworld type that actually applies, which an origin can replace.
    /// </summary>
    /// <remarks>
    /// Ocean Paradise starts its empire on an ocean world and gives the species an ocean climate
    /// whatever the design says, so an Aquatic species with a tropical preference is legal there.
    /// The game accepts such a design and simply uses the origin's value.
    /// </remarks>
    public string? EffectivePlanetClass { get; private init; }

    /// <summary>The empire's ship appearance set.</summary>
    public string? GraphicalCulture { get; private init; }

    /// <summary>Whether the empire starts nomadic.</summary>
    public bool IsNomadic { get; private init; }

    /// <summary>
    /// The class the empire's ruler belongs to, which decides the traits they may hold.
    /// </summary>
    /// <remarks>
    /// A ruler trait names the classes it is for, and the game holds to it. Nothing here read the
    /// ruler at all until now, so every ruler was offered every trait - an official could be given
    /// the commander's warlike and the scientist's spark of genius, neither of which the game would
    /// accept on them.
    /// </remarks>
    public string? RulerClass { get; private init; }

    /// <summary>The traits the ruler holds, which is one of them or none.</summary>
    public IReadOnlySet<string> RulerTraits { get; private init; } = new HashSet<string>();

    /// <summary>
    /// Every trait the founder species actually has, written down or not.
    /// </summary>
    /// <remarks>
    /// <see cref="Traits"/> is what the file says. This is what the empire has, which is not the
    /// same list: a habitability preference is forced by the homeworld and deliberately never
    /// written - see <c>GetWrittenForcedTraits</c> - so an ocean species carries
    /// <c>trait_pc_ocean_preference</c> and its five habitability modifiers with nothing in the file
    /// to say so.
    ///
    /// Filled in by whoever built the context, as <see cref="Government"/> is, because working it
    /// out needs the rules and the context has none. Falls back to the written list until then, so
    /// a context nobody completed still answers sensibly.
    /// </remarks>
    public IReadOnlySet<string> EffectiveTraits
    {
        get => _effectiveTraits ?? Traits;
        internal set => _effectiveTraits = value;
    }

    private IReadOnlySet<string>? _effectiveTraits;

    /// <summary>
    /// The content packs available. Everything gated on a pack is judged against this rather than
    /// against what the extraction machine happened to own.
    /// </summary>
    public IReadOnlySet<string> OwnedDlc { get; private init; } = new HashSet<string>();

    /// <summary>
    /// The ascension perks the plan means to take, which is empty for an empire nobody has planned.
    /// </summary>
    /// <remarks>
    /// Not part of the design and not written by the game. It is here because the game's own
    /// conditions ask about it: almost every rule keeping two perks apart is written as "not if that
    /// one is already taken", and a plan is the only thing that can say whether it is. Empty means
    /// none taken, which makes those conditions pass - which is right.
    /// </remarks>
    public IReadOnlySet<string> AscensionPerks { get; private set; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The tradition trees the plan means to open, on the same terms.</summary>
    public IReadOnlySet<string> TraditionTrees { get; private set; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The same design, asked as though a plan named these.
    /// </summary>
    /// <remarks>
    /// A copy rather than a change, because a context is shared - the page holds one and hands it to
    /// everything that asks a question. The picker needs to ask "what would still be allowed if
    /// these were taken", which is a different question about the same empire, and the answer must
    /// not leak back into the one everybody else is holding.
    /// </remarks>
    /// <param name="perks">The ascension perks the plan names.</param>
    /// <param name="trees">The tradition trees the plan opens.</param>
    /// <param name="civics">
    /// The civics the plan means the empire to end up with, or nothing to keep the ones it starts
    /// with. Civics exclude each other, so the plan's own set is what the game's conditions have to
    /// be asked about - otherwise a planned civic is judged against a government it is replacing.
    /// </param>
    internal DesignContext WithPlan(
        IEnumerable<string> perks,
        IEnumerable<string> trees,
        IEnumerable<string>? civics = null)
    {
        var copy = (DesignContext)MemberwiseClone();

        copy.AscensionPerks = new HashSet<string>(perks, StringComparer.Ordinal);
        copy.TraditionTrees = new HashSet<string>(trees, StringComparer.Ordinal);

        if (civics is not null)
        {
            copy.Civics = new HashSet<string>(civics, StringComparer.Ordinal);
        }

        return copy;
    }

    /// <summary>
    /// True when the empire is a hive mind or a machine intelligence.
    /// </summary>
    /// <remarks>
    /// Read from the ethic's own flag, which the game states as <c>is_gestalt = yes</c> and the
    /// extractor already carries. The key was written out here as well, so the same literal sat on
    /// both sides of the wire with nothing keeping them in step.
    /// </remarks>
    public bool IsGestalt => Ethics.Any(
        e => Database.Ethics.Any(x => x.IsGestalt && string.Equals(x.Key, e, StringComparison.Ordinal)));

    /// <summary>True when the empire is a hive mind.</summary>
    public bool IsHiveEmpire => Authority == "auth_hive_mind";

    /// <summary>True when the empire is a machine intelligence.</summary>
    public bool IsMachineEmpire => Authority == "auth_machine_intelligence";

    /// <summary>True when the founder species is machine but the empire is not a gestalt.</summary>
    public bool IsIndividualMachine => SpeciesArchetype == "MACHINE" && !IsGestalt;

    /// <summary>
    /// True when the founder species is robotic.
    /// </summary>
    /// <remarks>
    /// The game says which archetypes are, in <c>robotic = yes</c>, and the extractor has carried
    /// that flag all along without anything reading it. Two archetype names were listed here
    /// instead, which is the same answer today and the wrong one the moment a pack adds a third.
    /// </remarks>
    public bool IsRobotEmpire =>
        SpeciesArchetype is { } archetype &&
        Database.Archetypes.Any(a => a.IsRobotic && string.Equals(a.Key, archetype, StringComparison.Ordinal));

    /// <summary>True when the empire is a megacorporation.</summary>
    public bool IsMegacorp => Authority == "auth_corporate";

    /// <summary>
    /// A megacorp that took the criminal heritage, which the game asks about by one name.
    /// </summary>
    /// <remarks>
    /// Both halves are things a design holds, so this is answerable here rather than being one more
    /// question only a game in progress could settle.
    /// </remarks>
    public bool IsCriminalSyndicate => IsMegacorp && Civics.Contains("civic_criminal_heritage");

    /// <summary>True when the founder species belongs to the wilderness class.</summary>
    public bool IsWildernessEmpire => SpeciesClass == "WILDERNESS";

    /// <summary>Builds a context from a design.</summary>
    public static DesignContext FromDesign(
        EmpireDesign design,
        GameDatabase database,
        IReadOnlySet<string>? ownedDlc = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(database);

        var speciesClass = design.Species.Class;

        var origin = design.Origin is { } originKey
            ? database.Civics.FirstOrDefault(
                c => c.IsOrigin && string.Equals(c.Key, originKey, StringComparison.Ordinal))
            : null;

        return new DesignContext(database)
        {
            Ethics = new HashSet<string>(design.Ethics, StringComparer.Ordinal),
            Authority = design.Authority,
            Civics = new HashSet<string>(design.Civics, StringComparer.Ordinal),
            Origin = design.Origin,
            SpeciesClass = speciesClass,
            Portrait = design.Species.Portrait,
            SpeciesArchetype = ArchetypeOf(database, speciesClass),
            Traits = new HashSet<string>(design.Species.Traits, StringComparer.Ordinal),
            PlanetClass = design.PlanetClass,
            EffectivePlanetClass =
                origin?.HabitabilityPreference ?? origin?.StartingColony ?? design.PlanetClass,
            GraphicalCulture = design.GraphicalCulture,
            IsNomadic = design.IsNomadic ?? false,
            RulerClass = design.Ruler.LeaderClass,
            RulerTraits = new HashSet<string>(design.Ruler.Traits, StringComparer.Ordinal),
            OwnedDlc = ownedDlc ?? InstalledDlc(database),
        };
    }

    /// <summary>
    /// A copy describing another species of the same empire.
    /// </summary>
    /// <remarks>
    /// Everything the empire decides — its ethics, its civics, its origin, the packs it owns —
    /// belongs to both species and carries over; what changes is the species itself. An origin that
    /// widens the trait allowance widens it for either of them, which is why the civics stay.
    ///
    /// Without this the designer judged the second species by the first: its budget counted the
    /// founders' traits, so the counter never moved however many were added to it, and every
    /// availability answer was about the wrong species.
    /// </remarks>
    public DesignContext ForSpecies(SpeciesDesign species, bool secondary = false)
    {
        ArgumentNullException.ThrowIfNull(species);

        return new DesignContext(Database)
        {
            Ethics = Ethics,
            Authority = Authority,
            Civics = Civics,
            Origin = Origin,

            // The same empire, so the same government: it follows from the choices above, none of
            // which a second species changes.
            Government = Government,
            SpeciesClass = species.Class,
            Portrait = species.Portrait,
            SpeciesArchetype = ArchetypeOf(Database, species.Class),
            Traits = new HashSet<string>(species.Traits, StringComparer.Ordinal),
            IsSecondarySpecies = secondary,
            PlanetClass = PlanetClass,
            EffectivePlanetClass = EffectivePlanetClass,
            GraphicalCulture = GraphicalCulture,
            IsNomadic = IsNomadic,
            OwnedDlc = OwnedDlc,
        };
    }

    /// <summary>Whether the design has selected a given key in a given part of itself.</summary>
    public bool Has(SelectionCategory category, string key) => category switch
    {
        SelectionCategory.Ethics => Ethics.Contains(key),
        SelectionCategory.Authority => Authority == key,
        SelectionCategory.Civics => Civics.Contains(key),
        SelectionCategory.Origin => Origin == key,
        SelectionCategory.SpeciesArchetype => SpeciesArchetype == key,
        SelectionCategory.SpeciesClass => SpeciesClass == key,
        // What the species has, not what the file lists. A habitability preference is forced by the
        // homeworld and deliberately never written down, so asking the written list whether an ocean
        // species has trait_pc_ocean_preference always answered no - and Hydrocentric, whose whole
        // condition is that question, was hidden from every empire that qualifies for it.
        SelectionCategory.Traits => EffectiveTraits.Contains(key),

        // Judged against what actually applies, since an origin can replace the stored value.
        SelectionCategory.PreferredPlanetClass => EffectivePlanetClass == key,
        SelectionCategory.GraphicalCulture => GraphicalCulture == key,

        // An empire being designed is always an ordinary playable country.
        SelectionCategory.CountryType => key == "default",

        // What the plan says is meant to be taken. Empty for a design nobody has planned, which
        // makes every "not if you have that one" condition pass - correctly, since nothing has been
        // taken.
        SelectionCategory.AscensionPerk => AscensionPerks.Contains(key),
        SelectionCategory.TraditionTree => HasTradition(key),
        _ => false,
    };

    /// <summary>
    /// Whether the plan opens a tree, asked either by the tree's name or by one inside it.
    /// </summary>
    /// <remarks>
    /// The game asks both ways and mostly the second: <c>has_tradition = tr_adaptability_recycling</c>
    /// names one pick inside Adaptability, and <c>has_active_tradition</c> beside it does the same.
    /// A plan names trees, so a question about a tradition is a question about the tree it belongs
    /// to - opening one is undertaking to finish it, and every tradition in it comes with that.
    ///
    /// Matched against the tree's own list rather than by trimming the name, because the adoption
    /// and completion bonuses are not written to a pattern and the tree already knows its own.
    /// </remarks>
    private bool HasTradition(string key)
    {
        if (TraditionTrees.Contains(key))
        {
            return true;
        }

        var tree = Database.Traditions.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal))?.Tree;

        return tree is { Length: > 0 } && TraditionTrees.Contains(tree);
    }

    /// <summary>How many things the design has selected in a given part of itself.</summary>
    /// <remarks>
    /// What a <see cref="CountRequirement"/> is compared against. Only the perks and the trees are
    /// ever asked in an unmodified game, but counting is a question every set-valued part of a
    /// design can answer, so they all do rather than the two being special-cased.
    /// </remarks>
    public int Count(SelectionCategory category) => category switch
    {
        SelectionCategory.Ethics => Ethics.Count,
        SelectionCategory.Civics => Civics.Count,
        SelectionCategory.Traits => Traits.Count,
        SelectionCategory.AscensionPerk => AscensionPerks.Count,

        // Only the trees themselves. The set they sit in also holds the tradition that opens each
        // one and the tradition that finishes it, because the game's conditions ask after those by
        // name - so counting the set would say a plan had opened three times as many trees as it
        // had, and "a tree slot must still be free" would run out after two.
        SelectionCategory.TraditionTree =>
            TraditionTrees.Count(k => Database.TraditionTrees.Any(t => t.Key == k)),

        _ => 0,
    };

    /// <summary>Answers a named condition about the design as a whole.</summary>
    public bool Evaluate(string predicate) => predicate switch
    {
        DesignPredicates.IsGestalt => IsGestalt,
        DesignPredicates.IsHiveEmpire => IsHiveEmpire,
        DesignPredicates.IsMachineEmpire => IsMachineEmpire,
        DesignPredicates.IsIndividualMachine => IsIndividualMachine,
        DesignPredicates.IsRobotEmpire => IsRobotEmpire,
        DesignPredicates.IsMegacorp => IsMegacorp,
        DesignPredicates.IsCriminalSyndicate => IsCriminalSyndicate,
        DesignPredicates.IsWildernessEmpire => IsWildernessEmpire,
        DesignPredicates.IsRegularEmpire => true,
        DesignPredicates.IsNomadic => IsNomadic,

        // An unlisted predicate permits the option, matching how unknown conditions are treated.
        _ => true,
    };

    /// <summary>
    /// Whether a predicate is one this design can actually answer.
    /// </summary>
    /// <remarks>
    /// <see cref="Evaluate"/> says yes to anything it does not recognise, which is the right answer
    /// when the question is whether to let the player choose something: an unread condition must
    /// never hide an option. It is the wrong answer when the question is whether a bonus applies,
    /// because that would promise the empire something nobody has established. So the two questions
    /// are asked separately, and this is the second one.
    /// </remarks>
    public static bool Knows(string predicate) => predicate is
        DesignPredicates.IsGestalt or
        DesignPredicates.IsHiveEmpire or
        DesignPredicates.IsMachineEmpire or
        DesignPredicates.IsIndividualMachine or
        DesignPredicates.IsRobotEmpire or
        DesignPredicates.IsMegacorp or
        DesignPredicates.IsCriminalSyndicate or
        DesignPredicates.IsWildernessEmpire or
        DesignPredicates.IsRegularEmpire or
        DesignPredicates.IsNomadic;

    /// <summary>Reads a plain field of the design by the name the game's script uses.</summary>
    public string? Field(string name) => name switch
    {
        "is_nomadic" => IsNomadic ? "yes" : "no",
        "authority" => Authority,
        "government" => Government,
        "origin" => Origin,
        "species_class" => SpeciesClass,
        "species_archetype" => SpeciesArchetype,
        "planet_class" => PlanetClass,
        "graphical_culture" => GraphicalCulture,
        _ => null,
    };

    private static string? ArchetypeOf(GameDatabase database, string? speciesClass) =>
        speciesClass is null
            ? null
            : database.SpeciesClasses
                .FirstOrDefault(c => string.Equals(c.Key, speciesClass, StringComparison.Ordinal))
                ?.Archetype;

    private static HashSet<string> InstalledDlc(GameDatabase database) =>
        [.. database.Dlc.Where(d => d.Installed).Select(d => d.Name)];
}
