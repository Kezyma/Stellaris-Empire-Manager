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
    /// <summary>
    /// The shape this version of the code reads and writes.
    /// </summary>
    /// <remarks>
    /// Kept here rather than with the extractor because both ends need it: the extractor stamps it,
    /// and every host has to check it before trusting what it just read. The web host did not, so a
    /// site published with a database one version behind was read anyway, with whatever the shape had
    /// gained since taking its default and no sign that anything was missing.
    /// </remarks>
    public const int CurrentSchemaVersion = 3;

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

    /// <summary>
    /// The surfaces the game's own empire designer is drawn on, by sprite name.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Icons"/> because they are not pictures of anything: they are the
    /// panels, frames and buttons a window is made of, and most carry a border that must not stretch
    /// when the surface does. Only the designer's own page asks for these; the rest of the app is a
    /// tool in its own chrome and does not want the game's.
    /// </remarks>
    public IReadOnlyDictionary<string, ChromeSprite> Chrome { get; init; } =
        new Dictionary<string, ChromeSprite>();

    /// <summary>
    /// The numbers the game's script names rather than writes, by name without its <c>@</c>.
    /// </summary>
    /// <remarks>
    /// Declared in <c>common/scripted_variables</c> so that a value used in twenty places can be
    /// changed in one. The localisation refers to them too — the Organic trait's tooltip says
    /// <c>$@living_standard_energy_normal|*0$</c> rather than "1" — so the text cannot be read
    /// without them.
    /// </remarks>
    public IReadOnlyDictionary<string, double> ScriptedValues { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// What each of the game's scripted phrases falls back to, by the name the text calls it by.
    /// </summary>
    /// <remarks>
    /// The game's display text can call into script — the Shadows of the Shroud attunement
    /// modifiers read "Add Attunement with <c>[This.GetCradleColor]</c>" — and each of those calls
    /// is a list of conditions with a default at the end. Every condition asks about a game in
    /// progress, so the default is the branch that applies to an empire being designed, and it is
    /// what the game itself would show: an unmet patron is "an Unknown Entity". Without it the call
    /// was deleted and the sentence stopped mid-phrase.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ScriptedText { get; init; } =
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

    /// <summary>The groups the game sorts those sets into for its shipset browser.</summary>
    public IReadOnlyList<ShipSetDefinition> ShipSets { get; init; } = [];

    /// <summary>The kinds of leader the game defines, in the order it defines them.</summary>
    public IReadOnlyList<LeaderClassDefinition> LeaderClasses { get; init; } = [];

    /// <summary>Flag emblem and background categories.</summary>
    public IReadOnlyList<FlagCategoryDefinition> FlagCategories { get; init; } = [];

    /// <summary>The named colours a flag can be tinted with.</summary>
    public IReadOnlyList<FlagColorDefinition> FlagColors { get; init; } = [];

    /// <summary>How a flag is framed at each of the sizes the game draws one.</summary>
    public IReadOnlyList<FlagFrameDefinition> FlagFrames { get; init; } = [];

    /// <summary>The ships a nomadic empire may begin as, in place of a homeworld.</summary>
    public IReadOnlyList<ArkshipDefinition> Arkships { get; init; } = [];

    /// <summary>The weighted word lists the game's empire names are assembled from.</summary>
    public IReadOnlyList<EmpireNamePartsList> EmpireNameParts { get; init; } = [];

    /// <summary>The shapes those words are assembled into, and which empires each suits.</summary>
    public IReadOnlyList<EmpireNameFormat> EmpireNameFormats { get; init; } = [];

    /// <summary>Sets of country flags the game's own empires carry.</summary>
    public IReadOnlyList<EmpireFlagSet> EmpireFlagSets { get; init; } = [];

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

    /// <summary>
    /// Every condition the database holds, at the top of its own tree.
    /// </summary>
    /// <remarks>
    /// Written out one collection at a time rather than found by reflection, so that adding a
    /// definition with a condition on it and forgetting this list is a thing a reader can see. Use
    /// <see cref="Requirement.AndNested"/> to reach the conditions inside each one.
    /// </remarks>
    public IEnumerable<Requirement> Requirements()
    {
        foreach (var speciesClass in SpeciesClasses)
        {
            yield return speciesClass.Playable;
            yield return speciesClass.Possible;
            yield return speciesClass.PossibleSecondary;
        }

        // A modifier can be gated too, and those conditions are as much a part of the rules as the
        // ones deciding whether an option may be taken at all.
        var effects = Traits.Select(t => t.Effects)
            .Concat(Ethics.Select(e => e.Effects))
            .Concat(Authorities.Select(a => a.Effects))
            .Concat(Civics.Select(c => c.Effects));

        foreach (var conditional in effects.SelectMany(e => e.Conditional))
        {
            yield return conditional.When;
        }

        foreach (var authority in Authorities)
        {
            yield return authority.Playable;
            yield return authority.Possible;
        }

        foreach (var civic in Civics)
        {
            yield return civic.Playable;
            yield return civic.Potential;
            yield return civic.Possible;
        }

        foreach (var government in GovernmentTypes)
        {
            yield return government.Possible;
        }

        foreach (var planet in PlanetClasses)
        {
            yield return planet.Potential;
        }

        foreach (var portrait in PortraitSets.SelectMany(s => s.Portraits))
        {
            yield return portrait.Playable;
        }

        foreach (var nameList in NameLists)
        {
            yield return nameList.Selectable;
        }

        foreach (var voice in AdvisorVoices)
        {
            yield return voice.Playable;
        }

        foreach (var culture in GraphicalCultures)
        {
            yield return culture.Selectable;
        }

        foreach (var empire in PrescriptedEmpires)
        {
            yield return empire.Playable;
        }
    }
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

    /// <summary>
    /// How built-up the world in the designer's own preview is, on the game's nought-to-five scale.
    /// </summary>
    /// <remarks>
    /// The game's <c>DEFAULT_CITY_POP_LEVEL</c>, whose line in the defines is commented "Shown in
    /// empire designer" — so this is not a judgement about how a homeworld ought to look but the
    /// number the game itself draws with. It is what keeps the sixth band of city, an ecumenopolis
    /// covering half the frame, off a world that has not earned it.
    /// </remarks>
    public int CityPopLevel { get; init; } = 4;
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
    bool Installed)
{
    /// <summary>Path to the pack's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Whether owning this pack changes anything the designer offers.
    /// </summary>
    /// <remarks>
    /// Eleven do not. Some are obviously beside the point — a soundtrack, a set of forum avatars —
    /// but three are expansions whose content this designer never reaches: Utopia, Synthetic Dawn
    /// and Distant Stars add nothing an empire is built from. A switch that does nothing is worse
    /// than no switch, so the bar leaves them out.
    /// </remarks>
    public bool Decides { get; init; }
}

