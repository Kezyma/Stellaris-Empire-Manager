namespace Sem.GameData;

/// <summary>
/// Everything the empire designer needs to know about a Stellaris installation, extracted once and
/// then read without touching the game files again.
/// </summary>
/// <remarks>
/// The desktop app builds this from the player's own install; the web app ships one built at
/// publish time. Both then behave identically, because the designer only ever reads this.
/// </remarks>
public sealed partial record GameDatabase
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
    public const int CurrentSchemaVersion = 17;

    /// <summary>Version of this file's own shape, so an old cache can be detected and rebuilt.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The game version this was extracted from, such as <c>v4.5.0</c>.</summary>
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
    /// The ascension perks, which a design cannot hold but a plan for one can.
    /// </summary>
    /// <remarks>
    /// Here rather than somewhere apart, because the rules read them the same way they read civics:
    /// a list of things with conditions on them, some of which rule others out.
    /// </remarks>
    public IReadOnlyList<AscensionPerkDefinition> AscensionPerks { get; init; } = [];

    /// <summary>The tradition trees, which is the level a plan for one is made at.</summary>
    public IReadOnlyList<TraditionTreeDefinition> TraditionTrees { get; init; } = [];

    /// <summary>The traditions inside those trees, which is what a tree is worth taking for.</summary>
    public IReadOnlyList<TraditionDefinition> Traditions { get; init; } = [];

    /// <summary>
    /// Government types, in the order the game would consider them. The designer derives the
    /// government from the authority, ethics and civics rather than offering it as a choice.
    /// </summary>
    public IReadOnlyList<GovernmentTypeDefinition> GovernmentTypes { get; init; } = [];

    /// <summary>
    /// The personalities the game gives an AI empire, which is what one of these designs becomes
    /// when it turns up in somebody's galaxy.
    /// </summary>
    public IReadOnlyList<PersonalityDefinition> Personalities { get; init; } = [];

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

        foreach (var tree in TraditionTrees)
        {
            yield return tree.Potential;
        }

        foreach (var tradition in Traditions)
        {
            yield return tradition.Possible;

            foreach (var conditional in tradition.Effects.Conditional)
            {
                yield return conditional.When;
            }
        }

        foreach (var perk in AscensionPerks)
        {
            yield return perk.Potential;
            yield return perk.Possible;

            foreach (var conditional in perk.Effects.Conditional)
            {
                yield return conditional.When;
            }
        }

        foreach (var civic in Civics)
        {
            yield return civic.Playable;
            yield return civic.Potential;
            yield return civic.Possible;
            yield return civic.CanAddLater;
            yield return civic.CanRemoveLater;
        }

        foreach (var government in GovernmentTypes)
        {
            yield return government.Possible;
        }

        foreach (var personality in Personalities)
        {
            yield return personality.Allow;

            foreach (var addition in personality.Additions)
            {
                yield return addition.When;
            }
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

        // The condition on a generated empire name. Missed when this list was written, which is the
        // very thing the comment above warns about — so the packs that decide a name format went
        // uncounted and the pruner never kept the text those conditions explain themselves with.
        foreach (var format in EmpireNameFormats)
        {
            yield return format.When;
        }
    }
}
