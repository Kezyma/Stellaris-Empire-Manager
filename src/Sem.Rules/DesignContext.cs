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

    /// <summary>The empire's civics.</summary>
    public IReadOnlySet<string> Civics { get; private init; } = new HashSet<string>();

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
    /// The content packs available. Everything gated on a pack is judged against this rather than
    /// against what the extraction machine happened to own.
    /// </summary>
    public IReadOnlySet<string> OwnedDlc { get; private init; } = new HashSet<string>();

    /// <summary>True when the empire is a hive mind or a machine intelligence.</summary>
    public bool IsGestalt => Ethics.Contains("ethic_gestalt_consciousness");

    /// <summary>True when the empire is a hive mind.</summary>
    public bool IsHiveEmpire => Authority == "auth_hive_mind";

    /// <summary>True when the empire is a machine intelligence.</summary>
    public bool IsMachineEmpire => Authority == "auth_machine_intelligence";

    /// <summary>True when the founder species is machine but the empire is not a gestalt.</summary>
    public bool IsIndividualMachine => SpeciesArchetype == "MACHINE" && !IsGestalt;

    /// <summary>True when the founder species is robotic.</summary>
    public bool IsRobotEmpire => SpeciesArchetype is "MACHINE" or "ROBOT";

    /// <summary>True when the empire is a megacorporation.</summary>
    public bool IsMegacorp => Authority == "auth_corporate";

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

    /// <summary>Returns a copy with a different set of content packs available.</summary>
    public DesignContext WithOwnedDlc(IReadOnlySet<string> ownedDlc) =>
        new(Database)
        {
            Ethics = Ethics,
            Authority = Authority,
            Civics = Civics,
            Origin = Origin,
            SpeciesClass = SpeciesClass,
            Portrait = Portrait,
            SpeciesArchetype = SpeciesArchetype,
            Traits = Traits,
            IsSecondarySpecies = IsSecondarySpecies,
            PlanetClass = PlanetClass,
            EffectivePlanetClass = EffectivePlanetClass,
            GraphicalCulture = GraphicalCulture,
            IsNomadic = IsNomadic,
            OwnedDlc = ownedDlc,
        };

    /// <summary>Whether the design has selected a given key in a given part of itself.</summary>
    public bool Has(SelectionCategory category, string key) => category switch
    {
        SelectionCategory.Ethics => Ethics.Contains(key),
        SelectionCategory.Authority => Authority == key,
        SelectionCategory.Civics => Civics.Contains(key),
        SelectionCategory.Origin => Origin == key,
        SelectionCategory.SpeciesArchetype => SpeciesArchetype == key,
        SelectionCategory.SpeciesClass => SpeciesClass == key,
        SelectionCategory.Traits => Traits.Contains(key),
        // Judged against what actually applies, since an origin can replace the stored value.
        SelectionCategory.PreferredPlanetClass => EffectivePlanetClass == key,
        SelectionCategory.GraphicalCulture => GraphicalCulture == key,

        // An empire being designed is always an ordinary playable country.
        SelectionCategory.CountryType => key == "default",
        _ => false,
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
        DesignPredicates.IsWildernessEmpire => IsWildernessEmpire,
        DesignPredicates.IsRegularEmpire => true,
        DesignPredicates.IsNomadic => IsNomadic,

        // An unlisted predicate permits the option, matching how unknown conditions are treated.
        _ => true,
    };

    /// <summary>Reads a plain field of the design by the name the game's script uses.</summary>
    public string? Field(string name) => name switch
    {
        "is_nomadic" => IsNomadic ? "yes" : "no",
        "authority" => Authority,
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