/// <summary>Which of a portrait's three textures a layer wears.</summary>
/// <remarks>
/// The same three the mesh distinguishes by shader, named here as well so the wardrobe can be read
/// without the model code.
/// </remarks>
public enum PortraitSlot
{
    /// <summary>The body, which carries the skin and the eyes.</summary>
    Character,

    /// <summary>Clothing.</summary>
    Clothes,

    /// <summary>Hair, horns, masks, hats.</summary>
    Attachment,
}

/// <summary>One drawn form of one layer of a portrait.</summary>
/// <param name="Texture">The game's texture this wears, or <c>default</c> where the mesh decides.</param>
/// <param name="Image">Where the picture went, within the extracted assets.</param>
/// <param name="Left">Where its left edge sits in the whole frame, since it has been trimmed.</param>
/// <param name="Top">Where its top edge sits, likewise.</param>
public sealed record PortraitLayerImage(string Texture, string Image, int Left, int Top);

/// <summary>
/// One surface the game draws a window on.
/// </summary>
/// <remarks>
/// The game calls these cornered tiles: a picture drawn at any size by keeping its corners as they
/// are, stretching its edges along one axis and its middle along both. That is nine-slicing, and
/// <see cref="BorderX"/> and <see cref="BorderY"/> are the same numbers CSS wants for
/// <c>border-image-slice</c> — so a surface can be drawn on the web exactly as the game draws it
/// rather than approximated with a solid colour.
///
/// Zero on both means the picture is used whole, which is what a plain <c>spriteType</c> is.
/// </remarks>
/// <param name="Image">Where the picture went, within the extracted assets.</param>
/// <param name="BorderX">How many pixels down each vertical edge are never stretched.</param>
/// <param name="BorderY">How many pixels along each horizontal edge are never stretched.</param>
public sealed record ChromeSprite(string Image, int BorderX, int BorderY);

