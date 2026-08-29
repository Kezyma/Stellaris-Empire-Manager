namespace Sem.GameData;

/// <summary>
/// Everything the empire designer needs to know about a Stellaris installation, extracted once and
/// then read without touching the game files again.
/// </summary>
/// <remarks>
/// The desktop app builds this from the player's own install; the web app ships one built at
/// publish time. Both then behave identically, because the designer only ever reads this.
/// </remarks>
public sealed record GameDatabase
{
    /// <summary>Version of this file's own shape, so an old cache can be detected and rebuilt.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The game version this was extracted from, such as <c>v4.4.6</c>.</summary>
    public required string GameVersion { get; init; }

    /// <summary>The extractor build that produced it.</summary>
    public required string ExtractorVersion { get; init; }

    /// <summary>Values from the game's defines that constrain empire creation.</summary>
    public required GameDefines Defines { get; init; }

    /// <summary>Downloadable content, owned or not, keyed by the name the game's script uses.</summary>
    public IReadOnlyList<DlcDefinition> Dlc { get; init; } = [];

    /// <summary>Species archetypes and the trait budgets they grant.</summary>
    public IReadOnlyList<ArchetypeDefinition> Archetypes { get; init; } = [];

    /// <summary>Species classes.</summary>
    public IReadOnlyList<SpeciesClassDefinition> SpeciesClasses { get; init; } = [];

    /// <summary>Species traits, leader traits and starting ruler traits.</summary>
    public IReadOnlyList<TraitDefinition> Traits { get; init; } = [];

    /// <summary>
    /// How to display each modifier the options refer to. Carried in the database because the web
    /// app has no installation of its own to read it from.
    /// </summary>
    public IReadOnlyDictionary<string, ModifierInfo> Modifiers { get; init; } =
        new Dictionary<string, ModifierInfo>();

    /// <summary>Ready-made species the randomise button offers, grouped by species class.</summary>
    public IReadOnlyList<SpeciesNameSuggestion> SpeciesNames { get; init; } = [];

    /// <summary>
    /// The little pictures that appear inside the game's own sentences, by the code that stands for
    /// them. An effects line reading "+10" where the game shows an energy symbol is not the same
    /// sentence, so these travel with the text.
    /// </summary>
    public IReadOnlyDictionary<string, string> TextIcons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Pictures the designer's own controls borrow from the game, by sprite name — the gender
    /// buttons and the like, which belong to no option and so have nowhere else to live.
    /// </summary>
    public IReadOnlyDictionary<string, string> Icons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Ethics, including the fanatic variants and gestalt consciousness.</summary>
    public IReadOnlyList<EthicDefinition> Ethics { get; init; } = [];

    /// <summary>Government authorities.</summary>
    public IReadOnlyList<AuthorityDefinition> Authorities { get; init; } = [];

    /// <summary>Civics and origins, which the game defines together.</summary>
    public IReadOnlyList<CivicDefinition> Civics { get; init; } = [];

    /// <summary>
    /// Government types, in the order the game would consider them. The designer derives the
    /// government from the authority, ethics and civics rather than offering it as a choice.
    /// </summary>
    public IReadOnlyList<GovernmentTypeDefinition> GovernmentTypes { get; init; } = [];

    /// <summary>Planet classes, including which may be a homeworld.</summary>
    public IReadOnlyList<PlanetClassDefinition> PlanetClasses { get; init; } = [];

    /// <summary>The tabs the portrait picker is divided into.</summary>
    public IReadOnlyList<PortraitCategoryDefinition> PortraitCategories { get; init; } = [];

    /// <summary>Groups of portraits, each tied to a species class.</summary>
    public IReadOnlyList<PortraitSetDefinition> PortraitSets { get; init; } = [];

    /// <summary>Individual portraits.</summary>
    public IReadOnlyList<PortraitDefinition> Portraits { get; init; } = [];

