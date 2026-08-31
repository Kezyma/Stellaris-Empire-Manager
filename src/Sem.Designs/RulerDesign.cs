using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>The empire's starting ruler.</summary>
public sealed class RulerDesign : CwView
{
    /// <summary>Views a block the design already has.</summary>
    public RulerDesign(CwBlock block)
        : base(block, FieldOrder)
    {
    }

    /// <summary>Views a block that is made only when something is written to it.</summary>
    public RulerDesign(CwView parent, string key)
        : base(parent, key, FieldOrder)
    {
    }

    internal static readonly string[] FieldOrder =
    [
        "gender",
        "name",
        "portrait",
        "texture",
        "evolution_mask",
        "attachment",
        "clothes",
        "custom_biography",

        // The order the game's own editor shows them in: a title, its heir, then the same pair again
        // in their female forms.
        "ruler_title",
        "heir_title",
        "ruler_title_female",
        "heir_title_female",
        "trait",
        "leader_class",
    ];

    /// <summary>Gender: <c>not_set</c>, <c>male</c>, <c>female</c> or <c>indeterminable</c>.</summary>
    public string? Gender
    {
        get => GetString("gender");
        set => SetString("gender", value, quoted: false);
    }

    /// <summary>The ruler's name, wrapped in the regnal-name structure the game uses.</summary>
    public RulerName Name => new(this, "name");

    /// <summary>Portrait key. Usually, but not necessarily, one of the founder species' portraits.</summary>
    public string? Portrait
    {
        get => GetString("portrait");
        set => SetString("portrait", value);
    }

    /// <summary>Index into the portrait's skin texture variants.</summary>
    public int? Texture
    {
        get => GetInt("texture");
        set => SetInt("texture", value);
    }

    /// <summary>
    /// Ascension appearance stage, used by cybernetic and psionic portraits. Optional in files
    /// written before 4.x, always written since.
    /// </summary>
    public int? EvolutionMask
    {
        get => GetInt("evolution_mask");
        set => SetInt("evolution_mask", value);
    }

    /// <summary>Index into the portrait's attachment variants.</summary>
    public int? Attachment
    {
        get => GetInt("attachment");
        set => SetInt("attachment", value);
    }

    /// <summary>Index into the portrait's clothing variants.</summary>
    public int? Clothes
    {
        get => GetInt("clothes");
        set => SetInt("clothes", value);
    }

    /// <summary>
    /// The ruler's own story, as the player wrote it, or null when they wrote none.
    /// </summary>
    /// <remarks>
    /// Written as a block with the text under <c>key</c> and <c>literal=yes</c> beside it, the same
    /// shape as a name — and worth noting because the species' biography, which the game presents
    /// as the matching field, is a plain quoted string instead. That asymmetry is the game's, not a
    /// choice made here.
    /// </remarks>
    public LocRef? CustomBiography =>
        GetBlock("custom_biography") is { } biography ? new LocRef(biography) : null;

    /// <summary>Returns the biography, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddCustomBiography() => new(GetOrAddBlock("custom_biography"));

    /// <summary>Custom ruler title, or null to use the one the government implies.</summary>
    public LocRef? Title => GetBlock("ruler_title") is { } title ? new LocRef(title) : null;

    /// <summary>Custom female ruler title, or null.</summary>
    public LocRef? TitleFemale =>
        GetBlock("ruler_title_female") is { } title ? new LocRef(title) : null;

    /// <summary>
    /// What the ruler's successor is called, or null.
    /// </summary>
    /// <remarks>
    /// The game's own editor keeps a box for this and for its female form beside the two ruler
    /// titles, and shows "N/A" where a government has no heir. Rare in practice: no design in the
    /// player's own file carries one, and of the game's fifty-two empires only the Infernals'
    /// Pyrragthul does. Modelled because the editor offers it, and written only when something is
    /// typed — a design that never had one must not grow one.
    /// </remarks>
    public LocRef? HeirTitle => GetBlock("heir_title") is { } title ? new LocRef(title) : null;