/// <summary>One layer of a portrait: a run of parts painted together, in every form it takes.</summary>
/// <param name="Slot">Which of the three textures this run wears.</param>
/// <param name="Images">Its forms, the empire designer's first.</param>
public sealed record PortraitLayer(PortraitSlot Slot, IReadOnlyList<PortraitLayerImage> Images);

/// <summary>
/// A portrait's wardrobe, as pictures that stack back into a figure.
/// </summary>
/// <remarks>
/// <para>
/// Layers rather than finished portraits, because the combinations cannot be drawn: one humanoid has
/// eight skins, seven outfits and a hundred hairstyles, and the whole set runs to millions. Drawn
/// one form at a time it is a sum instead of a product.
/// </para>
/// <para>
/// The order is the order they are painted, furthest from the viewer first, and it matters: clothing
/// is painted on both sides of the body, so a humanoid's layers run outfit-back, body, outfit-front,
/// head, hair. Stacking them in any other order puts the coat's back over the chest.
/// </para>
/// </remarks>
/// <summary>
/// One slot's choices, in the order a design's index counts them.
/// </summary>
/// <remarks>
/// Held whole rather than read off the layers, for two reasons. A choice that draws nothing still
/// occupies its number, and leaving it out moved everything after it along by one — the human male
/// portrait offers eighty-seven hairstyles, of which eighty-five draw, and a design storing eighty
/// would have pointed past the end. And the layers of one slot do not all offer the same choices:
/// the same portrait draws its beard from a run of forty and its hair from a run of eighty-five, so
/// there is no one layer the list could be taken from.
/// </remarks>
/// <param name="Slot">Which of the three the list belongs to.</param>
/// <param name="Textures">Every texture the portrait's own selectors offer, in their order.</param>
public sealed record PortraitVariants(PortraitSlot Slot, IReadOnlyList<string> Textures);

/// <param name="Portrait">The portrait's key.</param>
/// <param name="Layers">Its layers, in painting order.</param>
public sealed record PortraitOutfit(string Portrait, IReadOnlyList<PortraitLayer> Layers)
{
    /// <summary>What each slot offers, which is what a design's stored numbers count.</summary>
    public IReadOnlyList<PortraitVariants> Variants { get; init; } = [];

    /// <summary>The texture a slot's stored number names, or null when it names none.</summary>
    public string? TextureFor(PortraitSlot slot, int? index) =>
        Variants.FirstOrDefault(v => v.Slot == slot) is { Textures: { Count: > 0 } textures }
            && index is { } chosen && chosen >= 0 && chosen < textures.Count
            ? textures[chosen]
            : null;

    /// <summary>How many choices a slot offers.</summary>
    public int CountFor(PortraitSlot slot) =>
        Variants.FirstOrDefault(v => v.Slot == slot)?.Textures.Count ?? 0;

