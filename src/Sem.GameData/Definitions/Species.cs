namespace Sem.GameData;

/// <summary>A species archetype and the trait budget it grants.</summary>
/// <param name="Key">Such as <c>BIOLOGICAL</c>, <c>MACHINE</c> or <c>LITHOID</c>.</param>
/// <param name="TraitPoints">Points available to spend on traits.</param>
/// <param name="MaxTraits">
/// How many traits may be taken. Traits costing nothing do not count against this.
/// </param>
/// <param name="IsRobotic">Whether species of this archetype are machines.</param>
public sealed record ArchetypeDefinition(
    string Key,
    int TraitPoints,
    int MaxTraits,
    bool IsRobotic)
{
    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>A species class.</summary>
/// <param name="Key">Such as <c>MAM</c> or <c>LITHOID</c>.</param>
/// <param name="Archetype">
/// The archetype whose trait budget this class uses, or null for the handful of classes that
/// exist only to contribute a ship or city appearance and are never a species in their own right.
/// </param>
public sealed record SpeciesClassDefinition(string Key, string? Archetype)
{
    /// <summary>
    /// True for a class that supplies appearance but is not a species choice, such as the psionic
    /// and cybernetic ship sets. The game's own files note these are used for their art alone.
    /// </summary>
    public bool IsAppearanceOnly => Archetype is null;

    /// <summary>Whether the class can be chosen at all, usually a check on owning a species pack.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>What the rest of the design must look like for this class to be legal.</summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>The same, for use as an origin's second species.</summary>
    public Requirement PossibleSecondary { get; init; } = new AlwaysRequirement(true);

    /// <summary>A trait every species of this class carries, such as <c>trait_lithoid</c>.</summary>
    public string? ForcedTrait { get; init; }

    /// <summary>The ship and city appearance this class defaults to.</summary>
    public string? GraphicalCulture { get; init; }

    /// <summary>Homeworld types this class adds, such as volcanic worlds for Infernals.</summary>
    public IReadOnlyList<string> AddedPlanetClasses { get; init; } = [];

    /// <summary>Homeworld types this class cannot use.</summary>
    public IReadOnlyList<string> RemovedPlanetClasses { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>What sort of thing a trait applies to.</summary>
public enum TraitKind
{
    /// <summary>A trait of the founder species.</summary>
    Species,

    /// <summary>A trait the empire's starting ruler may take.</summary>
    StartingRuler,

    /// <summary>
    /// A leader trait not offered during empire creation, which is not carried in the database.
    /// </summary>
    /// <remarks>
    /// Classified so that it can be left out. Nothing an empire is designed with can hold one, and
    /// they were a fifth of everything the app downloads.
    /// </remarks>
    Leader,
}

/// <summary>A species or leader trait.</summary>
public sealed record TraitDefinition(string Key, TraitKind Kind)
{
    /// <summary>Points this costs. Negative for drawbacks, which give points back.</summary>
    public int Cost { get; init; }

    /// <summary>Archetypes that may take it. Empty means no restriction.</summary>
    public IReadOnlyList<string> AllowedArchetypes { get; init; } = [];

    /// <summary>Species classes that may take it. Empty means no restriction.</summary>
    public IReadOnlyList<string> AllowedSpeciesClasses { get; init; } = [];

    /// <summary>
    /// Portraits that lift the species-class restriction. A species using one of these may take
    /// the trait whatever its class, which is how the game's own psionic empires carry traits
    /// nominally reserved for the psionic class.
    /// </summary>
    public IReadOnlyList<string> PortraitOverride { get; init; } = [];

    /// <summary>Traits that cannot be taken alongside this one.</summary>
    public IReadOnlyList<string> Opposites { get; init; } = [];

    /// <summary>Homeworld types this trait requires, as Aquatic requires an ocean world.</summary>
    public IReadOnlyList<string> AllowedPlanetClasses { get; init; } = [];

    /// <summary>Origins this trait is limited to.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>Origins that rule this trait out.</summary>
    public IReadOnlyList<string> ForbiddenOrigins { get; init; } = [];

    /// <summary>Ethics this trait requires.</summary>
    public IReadOnlyList<string> AllowedEthics { get; init; } = [];

    /// <summary>Ethics that rule this trait out, such as gestalt for some robotic traits.</summary>
    public IReadOnlyList<string> ForbiddenEthics { get; init; } = [];

    /// <summary>Civics this trait requires.</summary>
    public IReadOnlyList<string> AllowedCivics { get; init; } = [];

    /// <summary>
    /// Leader classes this trait is for, which is how a ruler trait says who may hold it.
    /// </summary>
    /// <remarks>
    /// Empty for a species trait, and for the few leader traits that name no class. The game
    /// enforces the pairing: across the twenty files of empires it ships, all fifty-two that give
    /// their ruler a trait give them a class that trait allows. It was read and discarded once - it
    /// decided whether a trait was a leader's or a species' and was then thrown away - so the
    /// designer offered an official the commander's traits and the scientist's.
    /// </remarks>
    public IReadOnlyList<string> AllowedLeaderClasses { get; init; } = [];

    /// <summary>Whether the trait can be chosen during empire creation at all.</summary>
    public bool Initial { get; init; } = true;

    /// <summary>Whether the trait is hidden from the player entirely.</summary>
    public bool Hidden { get; init; }

    /// <summary>Which downloadable content pack this trait needs, if any.</summary>
    public string? RequiredDlc { get; init; }

    /// <summary>Grouping used by the trait list, such as <c>normal</c>, <c>robotic</c> or <c>cyborg</c>.</summary>
    public string? Category { get; init; }

    /// <summary>What this trait does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the trait's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_desc";
}
