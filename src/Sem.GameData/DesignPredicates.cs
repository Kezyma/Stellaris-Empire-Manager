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

    /// <summary>A megacorp built on crime, which is the corporate authority and one civic.</summary>
    public const string IsCriminalSyndicate = "is_criminal_syndicate";

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
        IsCriminalSyndicate,
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

        // Raiders the galaxy generates. The empire names are gated on not being one, which is a
        // thing a player's empire never is.
        "is_pirate",
        "is_country_type_awakened_fallen_empire",

        // Flags are set by events during a game. An empire being designed has none yet, so any
        // condition asking about one is false, which is what the game finds at creation too.
        "has_country_flag",
        "has_global_flag",
        "has_planet_flag",

        // Things a game in progress has and a design does not. The ascension perks ask about all of
        // these, because they are written to be judged in the middle of a game - and at the point an
        // empire is being planned, none of them has happened yet.
        "country_has_situation",
        "any_situation",
        "any_relation",
        "has_technology",

        // The crisis path's own perks, which are a separate track a design cannot be on.
        "has_menace_perk",

        // Positions and relationships the galaxy hands out once a game is running.
        "is_subject",
        "is_galactic_custodian",
        "is_galactic_emperor",

        // Scopes over things a design has none of: pops, fleets, other countries.
        "exists",
        "species",
        "any_owned_pop_group",
        "uses_ship_category",
    };

    /// <summary>
    /// Conditions a plan may assume it will have satisfied by the time it gets there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are all in <see cref="NeverTrueInDesigner"/> as well, and both answers are right in
    /// their own place. A civic asking <c>has_technology</c> is asking about the empire being
    /// created, which has researched nothing - so false. An ascension perk asking it is asking
    /// about an empire thirty years in, and answering false there means World Shaper, Colossus and
    /// the Archaeo-Engineers can never be planned at all, which is not a rule but a refusal to
    /// answer the question.
    /// </para>
    /// <para>
    /// So this set is consulted only while compiling the things a plan names, and what it produces
    /// is an <see cref="UnknownRequirement"/> assumed true rather than a plain true. The difference
    /// shows through negation: <c>NOT = { has_technology = x }</c> has to stay permissive too, and
    /// an unknown does that where a constant would invert into a block.
    /// </para>
    /// <para>
    /// The line is what an empire could come to be, against what it definitionally is not. A player
    /// empire may research a technology, build a megastructure, be made a subject or become the
    /// crisis; it is never a fallen empire, a pre-FTL civilisation, a pirate or an AI, and those
    /// stay flatly false because they are facts about this design rather than about its future.
    /// </para>
    /// <para>
    /// Country flags belong here and it took a wrong turn to be sure of it. The Purity and Mutation
    /// trees are gated on a NOR listing the other ascensions and
    /// <c>clone_army_full_potential</c> among them, and reading that flag as satisfied made both
    /// trees vanish for every empire in the game - which looked like a reason to keep flags false,
    /// and was really a defect in how the answer combined. An unsettled term must not decide the
    /// group it sits in, in either direction; once it does not, the exclusion still fires on the
    /// ascensions it can read and the flag stops mattering.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> UnknowableWhenPlanning { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // Things a game grants over time.
        "has_technology",
        "has_been_the_crisis",
        "has_menace_perk",

        // Things events set, including the megastructure flag that Galactic Wonders asks for and
        // that no design could ever answer yes to.
        "has_country_flag",
        "has_global_flag",
        "has_planet_flag",

        // Situations and standings, which need a galaxy to be in.
        "country_has_situation",
        "any_situation",
        "any_relation",
        "is_subject",
        "is_galactic_custodian",
        "is_galactic_emperor",

        // Scopes over what a running empire has and a design does not: pops, fleets, other
        // countries. Compiled to a flat no, these threw away the condition inside them as well.
        "exists",
        "species",
        "any_owned_pop_group",
        "uses_ship_category",
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