    /// <summary>Species name lists.</summary>
    public IReadOnlyList<NameListDefinition> NameLists { get; init; } = [];

    /// <summary>Starting systems that can be chosen or that an origin imposes.</summary>
    public IReadOnlyList<InitializerDefinition> Initializers { get; init; } = [];

    /// <summary>Advisor voices.</summary>
    public IReadOnlyList<AdvisorVoiceDefinition> AdvisorVoices { get; init; } = [];

    /// <summary>Room backgrounds the designer offers.</summary>
    public IReadOnlyList<RoomDefinition> Rooms { get; init; } = [];

    /// <summary>Ship and city appearance sets.</summary>
    public IReadOnlyList<GraphicalCultureDefinition> GraphicalCultures { get; init; } = [];

    /// <summary>Flag emblem and background categories.</summary>
    public IReadOnlyList<FlagCategoryDefinition> FlagCategories { get; init; } = [];

    /// <summary>The named colours a flag can be tinted with.</summary>
    public IReadOnlyList<FlagColorDefinition> FlagColors { get; init; } = [];

    /// <summary>The game's built-in empires, offered as starting points.</summary>
    public IReadOnlyList<PrescriptedEmpireSummary> PrescriptedEmpires { get; init; } = [];

    /// <summary>
    /// The game's blank template, written in the player's own designs format, for starting a new
    /// empire from.
    /// </summary>
    /// <remarks>
    /// The game keeps a template of its own for the same purpose. Beginning from it means a new
    /// empire is playable straight away rather than being an empty shell the player must fill in
    /// before anything makes sense.
    /// </remarks>
    public string? NewEmpireTemplate { get; init; }

    /// <summary>
    /// Conditions the extractor did not recognise, with how often each appeared. A game patch
    /// showing up here is the signal that the extractor needs attention.
    /// </summary>
    public IReadOnlyDictionary<string, int> UnrecognisedTriggers { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// Conditions on modifiers that the extractor did not recognise.
    /// </summary>
    /// <remarks>
    /// Expected rather than alarming, and kept apart for that reason. These are mostly about a game
    /// in progress — whether a tradition has been adopted, whether a planet exists — which has no
    /// answer while an empire is only being designed. The modifiers they gate are shown as
    /// conditional instead of being counted as though they applied.
    /// </remarks>
    public IReadOnlyDictionary<string, int> UnrecognisedEffectConditions { get; init; } =
        new Dictionary<string, int>();
}

/// <summary>Values from the game's defines that constrain empire creation.</summary>
public sealed record GameDefines
{
    /// <summary>Total ethics points available. Three in an unmodified game.</summary>
    public required int EthicsPoints { get; init; }

    /// <summary>How many civics an empire may take. Two in an unmodified game.</summary>
    public required int CivicPoints { get; init; }