    /// <summary>
    /// The skin worn at an ascension stage, where this portrait has one drawn for it.
    /// </summary>
    /// <remarks>
    /// An ascended form is another form of the body layer rather than a layer of its own, because
    /// that is what it is: the same run of parts wearing a skin with the decal already blended in.
    /// So it is asked for the way any other skin is, and the drawing, the trimming and the painting
    /// order need not know that ascension exists.
    ///
    /// Null where the portrait has no artwork for that stage, which is the usual answer: of the
    /// five hundred and forty-six portraits, only the fifty-nine that name their own stages have
    /// any. The rest take the directory default, whose decal is a four-by-four placeholder — an
    /// ordinary portrait does not gain implants, the game swaps it for a cybernetic portrait.
    /// </remarks>
    /// <param name="index">Which skin, as the design stores it.</param>
    /// <param name="stage">Which stage, counting the unascended form as zero.</param>
    public string? AscendedCharacter(int? index, int stage)
    {
        if (stage <= 0 || TextureFor(PortraitSlot.Character, index ?? 0) is not { } skin)
        {
            return null;
        }

        var key = $"{skin}|stage{stage}";

        return Layers.Any(l => l.Slot == PortraitSlot.Character && l.Images.Any(i => i.Texture == key))
            ? key
            : null;
    }
}

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

    // No separate list of trait-budget modifiers. There was one, copied out of the always-on
    // modifiers at extraction, and it lost every bonus the game states inside a swap. The budget is
    // worked out from Effects above, which holds the conditional ones too.

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

    /// <summary>
    /// Path to the larger picture an origin carries, beside its icon.
    /// </summary>
    /// <remarks>
    /// Every origin has one, and in the game it is most of how an origin is presented: a scene of
    /// the world the empire wakes up on. Civics have none.
    /// </remarks>
    public string? Picture { get; init; }

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

    /// <summary>The sky over this world, seen from its surface.</summary>
    public string? Sky { get; init; }

    /// <summary>
    /// The world seen from its own surface: bands of landscape in front of its sky.
    /// </summary>
    /// <remarks>
    /// This is what shows through a room's window. The game builds the view from a sky and up to
    /// four bands of scenery, interleaved with the empire's own city so that hills sit between rows
    /// of towers — which is why the backdrop cannot be one picture. Furthest from the viewer first.
    /// </remarks>
    public IReadOnlyList<SceneryBand> Scenery { get; init; } = [];

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

    /// <summary>
    /// Every likeness the group offers for each gender, in the order the game lists them.
    /// </summary>
    /// <remarks>
    /// <see cref="Members"/> is the one the group shows by default; this is the whole shelf. The
    /// human group offers five male and five female faces, and a design may name any of them
    /// outright — one saved from the game reads <c>portrait = "human_female_05"</c> — so a designer
    /// that only knew the default could neither show nor offer four faces in five.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Phenotypes { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The likenesses offered for a gender, or none where the group lists none.</summary>
    public IReadOnlyList<string> PhenotypesFor(string? gender) =>
        gender is { Length: > 0 } && Phenotypes.TryGetValue(gender, out var faces) ? faces : [];

    /// <summary>True when this key names a group rather than a single portrait.</summary>
    public bool IsGroup => ResolvesTo is not null;

    /// <summary>
    /// The likeness to show for a gender, falling back to the group's default.
    /// </summary>
    public string? For(string? gender) =>
        gender is { Length: > 0 } && Members.TryGetValue(gender, out var member) ? member : ResolvesTo;

    /// <summary>How many skin variants it has, which the design stores as an index.</summary>
    public int TextureCount { get; init; }

    /// <summary>
    /// The ascended forms this portrait can wear, beyond the one it starts in.
    /// </summary>
    /// <remarks>
    /// Read from the portrait's own <c>portrait_evolution</c> where it has one, and otherwise from
    /// the single top-level block in <c>00_portraits_main.txt</c>, which is three: the two stages of
    /// cybernetisation and psionic ascension. Fifty-nine portraits override it — the cybernetic and
    /// Biogenesis ones with two of their own, the psionic and synthetic ones with one.
    ///
    /// Each entry is the asset suffix the stage names — <c>_stage_1</c>, <c>_ascended</c> — or an
    /// empty string where the stage is written as decal and mask paths instead and has no name to
    /// take. The count is the part that matters; the names are for saying which is which.
    /// </remarks>
    public IReadOnlyList<string> EvolutionStages { get; init; } = [];

    /// <summary>Path to the rendered thumbnail within the extracted assets, when one exists.</summary>
    public string? Thumbnail { get; init; }

    /// <summary>
    /// What this portrait calls the thing worn on its head, when it does not call it an attachment.
    /// </summary>
    /// <remarks>
    /// The same slider means something different from one species to the next, and the game says so:
    /// a portrait may declare <c>custom_attachment_label</c> and have the control read "Hairstyle"
    /// for a human, "Hat" for a reptilian, or "Mask". A portrait that declares none leaves the
    /// control saying "Attachments", which is the game's own default.
    /// </remarks>
    public string? AttachmentLabelKey { get; init; }

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

    /// <summary>
    /// The pattern this list numbers its fleets by, where it names none.
    /// </summary>
    /// <remarks>
    /// Sixteen lists work this way, the default human one among them: rather than a pool of names
    /// they carry a template such as "Tähtaailaivasto $R$" and count upwards. A list with this and
    /// no <see cref="FleetNames"/> is not an empty list.
    /// </remarks>
    public string? FleetPattern { get; init; }

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

    /// <summary>
    /// The names a ruler who reigns under a regnal name is drawn from.
    /// </summary>
    /// <remarks>
    /// Sixty-one of the game's seventy-one lists declare these, and none of them was read: the
    /// extractor asked for full, first and second names and stopped. They are a pool of their own —
    /// a list may offer names it uses only for a monarch — so they are kept apart rather than
    /// folded into the ordinary ones.
    /// </remarks>
    public GenderedNames RegnalFirstNames { get; init; } = new();

    /// <inheritdoc cref="RegnalFirstNames" />
    public GenderedNames RegnalSecondNames { get; init; } = new();

    /// <summary>Whether there is anything here to build a name from.</summary>
    public bool IsEmpty => FullNames.IsEmpty && FirstNames.IsEmpty;

    /// <summary>
    /// Names as they would actually appear, joined where the list holds them in parts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list either names its leaders outright or gives first and family names to be put together.
    /// Where it does the second, showing the two pools side by side reads as a jumble, so they are
    /// joined — which is what the game does in its own descriptions of these lists. The limit is
    /// there because the two pools multiply: a hundred first names and a hundred family names are
    /// ten thousand people, and nobody reads a list that long.
    /// </para>
    /// <para>
    /// Joined through <see cref="LeaderName"/> rather than with a space, because a family name is
    /// often a frame written round a given one and the two are not simply set side by side. Written
    /// with a space, a third of the game's lists offered the player names with <c>$1$</c> and
    /// <c>|||masc:</c> still in them.
    /// </para>
    /// </remarks>
    /// <param name="limit">How many to build, at most.</param>
    /// <param name="gender">Whose names these are, where the caller knows.</param>
    public IReadOnlyList<string> Assembled(int limit, string? gender = null)
    {
        var whole = FullNames.All;
        var firsts = FirstNames.All;
        var seconds = SecondNames.All;

        // Capped like everything else. The limit used to guard only the composed names, so a list
        // whose leaders are all written out in full — which the human lists are — returned its whole
        // pool however few the caller asked for.
        var joined = new List<string>(
            whole.Take(limit).Select(name => LeaderName.Variant(name, gender)));

        if (firsts.Count == 0)
        {
            return joined;
        }

        for (var i = 0; i < firsts.Count && joined.Count < limit; i++)
        {
            joined.Add(seconds.Count == 0
                ? LeaderName.Variant(firsts[i], gender)
                : LeaderName.Compose(firsts[i], seconds[i % seconds.Count], gender));
        }

        // The regnal pool after the ordinary one, since a ruler may be styled either way. Appended
        // rather than mixed in, so what a list offers first is what it offered before.
        var regnalFirsts = RegnalFirstNames.All;
        var regnalSeconds = RegnalSecondNames.All;

        for (var i = 0; i < regnalFirsts.Count && joined.Count < limit; i++)
        {
            joined.Add(regnalSeconds.Count == 0
                ? LeaderName.Variant(regnalFirsts[i], gender)
                : LeaderName.Compose(regnalFirsts[i], regnalSeconds[i % regnalSeconds.Count], gender));
        }

        return joined.Distinct(StringComparer.Ordinal).ToList();
    }
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

    /// <summary>
    /// The localisation keys behind each of those names.
    /// </summary>
    /// <remarks>
    /// The game's own files hold keys — <c>SPEC_Rexor</c>, not "Rexor" — and a design that picks one
    /// of these stores the key rather than the word, so that a player reading another language sees
    /// the species named in theirs. Both are needed: the key to write and the text to show.
    /// </remarks>
    public string? NameKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? PluralKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? HomePlanetKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? HomeSystemKey { get; init; }
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
    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The key with <c>_NAME</c> after it, not the key itself — which is why these read as tidied-up
    /// file names ("Custom Starting Init 01") where the game says "Random Trinary I".
    /// </remarks>
    public string NameKey => $"{Key}_NAME";

    /// <summary>Localisation key for the description, which says what shape the system is.</summary>
    public string DescriptionKey => $"{Key}_DESC";
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

    /// <summary>
    /// Whether the game's own empire designer lists this room.
    /// </summary>
    /// <remarks>
    /// Two different things are being kept apart here. A room the designer lists is one the game
    /// expects a custom empire to hold. A room it does not list is not thereby forbidden — most are
    /// handed out during play by conditions no designer could evaluate, and a design that names one
    /// is drawn in it. What would be a fault is offering something the game will not accept, since a
    /// custom empire it refuses does not appear as an error but simply stops being offered.
    /// </remarks>
    public bool IsOffered { get; init; } = true;
}