    /// <summary>What the ruler's successor is called, in its female form, or null.</summary>
    public LocRef? HeirTitleFemale =>
        GetBlock("heir_title_female") is { } title ? new LocRef(title) : null;

    /// <summary>Returns the ruler title, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddTitle() => new(GetOrAddBlock("ruler_title"));

    /// <summary>Returns the female ruler title, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddTitleFemale() => new(GetOrAddBlock("ruler_title_female"));

    /// <summary>Returns the heir title, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddHeirTitle() => new(GetOrAddBlock("heir_title"));

    /// <summary>Returns the female heir title, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddHeirTitleFemale() => new(GetOrAddBlock("heir_title_female"));

    /// <summary>
    /// The ruler's traits. Note the keys use two different prefixes, <c>leader_trait_*</c> and
    /// <c>trait_ruler_*</c>, so nothing here should assume one of them.
    /// </summary>
    public IReadOnlyList<string> Traits => GetStrings("trait");

    /// <summary>Replaces the trait list, reusing existing lines so the diff stays small.</summary>
    public void SetTraits(IReadOnlyList<string> traits) => SetStrings("trait", traits);

    /// <summary>Leader class: <c>official</c>, <c>commander</c> or <c>scientist</c>.</summary>
    public string? LeaderClass
    {
        get => GetString("leader_class");
        set => SetString("leader_class", value, quoted: false);
    }

    public override string ToString() => $"{Name} ({LeaderClass})";
}

/// <summary>
/// A ruler's name. The game wraps the name itself in <c>full_names</c> and records separately
/// whether the full regnal form should be displayed.
/// </summary>
public sealed class RulerName : CwView
{
    private static readonly string[] FieldOrder = ["full_names", "first_name", "second_name", "use_full_regnal_name"];

    /// <summary>Views a block the ruler already has.</summary>
    public RulerName(CwBlock block)
        : base(block, FieldOrder)
    {
    }

    /// <summary>Views a block that is made only when something is written to it.</summary>
    public RulerName(CwView parent, string key)
        : base(parent, key, FieldOrder)
    {
    }

    /// <summary>
    /// The name itself. Player-created designs always use this form; the game's own prescripted
    /// empires sometimes split the name instead, which
    /// <see cref="FirstName"/> and <see cref="SecondName"/> cover.
    /// </summary>
    public LocRef? FullNames => GetBlock("full_names") is { } names ? new LocRef(names) : null;

    /// <summary>First name, used only by some prescripted empires.</summary>
    public LocRef? FirstName => GetBlock("first_name") is { } name ? new LocRef(name) : null;

    /// <summary>Second name, used only by some prescripted empires.</summary>
    public LocRef? SecondName => GetBlock("second_name") is { } name ? new LocRef(name) : null;

    /// <summary>Returns the name, adding it in canonical position when absent.</summary>
    public LocRef GetOrAddFullNames() => new(GetOrAddBlock("full_names"));

    /// <summary>
    /// Drops the split form of the name.
    /// </summary>
    /// <remarks>
    /// For when a whole name is written over one the game had stored in pieces. Left in place, the
    /// two forms both describe the ruler and the game reads whichever it prefers, so the player's
    /// own name would sit beside a name they had replaced.
    /// </remarks>
    public void RemoveParts()
    {
        RemoveAll("first_name");
        RemoveAll("second_name");
    }

    /// <summary>Whether the full regnal form is used. Written only when true.</summary>
    public bool UseFullRegnalName
    {
        get => GetBool("use_full_regnal_name") ?? false;
        set => SetString("use_full_regnal_name", value ? "yes" : null, quoted: false);
    }

    public override string ToString() => FullNames?.ToString() ?? $"{FirstName} {SecondName}".Trim();
}
