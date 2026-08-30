using System.Numerics;

namespace Sem.MeshBake;

/// <summary>What a part of a portrait is, which decides where its texture comes from.</summary>
/// <remarks>
/// A portrait is not one picture. Its skin, its clothes and whatever it wears on its head are three
/// separate textures, chosen separately, and the mesh says which of them a part wants by naming the
/// shader that draws it.
/// </remarks>
public enum PartKind
{
    /// <summary>The body itself.</summary>
    Character,

    /// <summary>Clothing.</summary>
    Clothes,

    /// <summary>Hair, horns, hats — whatever the portrait attaches.</summary>
    Attachment,
}

/// <summary>One part of a portrait: its geometry and the texture it wears.</summary>
/// <param name="Name">The part's name, such as <c>bodyShape</c> or <c>outfitShape</c>.</param>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals, used for shading.</param>
/// <param name="TexCoords">Texture coordinates, one per vertex.</param>
/// <param name="Triangles">Vertex indices, three per triangle.</param>
/// <param name="Texture">
/// The diffuse texture the model names, which may be absent: many portraits leave it to their
/// definition to say what they are wearing.
/// </param>
/// <param name="Kind">Which of the portrait's textures this part wants.</param>
public sealed record MeshPart(
    string Name,
    Vector3[] Positions,
    Vector3[] Normals,
    Vector2[] TexCoords,
    int[] Triangles,
    string? Texture,
    PartKind Kind = PartKind.Character)
{
    /// <summary>
    /// Where this part sits among the parts cut from the same shape, counting from zero.
    /// </summary>
    /// <remarks>
    /// A shape is one object as the artist named it, and it holds a mesh per material it is painted
    /// with, so several parts can share a name and be told apart only by this. It is the number a
    /// set's <c>meshsettings</c> means by <c>index</c>.
    /// </remarks>
    public int Index { get; init; }

    /// <summary>Which bones move each vertex, four per vertex.</summary>
    public int[] BoneIndices { get; init; } = [];

    /// <summary>How much each of those bones moves it, four per vertex.</summary>
    public float[] BoneWeights { get; init; } = [];

    /// <summary>How many bones share each vertex.</summary>
    public int Influences => Positions.Length == 0 ? 0 : BoneIndices.Length / Positions.Length;
}

/// <summary>One bone of a portrait's skeleton.</summary>
/// <param name="Name">Its name, which is how the animation refers to it.</param>
/// <param name="Parent">The bone it hangs from, or -1 at the root.</param>
/// <param name="InverseBind">
/// The transform taking a vertex out of the model's own space and into this bone's.
/// </param>
public sealed record MeshBone(string Name, int Parent, Matrix4x4 InverseBind);

/// <summary>A whole portrait model, in the pose the artist modelled it in.</summary>
public sealed record PortraitMesh(IReadOnlyList<MeshPart> Parts)
{
    /// <summary>
    /// The skeleton, in the order the skin's bone indices refer to it.
    /// </summary>
    /// <remarks>
    /// Empty for the models built as a single flat card, which have no bones and need none.
    /// </remarks>
    public IReadOnlyList<MeshBone> Bones { get; init; } = [];