/// <summary>
/// A named set of country flags a design may carry.
/// </summary>
/// <remarks>
/// <para>
/// Not the picture on the ships — that is the empire flag, and it is drawn. These are markers the
/// game's own script tests for: <c>custom_start_screen</c> is what gives the United Nations of Earth
/// its own opening screen, and the rest gate the events written for that empire. A design names one
/// set, with <c>flag = "empire_human_1"</c>.
/// </para>
/// <para>
/// The game gives them no display names, so they are known by the empires that carry them.
/// </para>
/// </remarks>
/// <param name="Key">The set's name, as a design refers to it.</param>
/// <param name="Flags">The country flags it grants.</param>
public sealed record EmpireFlagSet(string Key, IReadOnlyList<string> Flags)
{
    /// <summary>The built-in empires that carry this set, by their own keys.</summary>
    public IReadOnlyList<string> Empires { get; init; } = [];
}

/// <summary>
/// A kind of leader.
/// </summary>
/// <remarks>
/// Read rather than assumed. The designer only ever needs the three that may rule, which is what the
/// ruler editor used to name outright — but the game declares four in
/// <c>common/leader_classes</c> and marks the envoy as unable to rule, so reading the file gets the
/// same three, in the game's own words, and follows a patch that adds a fifth.
/// </remarks>
/// <param name="Key">The class as a design stores it, such as <c>official</c>.</param>
/// <param name="NameKey">Localisation key for its name.</param>
public sealed record LeaderClassDefinition(string Key, string NameKey)
{
    /// <summary>Whether a leader of this class may be an empire's ruler.</summary>
    public bool CanRule { get; init; } = true;

