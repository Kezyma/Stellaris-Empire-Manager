using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// One of the empires the game ships in <c>prescripted_countries/</c>.
/// </summary>
/// <remarks>
/// These use a different dialect from the player's designs file: entries are keyed by a bare
/// identifier, names are plain localisation keys rather than the structured form, and values are
/// often unquoted. The two share a tokenizer but not a mapping, so they are modelled separately
/// rather than forced into one shape.
/// </remarks>
public sealed class PrescriptedEmpire : CwView
{
    private readonly CwNode _node;

    internal PrescriptedEmpire(CwNode node)
        : base(
            node?.Block ?? throw new ArgumentException("A prescripted empire entry must be a block.", nameof(node)))
    {
        _node = node;
    }

    /// <summary>The entry's identifier, such as <c>humans1</c>.</summary>
    public string Key => _node.Key ?? string.Empty;

    /// <summary>Localisation key for the empire's name.</summary>
    public string? Name => GetString("name");

    /// <summary>Localisation key for the empire's adjective.</summary>
    public string? Adjective => GetString("adjective");

    /// <summary>Localisation key for the fleet name prefix.</summary>
    public string? ShipPrefix => GetString("ship_prefix");

    /// <summary>
    /// The scripted trigger gating availability, such as <c>has_megacorp</c> or
    /// <c>empire_design_never</c>. Written as a bare trigger name with no block.
    /// </summary>
    public string? Playable => GetString("playable");

    /// <summary>True for the blank template in <c>default.txt</c> that new empires start from.</summary>
    public bool IsDefaultTemplate => GetBool("default") ?? false;

    /// <summary>The founder species.</summary>
    public PrescriptedSpecies? Species =>
        GetBlock("species") is { } block ? new PrescriptedSpecies(block) : null;

    /// <summary>The second species some origins add.</summary>
    public PrescriptedSpecies? SecondarySpecies =>
        GetBlock("secondary_species") is { } block ? new PrescriptedSpecies(block) : null;

    /// <summary>The starting ruler.</summary>
    public PrescriptedRuler? Ruler =>
        GetBlock("ruler") is { } block ? new PrescriptedRuler(block) : null;

    /// <summary>The empire's flag, which uses the same shape as a player design.</summary>
    public EmpireFlag? Flag => GetBlock("empire_flag") is { } block ? new EmpireFlag(block) : null;

    /// <summary>A named flag preset from <c>common/prescripted_flags</c>.</summary>
    public string? PrescriptedFlag => GetString("flag");

    /// <summary>Authority key.</summary>
    public string? Authority => GetString("authority");

    /// <summary>Government type key.</summary>
    public string? Government => GetString("government");

    /// <summary>Origin key.</summary>
    public string? Origin => GetString("origin");

    /// <summary>The empire's ethics.</summary>
    public IReadOnlyList<string> Ethics => GetStrings("ethic");

    /// <summary>The empire's civics.</summary>
    public IReadOnlyList<string> Civics => GetBlockElements("civics");

    /// <summary>Localisation key for the homeworld's name.</summary>
    public string? PlanetName => GetString("planet_name");

    /// <summary>Homeworld planet class.</summary>
    public string? PlanetClass => GetString("planet_class");

    /// <summary>Localisation key for the home system's name.</summary>
    public string? SystemName => GetString("system_name");

    /// <summary>Starting system initializer.</summary>
    public string? Initializer => GetString("initializer");

    /// <summary>Ship appearance set.</summary>
    public string? GraphicalCulture => GetString("graphical_culture");

    /// <summary>City appearance set.</summary>
    public string? CityGraphicalCulture => GetString("city_graphical_culture");

    /// <summary>Room background key.</summary>
    public string? Room => GetString("room");

    /// <summary>Advisor voice key.</summary>
    public string? AdvisorVoiceType => GetString("advisor_voice_type");

    /// <summary>Whether the empire starts nomadic.</summary>
    public bool? IsNomadic => GetBool("is_nomadic");

    /// <summary>Starting ship size, for arkship starts.</summary>
    public string? ShipSize => GetString("ship_size");