    /// <summary>The planet class the city appearance preview defaults to.</summary>
    public string? DefaultCityPreviewPlanetClass { get; init; }
}

/// <summary>A downloadable content pack.</summary>
/// <param name="Folder">Its folder under <c>dlc/</c>, which also indicates whether it is installed.</param>
/// <param name="Name">The name the game's script matches on, such as <c>Utopia</c>.</param>
/// <param name="NameKey">Localisation key for its display name.</param>
/// <param name="Category">Its kind: expansion, story pack, species pack or content pack.</param>
/// <param name="Installed">Whether the pack is present in this installation.</param>
public sealed record DlcDefinition(
    string Folder,
    string Name,
    string? NameKey,
    string? Category,
    bool Installed);

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

/// <summary>How a modifier's value should be displayed.</summary>
/// <param name="IsPercentage">Whether the value is a proportion, shown as a percentage.</param>
/// <param name="IsGood">Whether a larger number is the better one.</param>
/// <param name="IsNeutral">Whether the value is neither good nor bad, and so uncoloured.</param>
/// <param name="Decimals">How many decimal places to show at most.</param>
/// <param name="Declared">
/// Whether the game states this, as opposed to it having been guessed from the modifier's name.
/// Guesses are right more often than not but not always, so it is worth being able to count them.
/// </param>
public sealed record ModifierInfo(
    bool IsPercentage,
    bool IsGood,
    bool IsNeutral,
    int Decimals,
    bool Declared)
{
    /// <summary>What to assume about a modifier nothing is known about.</summary>
    public static ModifierInfo Unknown { get; } = new(false, true, false, 2, false);
}

/// <summary>Modifiers that apply only when a condition holds.</summary>
/// <param name="When">The condition, compiled from the block's <c>potential</c>.</param>
/// <param name="Modifiers">What applies while it holds.</param>
public sealed record ConditionalEffects(Requirement When, IReadOnlyDictionary<string, double> Modifiers);

/// <summary>
/// Everything an option does, as the game would describe it.
/// </summary>
/// <remarks>
/// The game does not simply list an option's modifiers. A hand-written tooltip can replace that
/// list, add to it, or be the only thing there is, and some options hide their numbers entirely.
/// Recording which of those applies is what keeps a democratic authority from showing its two
/// modifiers twice, once from the script and once from the tooltip that restates them.
/// </remarks>
public sealed record EffectSet
{
    /// <summary>An option with nothing to say.</summary>
    public static EffectSet None { get; } = new();

    /// <summary>Modifiers that always apply, as keys and their values.</summary>
    public IReadOnlyDictionary<string, double> Modifiers { get; init; } =
        new Dictionary<string, double>();

    /// <summary>Modifiers that apply only in certain empires.</summary>
    public IReadOnlyList<ConditionalEffects> Conditional { get; init; } = [];

    /// <summary>
    /// Localisation keys naming capabilities rather than numbers, such as being able to use a war
    /// doctrine. Each resolves to a complete sentence.
    /// </summary>
    public IReadOnlyList<string> TagKeys { get; init; } = [];

    /// <summary>Extra text shown under the effects heading, for consequences that are not modifiers.</summary>
    public string? DescriptionKey { get; init; }

    /// <summary>Text shown under a separate penalties heading.</summary>
    public string? PenaltyKey { get; init; }

    /// <summary>A hand-written tooltip that stands in place of, or alongside, the modifier list.</summary>
    public string? TooltipKey { get; init; }

    /// <summary>
    /// Whether <see cref="TooltipKey"/> replaces the modifier list rather than adding to it. The
    /// game's default when a tooltip is declared inside a modifier block, unless it opts out.
    /// </summary>
    public bool TooltipReplacesModifiers { get; init; }

    /// <summary>Whether the option's numbers are deliberately not shown.</summary>
    public bool HideModifiers { get; init; }

    /// <summary>Whether there is anything at all to display.</summary>
    public bool IsEmpty =>
        Modifiers.Count == 0 &&
        Conditional.Count == 0 &&
        TagKeys.Count == 0 &&
        DescriptionKey is null &&
        PenaltyKey is null &&
        TooltipKey is null;

    /// <summary>
    /// The modifiers that should be listed, which is nothing when the option suppresses them or
    /// replaces them with its own wording.
    /// </summary>
    public IReadOnlyDictionary<string, double> VisibleModifiers =>
        HideModifiers || TooltipReplacesModifiers ? new Dictionary<string, double>() : Modifiers;
}

/// <summary>What sort of thing a trait applies to.</summary>
public enum TraitKind
{
    /// <summary>A trait of the founder species.</summary>
    Species,

    /// <summary>A trait the empire's starting ruler may take.</summary>
    StartingRuler,

    /// <summary>A leader trait not offered during empire creation.</summary>
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

    /// <summary>Whether the trait can be chosen during empire creation at all.</summary>
    public bool Initial { get; init; } = true;

    /// <summary>Whether the trait is hidden from the player entirely.</summary>
    public bool Hidden { get; init; }