    /// <summary>Path to its badge within the extracted assets.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// A group the game sorts its shipset browser into.
/// </summary>
/// <remarks>
/// There are two, from <c>common/ship_sets</c>: Biological and Mechanical. A set belongs to the one
/// whose condition its ships answer, so a player comparing appearances is not left to work out which
/// of the twenty are grown rather than built.
/// </remarks>
/// <param name="Key">The group's own name in the script.</param>
/// <param name="NameKey">Localisation key for the heading.</param>
public sealed record ShipSetDefinition(string Key, string NameKey)
{
    /// <summary>The kind of ship this group gathers, or the one it excludes.</summary>
    public string? Category { get; init; }

    /// <summary>Whether the condition is a negation — mechanical is "anything but biological".</summary>
    public bool Inverted { get; init; }

    /// <summary>Whether a set that builds this kind of ship belongs to this group.</summary>
    public bool Includes(string? shipCategory) =>
        string.Equals(shipCategory, Category, StringComparison.Ordinal) != Inverted;
}

/// <summary>
/// One band of an empire's city, and how built-up a world has to be for the game to draw it.
/// </summary>
/// <remarks>
/// The bounds are the game's own, from the <c>planet</c> block of
/// <c>gfx/portraits/portraits/00_portraits_main.txt</c>. They are not decoration: the sixth band is a
/// wall-to-wall ecumenopolis needing a world at five, and drawn alongside the rest it covers the sky,
/// the horizon and every other band at once.
/// </remarks>
/// <param name="Band">Which of the game's bands this is, counting from one.</param>
/// <param name="Image">Where the band lives within the extracted assets.</param>
/// <param name="MinPop">How built-up a world must be before this band appears.</param>
/// <param name="MaxPop">The last level it appears at, or null where the game sets no limit.</param>
public sealed record CityLayer(int Band, string Image, int MinPop, int? MaxPop)
{
    /// <summary>Whether the game would draw this band on a world of the given level.</summary>
    public bool AppearsAt(int level) => level >= MinPop && (MaxPop is not { } max || level <= max);
}

/// <summary>
/// One band of landscape between a world's sky and its city.
/// </summary>
/// <remarks>
/// Numbered rather than merely ordered, because two worlds are missing one: an arctic and a desert
/// world have a first, third and fourth band and no second. Held as a plain list, their third band
/// took the second's place in the interleave and every row of hills after the gap was drawn in front
/// of the towers it belongs behind.
/// </remarks>
/// <param name="Band">Which of the game's bands this is, counting from one.</param>
/// <param name="Image">Where the band lives within the extracted assets.</param>
public sealed record SceneryBand(int Band, string Image);

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

