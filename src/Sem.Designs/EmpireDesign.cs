using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// One empire in the player's <c>user_empire_designs_v3.4.txt</c>.
/// </summary>
/// <remarks>
/// Every field except the handful the game always writes is optional, because files saved by
/// earlier versions omit many of them and the game loads those quite happily. Fields not modelled
/// here stay in the underlying tree untouched.
/// </remarks>
public sealed class EmpireDesign : CwView
{
    /// <summary>The order the game writes these fields in. New fields are inserted to match.</summary>
    internal static readonly string[] FieldOrder =
    [
        "key",
        "ship_prefix",
        "species",
        "secondary_species",
        "name",
        "adjective",
        "authority",
        "flag",
        "government",
        "is_nomadic",
        "advisor_voice_type",
        "planet_name",
        "planet_class",
        "ship_size",
        "system_name",
        "initializer",
        "graphical_culture",
        "city_graphical_culture",
        "empire_flag",
        "ruler",
        "spawn_as_fallen",
        "ignore_portrait_duplication",
        "room",
        "spawn_enabled",
        "ethic",
        "civics",
        "origin",
    ];

    private readonly CwNode _node;

    internal EmpireDesign(CwNode node)
        : base(
            node?.Block ?? throw new ArgumentException("An empire design entry must be a block.", nameof(node)),
            FieldOrder)
    {
        _node = node;
    }

    /// <summary>
    /// The identifier the file is keyed by, which the game keeps equal to the empire's name.
    /// Use <see cref="Rename"/> to change it, since it appears in two places.
    /// </summary>
    public string Key => GetString("key") ?? _node.Key ?? string.Empty;

    /// <summary>Fleet name prefix, such as <c>ISS</c>. Empty when the empire has none.</summary>
    public LocRef ShipPrefix => new(this, "ship_prefix");

    /// <summary>The founder species.</summary>
    public SpeciesDesign Species => new(this, "species");

    /// <summary>
    /// The second species some origins add, such as Syncretic Evolution. Null for most empires.
    /// </summary>
    public SpeciesDesign? SecondarySpecies =>
        GetBlock("secondary_species") is { } block ? new SpeciesDesign(block) : null;

    /// <summary>The empire's name as displayed.</summary>
    public LocRef Name => new(this, "name");

    /// <summary>The adjectival form of the empire's name.</summary>
    public LocRef Adjective => new(this, "adjective");

    /// <summary>Authority key, such as <c>auth_corporate</c>.</summary>
    public string? Authority
    {
        get => GetString("authority");
        set => SetString("authority", value);
    }

    /// <summary>
    /// A named flag preset from <c>common/prescripted_flags</c>, used instead of
    /// <see cref="Flag"/> by a few empires. Named <c>flags</c> before 4.x.
    /// </summary>
    public string? PrescriptedFlag
    {
        get => GetString("flag");
        set => SetString("flag", value);
    }

    /// <summary>
    /// Government type key, such as <c>gov_megacorporation</c>. The game derives this from the
    /// authority, ethics and civics rather than letting it be chosen directly.
    /// </summary>
    public string? Government
    {
        get => GetString("government");
        set => SetString("government", value);
    }

    /// <summary>Whether the empire starts nomadic. Added in 4.x with the Nomads pack.</summary>
    public bool? IsNomadic
    {
        get => GetBool("is_nomadic");
        set => SetBool("is_nomadic", value);
    }

    /// <summary>Advisor voice key, such as <c>l_english</c> or <c>l_militarist</c>.</summary>
    public string? AdvisorVoiceType
    {
        get => GetString("advisor_voice_type");
        set => SetString("advisor_voice_type", value);
    }

    /// <summary>The homeworld's name.</summary>
    public LocRef PlanetName => new(this, "planet_name");

    /// <summary>Homeworld planet class, such as <c>pc_tropical</c>.</summary>
    public string? PlanetClass
    {
        get => GetString("planet_class");
        set => SetString("planet_class", value);
    }

    /// <summary>Starting ship size, used by nomadic and arkship starts. Added in 4.x.</summary>
    public string? ShipSize
    {
        get => GetString("ship_size");
        set => SetString("ship_size", value);
    }

    /// <summary>The home system's name.</summary>
    public LocRef SystemName => new(this, "system_name");

    /// <summary>
    /// Starting system initializer. Empty is normal and means the game generates a system rather
    /// than using a scripted one.
    /// </summary>
    public string? Initializer
    {
        get => GetString("initializer");
        set => SetString("initializer", value);
    }

    /// <summary>Ship appearance set, such as <c>avian_01</c>.</summary>
    public string? GraphicalCulture
    {
        get => GetString("graphical_culture");
        set => SetString("graphical_culture", value);
    }

    /// <summary>City appearance set. Chosen independently of <see cref="GraphicalCulture"/>.</summary>
    public string? CityGraphicalCulture
    {
        get => GetString("city_graphical_culture");
        set => SetString("city_graphical_culture", value);
    }

    /// <summary>The empire's flag.</summary>
    public EmpireFlag Flag => new(this, "empire_flag");