    /// <summary>Which downloadable content pack this trait needs, if any.</summary>
    public string? RequiredDlc { get; init; }

    /// <summary>Grouping used by the trait list, such as <c>normal</c>, <c>robotic</c> or <c>cyborg</c>.</summary>
    public string? Category { get; init; }

    /// <summary>Ordering hint the game's own trait list uses.</summary>
    public int SortingPriority { get; init; }

    /// <summary>Descriptive tags, used for filtering.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>What this trait does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the trait's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>An ethic.</summary>
public sealed record EthicDefinition(string Key, int Cost, string Category)
{
    /// <summary>
    /// Where this sits within its category. Opposing ethics share a category, so only one may be
    /// taken from each.
    /// </summary>
    public int CategoryValue { get; init; }

    /// <summary>The fanatic form of this ethic. Absent on an ethic that is already fanatic.</summary>
    public string? FanaticVariant { get; init; }

    /// <summary>The ordinary form of this ethic, present only on fanatic ethics.</summary>
    public string? RegularVariant { get; init; }

    /// <summary>True for gestalt consciousness, which cannot be combined with anything.</summary>
    public bool IsGestalt { get; init; }

    /// <summary>True when this is a fanatic ethic, which the game marks by having no fanatic form.</summary>
    public bool IsFanatic => FanaticVariant is null && !IsGestalt;

    /// <summary>What this ethic does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the ethic's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>A government authority.</summary>
public sealed record AuthorityDefinition(string Key)
{
    /// <summary>Whether the authority can be chosen, usually a check on owning an expansion.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>What the rest of the design must look like for this authority to be legal.</summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>Whether this authority is only ever used by the game's own empires.</summary>
    public bool AiOnly { get; init; }

    /// <summary>Traits this authority forces onto the founder species, such as <c>trait_hive_mind</c>.</summary>
    public IReadOnlyList<string> ForcedTraits { get; init; } = [];

    /// <summary>Whether the ruler has an heir.</summary>
    public bool HasHeir { get; init; }

    /// <summary>How rulers are chosen, or <c>none</c>.</summary>
    public string? ElectionType { get; init; }

    /// <summary>What this authority does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the authority's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_tt";
}

/// <summary>A civic, or an origin, which the game defines in the same files.</summary>
public sealed record CivicDefinition(string Key, bool IsOrigin)
{
    /// <summary>Whether this is offered at all, usually a check on owning content.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether this appears in the list for an empire of this shape. A civic that fails here is
    /// hidden rather than shown as blocked.
    /// </summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether this is legal given the rest of the design. A civic that fails here is shown but
    /// blocked, with the game's own explanation.
    /// </summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>Traits this forces onto the founder species.</summary>
    public IReadOnlyList<string> ForcedTraits { get; init; } = [];

    /// <summary>Traits an origin grants that the player may remove again.</summary>
    public IReadOnlyList<string> SoftTraits { get; init; } = [];

    /// <summary>What this does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Extra trait points or picks this grants, keyed by the modifier the game uses.</summary>
    public IReadOnlyDictionary<string, double> TraitBudgetModifiers { get; init; } =
        new Dictionary<string, double>();

    /// <summary>The homeworld type an origin forces, such as a habitat for Void Dwellers.</summary>
    public string? StartingColony { get; init; }

    /// <summary>
    /// The climate an origin gives the founder species regardless of what was picked, such as
    /// ocean for Ocean Paradise. Traits tied to a homeworld type are judged against this.
    /// </summary>
    public string? HabitabilityPreference { get; init; }

    /// <summary>Starting systems an origin restricts the empire to.</summary>
    public IReadOnlyList<string> Initializers { get; init; } = [];

    /// <summary>Homeworld types this adds to the picker.</summary>
    public IReadOnlyList<string> AddedPlanetClasses { get; init; } = [];

    /// <summary>Homeworld types this removes from the picker.</summary>
    public IReadOnlyList<string> RemovedPlanetClasses { get; init; } = [];