    /// <summary>
    /// The city's own layers, nearest the viewer last, for building the scene behind a portrait.
    /// </summary>
    /// <remarks>
    /// A city is drawn as several bands of buildings at different distances, interleaved with the
    /// planet's own scenery, so it cannot be one flat picture: the hills belong between two rows of
    /// towers. Each band carries the range of world it belongs to, and drawing them all at once is
    /// what buried the planet behind an ecumenopolis.
    /// </remarks>
    public IReadOnlyList<CityLayer> CityLayers { get; init; } = [];

    /// <summary>
    /// Which of a set's ships the game builds — <c>bio_ship</c> for a grown fleet, otherwise
    /// <c>default_ship</c>. Null when the set models no ships of its own.
    /// </summary>
    /// <remarks>
    /// This is what <c>common/ship_sets</c> sorts the picker by, and what tells a shipset from a set
    /// that only dresses cities: Solarpunk and Wilderness declare no <c>ship_kinds</c> and keep no
    /// models, so the game flies them in whatever their fallback builds.
    /// </remarks>
    public string? ShipCategory { get; init; }

    /// <summary>
    /// A drawn ship from this set within the extracted assets, when the set has ships.
    /// </summary>
    /// <remarks>
    /// The game keeps no picture of a shipset — its own picker spins the models — so this is
    /// rendered during extraction rather than copied out of the installation.
    /// </remarks>
    public string? ShipPreview { get; init; }

    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The key shouted, which is how the game names a shipset: <c>BIOGENESIS_01</c> is "Spinovore"
    /// and <c>BIOGENESIS_02</c> is "Shellcraft". Only those two are named — every other set has no
    /// entry under any spelling, and falls back to its key made readable. That is a fact about the
    /// game's text rather than a gap here.
    /// </remarks>
    public string NameKey => Key.ToUpperInvariant();

    /// <summary>
    /// Localisation key for what the game says the set looks like.
    /// </summary>
    /// <remarks>
    /// Every set has one of these — "Sturdy and resolute, these vessels are built to endure the
    /// trials of deep space" — and unlike the name they are all present.
    /// </remarks>
    public string DescriptionKey => $"{Key}_shipset_desc";
}

/// <summary>A folder of flag emblems, or the folder of flag backgrounds.</summary>
public sealed record FlagCategoryDefinition(string Key, bool IsBackground)
{
    /// <summary>The image file names in this category.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => $"FLAG_CATEGORY_{Key}";
}