    /// <summary>The box enclosing every part, for framing the view.</summary>
    public (Vector3 Min, Vector3 Max) Bounds
    {
        get
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var position in Parts.SelectMany(p => p.Positions))
            {
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            return Parts.Any(p => p.Positions.Length > 0) ? (min, max) : (Vector3.Zero, Vector3.One);
        }
    }

    /// <summary>Reads a portrait model out of a Paradox mesh file.</summary>
    /// <remarks>
    /// A shape carries one mesh per material it is painted with, not one mesh. The plantoid
    /// corvette is a single <c>polySurface84Shape</c> split three ways — its hull, a strip of
    /// panels, and a translucent piece — and reading only the first of them drew the panels and
    /// left the ship out, which is what made that set's picture a dark smudge. Seven of the game's
    /// corvettes are built this way, and one portrait, which is not one a player can choose.
    /// </remarks>
    public static PortraitMesh Load(ReadOnlySpan<byte> bytes)
    {
        var asset = PdxAssetReader.Read(bytes);
        var parts = new List<MeshPart>();
        IReadOnlyList<MeshBone> bones = [];

        foreach (var shape in asset.Descendants())
        {
            // Every part repeats the same skeleton, so the first one that carries it settles it.
            if (bones.Count == 0 && shape.Child("skeleton") is { } skeleton)
            {
                bones = ReadBones(skeleton);
            }

            var index = 0;

            foreach (var mesh in shape.Children.Where(c => c.Name == "mesh"))
            {
                var slice = index++;
                var positions = ReadVector3(mesh.Floats("p"));
                var triangles = mesh.Ints("tri");

                if (positions.Length == 0 || triangles is not { Length: > 0 })
                {
                    continue;
                }

                var material = mesh.Child("material");
                var skin = mesh.Child("skin");

                parts.Add(new MeshPart(
                    shape.Name,
                    positions,
                    ReadVector3(mesh.Floats("n")),
                    ReadVector2(mesh.Floats("u0")),
                    triangles,
                    material?.String("diff"),
                    KindOf(material?.String("shader")))
                {
                    // Counted over every mesh the shape holds, including any skipped just above, so
                    // that it stays the number the game's own settings use.
                    Index = slice,
                    BoneIndices = skin?.Ints("ix") ?? [],
                    BoneWeights = skin?.Floats("w") ?? [],
                });
            }
        }

        return new PortraitMesh(parts) { Bones = bones };
    }

    /// <summary>
    /// Reads the skeleton: each bone's parent, and the transform into its own space.
    /// </summary>
    /// <remarks>
    /// The transform is stored as nine numbers of rotation followed by three of translation. It
    /// takes a vertex out of the space the model was drawn in; putting it back where the game shows
    /// it needs the other half of the pair, which lives in the animation.
    /// </remarks>
    private static IReadOnlyList<MeshBone> ReadBones(PdxNode skeleton)
    {
        var bones = new List<MeshBone>();

        foreach (var bone in skeleton.Children)
        {
            if (bone.Floats("tx") is not { Length: >= 12 } tx)
            {
                continue;
            }

            bones.Add(new MeshBone(
                bone.Name,
                bone.Ints("pa")?.FirstOrDefault() ?? -1,
                new Matrix4x4(
                    tx[0], tx[1], tx[2], 0,
                    tx[3], tx[4], tx[5], 0,
                    tx[6], tx[7], tx[8], 0,
                    tx[9], tx[10], tx[11], 1)));
        }

        return bones;
    }

    /// <summary>
    /// Works out what a part is from the shader that draws it.
    /// </summary>
    /// <remarks>
    /// The names are the game's: <c>PdxMeshPortraitClothes</c> and <c>PdxMeshPortraitHair</c> beside
    /// the plain <c>PdxMeshPortrait</c>. Anything unfamiliar counts as part of the body, which is
    /// the common case and the one that degrades most gracefully.
    /// </remarks>
    private static PartKind KindOf(string? shader) => shader switch
    {
        not null when shader.EndsWith("Clothes", StringComparison.OrdinalIgnoreCase) => PartKind.Clothes,
        not null when shader.EndsWith("Hair", StringComparison.OrdinalIgnoreCase) => PartKind.Attachment,
        _ => PartKind.Character,
    };

    private static Vector3[] ReadVector3(float[]? values)
    {
        if (values is null)
        {
            return [];
        }

        var result = new Vector3[values.Length / 3];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Vector3(values[i * 3], values[(i * 3) + 1], values[(i * 3) + 2]);
        }

        return result;
    }

    private static Vector2[] ReadVector2(float[]? values)
    {
        if (values is null)
        {
            return [];
        }

        var result = new Vector2[values.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Vector2(values[i * 2], values[(i * 2) + 1]);
        }

        return result;
    }
}