    /// <summary>Traits the second species gets, when this origin adds one.</summary>
    public IReadOnlyList<string> SecondarySpeciesTraits { get; init; } = [];

    /// <summary>Whether this origin requires the player to design a second species.</summary>
    public bool RequiresSecondarySpecies { get; init; }

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description of what it does.</summary>
    public string? EffectsKey { get; init; }

    /// <summary>Localisation key for the description of its drawbacks.</summary>
    public string? PenaltiesKey { get; init; }
}

/// <summary>
/// A government type. The game picks the highest-weighted one whose conditions the design meets,
/// which is how an empire ends up called a Divine Empire rather than a Despotic Hegemony.
/// </summary>
public sealed record GovernmentTypeDefinition(string Key, double Weight, int FileOrder)
{
    /// <summary>What the design must look like for this government to apply.</summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>Localisation key for the ruler's title.</summary>
    public string? RulerTitleKey { get; init; }

    /// <summary>Localisation key for the female form of the ruler's title.</summary>
    public string? RulerTitleFemaleKey { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>A planet class.</summary>
public sealed record PlanetClassDefinition(string Key)
{
    /// <summary>Its climate group: dry, wet, cold or otherwise.</summary>
    public string? Climate { get; init; }

    /// <summary>Whether an empire may start here without an origin saying so.</summary>
    public bool IsStartingWorld { get; init; }

    /// <summary>Whether the class can be colonised at all.</summary>
    public bool Colonizable { get; init; }

    /// <summary>What must hold for this to be offered, normally owning a content pack.</summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>A tab in the portrait picker.</summary>
public sealed record PortraitCategoryDefinition(string Key, string NameKey, IReadOnlyList<string> Sets);

/// <summary>A group of portraits belonging to one species class.</summary>
public sealed record PortraitSetDefinition(string Key, string? SpeciesClass)
{
    /// <summary>
    /// The portraits in this set, in the order the game lists them. Order is meaningful: the game
    /// uses empty conditional groups purely to arrange the picker, so this must not be sorted.
    /// </summary>
    public IReadOnlyList<PortraitEntry> Portraits { get; init; } = [];
}

/// <summary>One portrait within a set, with whatever gates its availability.</summary>
/// <param name="Key">The portrait's key, such as <c>mam1</c>.</param>
/// <param name="Playable">What must hold for the player to choose it.</param>
public sealed record PortraitEntry(string Key, Requirement Playable);

/// <summary>An individual portrait, or a group that stands in for one.</summary>
public sealed record PortraitDefinition(string Key)
{
    /// <summary>
    /// The concrete portrait this one's artwork comes from, when this key names a group rather
    /// than a portrait.
    /// </summary>
    /// <remarks>
    /// Portrait sets name groups as freely as they name portraits. A group exists so the game can
    /// pick a different likeness depending on the ruler's gender, and it nominates a default for
    /// when there is nothing to go on. That default is what a designer should show.
    /// </remarks>
    public string? ResolvesTo { get; init; }

    /// <summary>
    /// The likenesses a group offers, by the gender each is for.
    /// </summary>
    /// <remarks>
    /// A group exists so the same choice can show a different face depending on gender, which is
    /// why a design stores the group rather than one of its members — the game's own United Nations
    /// of Earth records <c>portrait = "human"</c>. Keeping the members lets a designer show the
    /// right face without changing what is written to the file.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Members { get; init; } =
        new Dictionary<string, string>();

    /// <summary>True when this key names a group rather than a single portrait.</summary>
    public bool IsGroup => ResolvesTo is not null;

    /// <summary>
    /// The likeness to show for a gender, falling back to the group's default.
    /// </summary>
    public string? For(string? gender) =>
        gender is { Length: > 0 } && Members.TryGetValue(gender, out var member) ? member : ResolvesTo;

    /// <summary>How many skin variants it has, which the design stores as an index.</summary>
    public int TextureCount { get; init; }

    /// <summary>Path to the rendered thumbnail within the extracted assets, when one exists.</summary>
    public string? Thumbnail { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>A species name list.</summary>
public sealed record NameListDefinition(string Key, string? Category)
{
    /// <summary>Whether the player may choose it. A few lists exist only for the game's own use.</summary>
    public Requirement Selectable { get; init; } = new AlwaysRequirement(true);

    /// <summary>Whether it may be picked when an empire asks for any name list at random.</summary>
    public bool Randomized { get; init; } = true;

    /// <summary>
    /// A different name list to draw species, homeworld and system names from.
    /// </summary>
    /// <remarks>
    /// The game's three human lists do this, so that randomising a species for the United Nations of
    /// Earth offers ordinary human species names rather than the empire's own naming conventions.
    /// </remarks>
    public string? RandomNameSource { get; init; }

    /// <summary>Ruler names this list offers, already in the player's language.</summary>
    public NameSet CharacterNames { get; init; } = new();

    /// <summary>Planet names this list offers, already in the player's language.</summary>
    public IReadOnlyList<string> PlanetNames { get; init; } = [];

    /// <summary>Ship names this list offers, used to show what the list sounds like.</summary>
    public IReadOnlyList<string> ShipNames { get; init; } = [];

    /// <summary>Fleet names this list offers.</summary>
    public IReadOnlyList<string> FleetNames { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => $"name_list_{Key}";
}

/// <summary>
/// One kind of name, in the variants a list may offer it.
/// </summary>
/// <remarks>
/// The game's rule, from its own documentation: the list matching the character's gender is used
/// when it holds anything, and the ungendered list stands in when it does not. Every kind of name
/// follows it — a list may give full names by gender and second names without, or the reverse.
/// </remarks>
public sealed record GenderedNames
{
    /// <summary>Names for a character of no particular gender.</summary>
    public IReadOnlyList<string> Any { get; init; } = [];

    /// <summary>Names for a male character.</summary>
    public IReadOnlyList<string> Male { get; init; } = [];

    /// <summary>Names for a female character.</summary>
    public IReadOnlyList<string> Female { get; init; } = [];

    /// <summary>Whether the list offers none of this kind.</summary>
    public bool IsEmpty => Any.Count == 0 && Male.Count == 0 && Female.Count == 0;

    /// <summary>The names to draw on for a character, following the game's rule.</summary>
    public IReadOnlyList<string> For(bool female)
    {
        var gendered = female ? Female : Male;
        return gendered.Count > 0 ? gendered : Any;
    }

    /// <summary>Every name of this kind, for showing what a list sounds like.</summary>
    public IReadOnlyList<string> All =>
        [.. Any.Concat(Male).Concat(Female).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// The names a list offers for a character.
/// </summary>
/// <remarks>
/// A name is either complete in itself or a first name joined to a second. Where a list offers both,
/// the game picks between them evenly.
/// </remarks>
public sealed record NameSet
{
    /// <summary>Names that stand alone.</summary>
    public GenderedNames FullNames { get; init; } = new();

    /// <summary>First names, always joined to a second.</summary>
    public GenderedNames FirstNames { get; init; } = new();

    /// <summary>Second names, always joined to a first.</summary>
    public GenderedNames SecondNames { get; init; } = new();

    /// <summary>Whether there is anything here to build a name from.</summary>
    public bool IsEmpty => FullNames.IsEmpty && FirstNames.IsEmpty;
}

/// <summary>
/// A ready-made species, as the game's own randomise button offers.
/// </summary>
/// <remarks>
/// The game does not invent a species name a piece at a time. It picks one of these, which carries a
/// name, its plural, a homeworld, a home system and the name list that suits it — so pressing
/// randomise fills in five fields at once and they agree with each other.
/// </remarks>
/// <param name="SpeciesClass">The class this suits, such as <c>MAM</c>.</param>
/// <param name="Name">The species name.</param>
public sealed record SpeciesNameSuggestion(string SpeciesClass, string Name)
{
    /// <summary>The plural form.</summary>
    public string? Plural { get; init; }

    /// <summary>A name for the homeworld.</summary>
    public string? HomePlanet { get; init; }

    /// <summary>A name for the home system.</summary>
    public string? HomeSystem { get; init; }

    /// <summary>The name list that goes with it.</summary>
    public string? NameList { get; init; }
}

/// <summary>How a starting system may be used.</summary>
public enum InitializerUsage
{
    /// <summary>Not offered during empire creation.</summary>
    None,

    /// <summary>The player may pick it for any empire.</summary>
    CustomEmpire,

    /// <summary>Only available when an origin names it.</summary>
    Origin,
}

/// <summary>A starting system.</summary>
public sealed record InitializerDefinition(string Key, InitializerUsage Usage)
{
    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>An advisor voice.</summary>
public sealed record AdvisorVoiceDefinition(string Key, string NameKey)
{
    /// <summary>What must hold for the voice to be offered.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// A room background. These have no localised names in the game, so the picker shows the image.
/// </summary>
public sealed record RoomDefinition(string Key)
{
    /// <summary>Path to the image within the extracted assets.</summary>
    public string? Image { get; init; }
}

/// <summary>A ship and city appearance set.</summary>
public sealed record GraphicalCultureDefinition(string Key)
{
    /// <summary>Whether the player may choose it.</summary>
    public Requirement Selectable { get; init; } = new AlwaysRequirement(true);

    /// <summary>The set to fall back to when this one lacks an asset.</summary>
    public string? Fallback { get; init; }

    /// <summary>Whether this set has city artwork, and so can be used as a city appearance.</summary>
    public bool HasCityArt { get; init; }

    /// <summary>A preview of the city artwork within the extracted assets, when there is any.</summary>
    public string? CityPreview { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>A folder of flag emblems, or the folder of flag backgrounds.</summary>
public sealed record FlagCategoryDefinition(string Key, bool IsBackground)
{
    /// <summary>The image file names in this category.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => $"FLAG_CATEGORY_{Key}";
}

/// <summary>A named colour a flag can be tinted with.</summary>
/// <param name="Key">The colour's name, as stored in a design.</param>
/// <param name="Red">Red channel of the flag colour, 0 to 255.</param>
/// <param name="Green">Green channel of the flag colour, 0 to 255.</param>
/// <param name="Blue">Blue channel of the flag colour, 0 to 255.</param>
/// <summary>
/// A named colour a flag can use.
/// </summary>
/// <remarks>
/// One name, three different colours. The game tints a flag, an empire's territory on the galaxy
/// map and its ship trails from the same choice, and the three are not the same shade — "pink" is a
/// muted purple on a flag and a far stronger one on the map. Showing the flag tint in a swatch that
/// chooses a map colour would be quietly misleading, so all three are kept.
/// </remarks>
public sealed record FlagColorDefinition(string Key, byte Red, byte Green, byte Blue)
{
    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapRed { get; init; }

    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapGreen { get; init; }

    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapBlue { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipRed { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipGreen { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipBlue { get; init; }
}

/// <summary>Enough of a built-in empire to list it as a starting point in the designer.</summary>
public sealed record PrescriptedEmpireSummary(string Key, string SourceFile)
{
    /// <summary>Localisation key for its name.</summary>
    public string? NameKey { get; init; }

    /// <summary>Its species class, for showing alongside the entry.</summary>
    public string? SpeciesClass { get; init; }

    /// <summary>Its portrait, for showing alongside the entry.</summary>
    public string? Portrait { get; init; }

    /// <summary>Its authority.</summary>
    public string? Authority { get; init; }

    /// <summary>Its origin.</summary>
    public string? Origin { get; init; }

    /// <summary>The scripted trigger gating whether the game offers it.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);
}