/// <summary>
/// A ship a nomadic empire begins as, instead of on a planet.
/// </summary>
/// <remarks>
/// A nomad has no homeworld, so the game swaps the planet-class picker for this one: the arkship
/// panel sits inside the same <c>planet_class_editor</c> window, beside the planets rather than
/// anywhere else. Three are offered — a civilian, a science and a military ark — and the game marks
/// exactly those three <c>is_starting_arkship</c>, which is how they are found rather than by name.
/// The higher tiers exist but are built during a game.
/// </remarks>
/// <param name="Key">Such as <c>civilian_arkship_tier_1</c>, as a design stores it.</param>
public sealed record ArkshipDefinition(string Key)
{
    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The game names these by their family rather than their tier: <c>civilian_arkship_tier_1</c>
    /// reads out of <c>civilian_arkship_name</c>, which is itself built from a class word and the
    /// word "Arkship".
    /// </remarks>
    public string NameKey =>
        $"{Key[..(Key.IndexOf("_tier_", StringComparison.Ordinal) is var t and >= 0 ? t : Key.Length)]}_name";

    /// <summary>
    /// The picture the game's own panel shows beside the name.
    /// </summary>
    /// <remarks>
    /// One frame of the ship-size sheet rather than a file of its own, which is why it was missed:
    /// the definition names a sprite, the sprite names a sheet, and the arkships sat as three
    /// unillustrated cards in a panel where every other choice has a picture.
    /// </remarks>
    public string? Icon { get; init; }
}

/// <summary>
/// How the game frames a flag at one of its sizes.
/// </summary>
/// <remarks>
/// <para>
/// A flag is three pictures, not one: an ornamental frame, the coloured field inset inside it under
/// a mask that rounds its corners, and the emblem inset smaller still. Drawing the field edge to
/// edge with the emblem stretched over all of it, which is what happened before, made every emblem
/// about a third larger than the game's.
/// </para>
/// <para>
/// A set of these per size, because the proportions are not constant: the emblem is seven tenths of
/// the field on the largest flag and four fifths on the smallest, so that a small one stays legible.
/// Every measurement here is in the frame's own pixels, which is why <see cref="FrameSize"/> is the
/// number everything else is a fraction of.
/// </para>
/// </remarks>
/// <param name="Key">The sprite's name, such as <c>GFX_empire_flag_128</c>.</param>
/// <param name="FrameSize">How wide the whole thing is, the frame included.</param>
/// <param name="BackgroundOffset">Where the coloured field starts inside the frame.</param>
/// <param name="BackgroundSize">How wide the coloured field is.</param>
/// <param name="EmblemOffset">Where the emblem starts inside the frame.</param>
/// <param name="EmblemSize">How wide the emblem is.</param>
public sealed record FlagFrameDefinition(
    string Key,
    double FrameSize,
    double BackgroundOffset,
    double BackgroundSize,
    double EmblemOffset,
    double EmblemSize)
{
    /// <summary>The ornamental border, within the extracted assets.</summary>
    public string? FrameImage { get; init; }

    /// <summary>The shape the coloured field is cut to, within the extracted assets.</summary>
    public string? MaskImage { get; init; }
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

    /// <summary>The set of country flags it carries, where it carries one.</summary>
    public string? FlagSet { get; init; }

    /// <summary>The room it sits in.</summary>
    public string? Room { get; init; }

    /// <summary>
    /// The key of the paragraph the game writes about it.
    /// </summary>
    /// <remarks>
    /// Every one of the game's empires has one, and it exists only as text: no prescripted country
    /// file mentions it, so it belongs to the game's empire rather than to a copy a player takes,
    /// which has nowhere to keep it.
    /// </remarks>
    public string? DescriptionKey => NameKey is { Length: > 0 } name ? $"{name}_desc" : null;

    /// <summary>
    /// The empire itself, written in the player's own designs format.
    /// </summary>
    /// <remarks>
    /// The game writes its own empires in a shape of their own, and a browser has no installation to
    /// convert them with, so the conversion travels with the data. This is what a player takes a
    /// copy of.
    /// </remarks>
    public string? Design { get; init; }
}
