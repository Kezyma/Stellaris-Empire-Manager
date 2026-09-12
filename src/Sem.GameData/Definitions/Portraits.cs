namespace Sem.GameData;

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

/// <summary>One layer of a portrait: a run of parts painted together, in every form it takes.</summary>
/// <param name="Slot">Which of the three textures this run wears.</param>
/// <param name="Images">Its forms, the empire designer's first.</param>
public sealed record PortraitLayer(PortraitSlot Slot, IReadOnlyList<PortraitLayerImage> Images);

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