    /// <summary>The starting ruler.</summary>
    public RulerDesign Ruler => new(this, "ruler");

    /// <summary>Whether the empire spawns as a fallen empire.</summary>
    public bool? SpawnAsFallen
    {
        get => GetBool("spawn_as_fallen");
        set => SetBool("spawn_as_fallen", value);
    }

    /// <summary>Whether the empire may share a portrait with another in the same game.</summary>
    public bool? IgnorePortraitDuplication
    {
        get => GetBool("ignore_portrait_duplication");
        set => SetBool("ignore_portrait_duplication", value);
    }

    /// <summary>Room background key, such as <c>default_room</c>.</summary>
    public string? Room
    {
        get => GetString("room");
        set => SetString("room", value);
    }

    /// <summary>
    /// Whether the empire may appear as an AI: <c>no</c>, <c>always</c>, or <c>yes</c> for
    /// "sometimes". Written unquoted.
    /// </summary>
    public string? SpawnEnabled
    {
        get => GetString("spawn_enabled");
        set => SetString("spawn_enabled", value, quoted: false);
    }

    /// <summary>
    /// The empire's ethics. One entry for a gestalt, otherwise one or two fanatic and regular
    /// picks totalling three points.
    /// </summary>
    public IReadOnlyList<string> Ethics => GetStrings("ethic");

    /// <summary>Replaces the ethics, reusing existing lines so the diff stays small.</summary>
    public void SetEthics(IReadOnlyList<string> ethics) => SetStrings("ethic", ethics);

    /// <summary>The empire's civics.</summary>
    public IReadOnlyList<string> Civics => GetBlockElements("civics");

    /// <summary>Replaces the civics, reusing existing entries so the diff stays small.</summary>
    public void SetCivics(IReadOnlyList<string> civics) => SetBlockElements("civics", civics);

    /// <summary>Origin key, such as <c>origin_ocean_paradise</c>.</summary>
    public string? Origin
    {
        get => GetString("origin");
        set => SetString("origin", value);
    }

    /// <summary>True when the ruler's species is not the founder species.</summary>
    public bool HasSecondarySpecies => GetBlock("secondary_species") is not null;

    /// <summary>
    /// Whether one of this empire's species is the second one rather than the founders.
    /// </summary>
    /// <remarks>
    /// Asked positively - is this the second species - rather than as "is this not the founders",
    /// because <see cref="SecondarySpecies"/> is read through <c>GetBlock</c> and so
    /// exists only if the design says it does, while reaching for the founders' block would make
    /// one in a design that has none. An empire's controls each hold whichever species they were
    /// given and have to ask, since the two are judged by different rules.
    /// </remarks>
    public bool IsSecondary(SpeciesDesign species) =>
        SecondarySpecies is { } second && second.SameAs(species);

    /// <summary>
    /// Renames the empire, updating both the file's entry key and the <c>key</c> field, which the
    /// game keeps in step. The displayed name in <see cref="Name"/> is separate and unchanged.
    /// </summary>
    public void Rename(string newKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(newKey);

        SetString("key", newKey);
        _node.Rename(newKey, quoted: _node.KeyToken?.Kind != CwTokenKind.BareToken);
    }

    /// <summary>
    /// Takes a copy of this empire exactly as it stands, to be put back later.
    /// </summary>
    /// <remarks>
    /// A clone of the entry rather than a note of its fields, for the same reason a shared link
    /// carries the whole block: a snapshot that listed what it knew about would silently fail to
    /// restore whatever was added to a design afterwards.
    /// </remarks>
    public EmpireSnapshot Snapshot() => new(_node.Clone());

    /// <summary>
    /// Puts this empire back to a copy taken earlier, in the place it already occupies.
    /// </summary>
    /// <remarks>
    /// The entry itself is kept and its contents replaced, rather than the entry being swapped for
    /// the copy. That keeps this design the same object — the designer is holding it, and would go
    /// on editing the one it had — and keeps its position and its whitespace in the file, so an
    /// empire edited and then put back leaves the file as it found it.
    /// </remarks>
    public void Restore(EmpireSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (_node.Value is not CwBlock live || snapshot.Entry.Value is not CwBlock stored)
        {
            return;
        }

        live.Clear();

        foreach (var field in stored.Nodes)
        {
            live.Add(field.Clone());
        }

        // The entry's own key is not one of the fields inside it, and renaming changes both.
        if (snapshot.Entry.KeyToken is { } key)
        {
            _node.Rename(key.Value, quoted: key.Kind == CwTokenKind.QuotedString);
        }
    }

    /// <summary>Adds a secondary species block, for origins that need one.</summary>
    public SpeciesDesign AddSecondarySpecies() => new(GetOrAddBlock("secondary_species"));

    /// <summary>Removes the secondary species block.</summary>
    public void RemoveSecondarySpecies() => RemoveAll("secondary_species");

    /// <summary>The node this design was read from.</summary>
    internal CwNode Node => _node;

    public override string ToString() => Key;
}
