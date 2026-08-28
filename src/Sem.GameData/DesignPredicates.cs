namespace Sem.GameData;

/// <summary>
/// Named conditions about an empire design as a whole, which the game's script asks about through
/// scripted triggers and the rules engine works out from the current selections.
/// </summary>
/// <remarks>
/// Shared between extraction and evaluation so the two cannot drift: the extractor only emits a
/// <see cref="PredicateRequirement"/> for a name listed here, and anything else becomes an
/// <see cref="UnknownRequirement"/> that shows up as a warning instead of being silently wrong.
/// </remarks>
public static class DesignPredicates
{
    /// <summary>The empire is a hive mind or machine intelligence.</summary>
    public const string IsGestalt = "is_gestalt";

    /// <summary>The empire is a hive mind.</summary>
    public const string IsHiveEmpire = "is_hive_empire";

    /// <summary>The empire is a machine intelligence.</summary>
    public const string IsMachineEmpire = "is_machine_empire";

    /// <summary>The founder species is machine but the empire is not a gestalt.</summary>
    public const string IsIndividualMachine = "is_individual_machine";

    /// <summary>The founder species is robotic.</summary>
    public const string IsRobotEmpire = "is_robot_empire";

    /// <summary>The empire uses the corporate authority.</summary>
    public const string IsMegacorp = "is_megacorp";

    /// <summary>The empire's founder species is of the wilderness class.</summary>
    public const string IsWildernessEmpire = "is_wilderness_empire";

    /// <summary>An ordinary playable empire, which anything designed here always is.</summary>
    public const string IsRegularEmpire = "is_regular_empire";

    /// <summary>The empire starts nomadic.</summary>
    public const string IsNomadic = "is_nomadic";

    /// <summary>Every predicate the extractor will emit.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        IsGestalt,
        IsHiveEmpire,
        IsMachineEmpire,
        IsIndividualMachine,
        IsRobotEmpire,
        IsMegacorp,
        IsWildernessEmpire,
        IsRegularEmpire,
        IsNomadic,
    };

    /// <summary>
    /// Conditions that are never true of an empire being designed, because they describe the game
    /// state rather than the design. Compiling these to a constant keeps blocked options from
    /// appearing for reasons the player cannot act on.
    /// </summary>
    public static IReadOnlySet<string> NeverTrueInDesigner { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "is_fallen_empire",
        "is_fallen_empire_machine",
        "is_fallen_empire_spiritualist",
        "is_ai",
        "is_pre_ftl_empire",
        "is_primitive",
        "has_been_the_crisis",
        "is_country_type_awakened_fallen_empire",

        // Flags are set by events during a game. An empire being designed has none yet, so any
        // condition asking about one is false, which is what the game finds at creation too.
        "has_country_flag",
        "has_global_flag",
        "has_planet_flag",
    };

    /// <summary>
    /// Conditions treated as true because the alternative is worse. Being wrong here shows an
    /// option that turns out to be unavailable; being wrong the other way hides one the player
    /// could have had.
    /// </summary>
    public static IReadOnlySet<string> AssumedTrueInDesigner { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // Gates two cosmetic portraits on being signed in to a Paradox account, which this app
        // cannot check and most players are.
        "logged_in_to_pdx_account",
    };
}
