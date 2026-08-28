using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>The founder species of an empire design, or its secondary species.</summary>
public sealed class SpeciesDesign(CwBlock block) : CwView(block, FieldOrder)
{
    internal static readonly string[] FieldOrder =
    [
        "class",
        "portrait",
        "species_name",
        "species_plural",
        "species_adjective",
        "species_bio",
        "name_list",
        "gender",
        "trait",
    ];

    /// <summary>Species class key, such as <c>MAM</c>, <c>AVI</c> or <c>MACHINE</c>.</summary>
    public string? Class
    {
        get => GetString("class");
        set => SetString("class", value);
    }

    /// <summary>Portrait key, such as <c>avi18</c>.</summary>
    public string? Portrait
    {
        get => GetString("portrait");
        set => SetString("portrait", value);
    }

    /// <summary>The species name.</summary>
    public LocRef Name => new(GetOrAddBlock("species_name"));

    /// <summary>The plural form of the species name.</summary>
    public LocRef Plural => new(GetOrAddBlock("species_plural"));

    /// <summary>The adjectival form of the species name.</summary>
    public LocRef Adjective => new(GetOrAddBlock("species_adjective"));

    /// <summary>
    /// Free-text species description. Added in 4.x and optional. The game's own writer truncates
    /// this at a length cap, so a value read back may end mid-sentence.
    /// </summary>
    public string? Biography
    {
        get => GetString("species_bio");
        set => SetString("species_bio", value);
    }

    /// <summary>Name list key, such as <c>AVI3</c>.</summary>
    public string? NameList
    {
        get => GetString("name_list");
        set => SetString("name_list", value);
    }

    /// <summary>Gender: <c>not_set</c>, <c>male</c>, <c>female</c> or <c>indeterminable</c>.</summary>
    public string? Gender
    {
        get => GetString("gender");
        set => SetString("gender", value, quoted: false);
    }

    /// <summary>
    /// Species traits in order, including the class trait such as <c>trait_organic</c>, which 4.x
    /// writes explicitly.
    /// </summary>
    public IReadOnlyList<string> Traits => GetStrings("trait");

    /// <summary>Replaces the trait list, reusing existing lines so the diff stays small.</summary>
    public void SetTraits(IReadOnlyList<string> traits) => SetStrings("trait", traits);

    /// <summary>Builds an empty species block with no fields set.</summary>
    public static CwBlock CreateBlock() => new();

    public override string ToString() => $"{Class} ({Portrait})";
}