    /// <summary>Whether the empire may appear as an AI: <c>no</c>, <c>yes</c> or <c>always</c>.</summary>
    public string? SpawnEnabled => GetString("spawn_enabled");

    /// <summary>Whether the empire spawns as a fallen empire.</summary>
    public bool? SpawnAsFallen => GetBool("spawn_as_fallen");

    /// <summary>Whether the empire may share a portrait with another.</summary>
    public bool? IgnorePortraitDuplication => GetBool("ignore_portrait_duplication");

    public override string ToString() => $"{Key} ({Name})";
}

/// <summary>A species inside a prescripted empire.</summary>
public sealed class PrescriptedSpecies(CwBlock block) : CwView(block)
{
    /// <summary>Species class key.</summary>
    public string? Class => GetString("class");

    /// <summary>Portrait key.</summary>
    public string? Portrait => GetString("portrait");

    /// <summary>
    /// Localisation key for the species name. Older files spell this <c>name</c> and newer ones
    /// <c>species_name</c>; the game accepts both, so both are read.
    /// </summary>
    public string? Name => GetString("species_name") ?? GetString("name");

    /// <summary>Localisation key for the plural species name.</summary>
    public string? Plural => GetString("species_plural") ?? GetString("plural");

    /// <summary>Localisation key for the species adjective.</summary>
    public string? Adjective => GetString("species_adjective") ?? GetString("adjective");

    /// <summary>Name list key.</summary>
    public string? NameList => GetString("name_list");

    /// <summary>Gender.</summary>
    public string? Gender => GetString("gender");

    /// <summary>Species traits, including the class trait.</summary>
    public IReadOnlyList<string> Traits => GetStrings("trait");

    public override string ToString() => $"{Class} ({Portrait})";
}

/// <summary>The ruler of a prescripted empire.</summary>
public sealed class PrescriptedRuler(CwBlock block) : CwView(block)
{
    /// <summary>
    /// Localisation key for the ruler's name, when written as a plain string. Null when the name
    /// is split into <see cref="FirstName"/> and <see cref="SecondName"/> instead.
    /// </summary>
    public string? Name => GetString("name");

    /// <summary>Localisation key for the ruler's first name, used by a few empires.</summary>
    public string? FirstName => GetBlock("name") is { } name
        ? name.Nodes.FirstOrDefault(n => n.Key == "first_name")?.ScalarValue
        : null;

    /// <summary>Localisation key for the ruler's second name, used by a few empires.</summary>
    public string? SecondName => GetBlock("name") is { } name
        ? name.Nodes.FirstOrDefault(n => n.Key == "second_name")?.ScalarValue
        : null;

    /// <summary>Gender.</summary>
    public string? Gender => GetString("gender");

    /// <summary>Portrait key.</summary>
    public string? Portrait => GetString("portrait");

    /// <summary>Index into the portrait's skin texture variants.</summary>
    public int? Texture => GetInt("texture");

    /// <summary>Index into the portrait's attachment variants.</summary>
    public int? Attachment => GetInt("attachment");

    /// <summary>Index into the portrait's clothing variants.</summary>
    public int? Clothes => GetInt("clothes");

    /// <summary>Ascension appearance stage.</summary>
    public int? EvolutionMask => GetInt("evolution_mask");

    /// <summary>Localisation key for the ruler's title.</summary>
    public string? Title => GetString("ruler_title");

    /// <summary>Localisation key for the female form of the ruler's title.</summary>
    public string? TitleFemale => GetString("ruler_title_female");

    /// <summary>Localisation key for the heir's title. Prescripted empires only.</summary>
    public string? HeirTitle => GetString("heir_title");

    /// <summary>Localisation key for the female form of the heir's title.</summary>
    public string? HeirTitleFemale => GetString("heir_title_female");

    /// <summary>The ruler's traits.</summary>
    public IReadOnlyList<string> Traits => GetStrings("trait");

    /// <summary>Leader class.</summary>
    public string? LeaderClass => GetString("leader_class");

    public override string ToString() => Name ?? $"{FirstName} {SecondName}".Trim();
}
