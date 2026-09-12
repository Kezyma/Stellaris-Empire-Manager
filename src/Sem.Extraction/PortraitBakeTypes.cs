using Sem.Assets;
using Sem.Clausewitz;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction;

/// <summary>What came of drawing the portraits.</summary>
/// <param name="Rendered">How many likenesses were produced.</param>
/// <param name="Bytes">Their total size.</param>
/// <param name="Failures">Portraits that could not be drawn, with the reason.</param>
public sealed record PortraitBakeReport(int Rendered, long Bytes, IReadOnlyList<string> Failures);

/// <summary>How far a posed, scaled portrait reaches from its own origin.</summary>
/// <param name="Key">The portrait.</param>
/// <param name="Rise">How far the highest point stands above the origin, in model units.</param>
/// <param name="Drop">How far the lowest point hangs below it, negative where nothing does.</param>
/// <param name="Clipped">
/// Whether it reached the edge of even the measuring frame, in which case what it reports is a floor
/// rather than the truth.
/// </param>
public sealed record PortraitExtent(string Key, float Rise, float Drop, bool Clipped);

/// <summary>
/// One ascended form of one skin: the skin it replaces, and where it lets the original show.
/// </summary>
/// <param name="Decal">The ascended skin, a whole texture rather than an overlay.</param>
/// <param name="Mask">
/// Where the two are mixed rather than the decal simply winning. See
/// <see cref="Sem.Assets.DdsImageOps.BlendEvolution"/>, which is the game's own shader written out.
/// </param>
public sealed record EvolutionArtwork(string Decal, string Mask);

/// <summary>
/// Everything a portrait could be wearing, rather than the one thing it opens in.
/// </summary>
/// <remarks>
/// <para>
/// The empire designer needs one face per portrait; a leader designer needs the wardrobe. The game
/// keeps the three separately — a body texture chosen from the portrait's own list, an outfit and an
/// attachment each chosen by a selector — which is why they can be recombined at all. Baking the
/// combinations is not possible: one humanoid has eight colours, seven outfits and a hundred
/// attachments, which is five thousand six hundred pictures of one species.
/// </para>
/// <para>
/// The default of each is the one the empire designer shows, and is listed first.
/// </para>
/// </remarks>
/// <param name="Character">Body textures, which carry the skin and the eyes.</param>
/// <param name="Clothes">Outfits.</param>
/// <param name="Attachment">Hair, horns, masks and hats.</param>
public sealed record PortraitWardrobe(
    IReadOnlyList<string> Character,
    IReadOnlyList<string> Clothes,
    IReadOnlyList<string> Attachment)
{
    /// <summary>
    /// The ascended forms, outermost list a stage and innermost a variant of that stage.
    /// </summary>
    /// <remarks>
    /// A stage's variants line up with <see cref="Character"/> by index, which is the same
    /// correspondence <c>tied_texture</c> states with its <c>evolution_variants</c>: skin three and
    /// ascended form three are the same choice seen twice. Where a stage names fewer forms than
    /// there are skins — the cybernetic portraits name one — the last stands for all of them.
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<EvolutionArtwork>> Evolution { get; init; } = [];

    /// <summary>An empty wardrobe, for a portrait whose definition offers nothing.</summary>
    public static PortraitWardrobe None { get; } = new([], [], []);

    /// <summary>Every option for one kind of part.</summary>
    public IReadOnlyList<string> For(PartKind kind) => kind switch
    {
        PartKind.Clothes => Clothes,
        PartKind.Attachment => Attachment,
        _ => Character,
    };

    /// <summary>What the empire designer shows, which is the first of each.</summary>
    public PortraitTextures Default => new(
        Character.FirstOrDefault(),
        Clothes.FirstOrDefault(),
        Attachment.FirstOrDefault());
}

/// <summary>What a portrait is wearing, as its own definition describes it.</summary>
/// <param name="Character">The body texture.</param>
/// <param name="Clothes">The clothing texture.</param>
/// <param name="Attachment">Hair, horns, a hat — whatever is fixed to the head.</param>
/// <remarks>
/// Held apart rather than resolved into the mesh because the three are chosen independently. Drawing
/// the same model in different clothes, which the ruler's appearance will want, is then a matter of
/// a different set rather than a different renderer.
/// </remarks>
public sealed record PortraitTextures(string? Character, string? Clothes, string? Attachment)
{
    /// <summary>A portrait whose definition says nothing, leaving the mesh to supply everything.</summary>
    public static PortraitTextures None { get; } = new(null, null, null);

    /// <summary>The texture for one kind of part, or null when the definition names none.</summary>
    public string? For(PartKind kind) => kind switch
    {
        PartKind.Clothes => Clothes,
        PartKind.Attachment => Attachment,
        _ => Character,
    };
}
