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
    PartKind Kind = PartKind.Character);

/// <summary>A whole portrait model, in the pose the artist modelled it in.</summary>
public sealed record PortraitMesh(IReadOnlyList<MeshPart> Parts)
{
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

    /// <summary>
    /// The height the figure stands at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not simply the bottom of the model: some carry a stray piece well below the figure — one
    /// humanoid keeps a scrap of geometry twenty-three units beneath its own feet — and standing the
    /// portrait on that pushes it off the top of the frame.
    /// </para>
    /// <para>
    /// Nor the bottom of the largest part, which sounds like the body and often is not: a detailed
    /// head can carry more vertices than the torso beneath it, and a portrait balanced on its chin
    /// is no better. So the figure is grown outwards from its largest part, taking in anything that
    /// overlaps what has been gathered so far. What is connected to the body is the body; a scrap
    /// floating well below it never joins.
    /// </para>
    /// </remarks>
    public float Footing
    {
        get
        {
            var spans = Parts
                .Where(p => p.Positions.Length > 0)
                .Select(p => (Count: p.Positions.Length, Low: p.Positions.Min(v => v.Y), High: p.Positions.Max(v => v.Y)))
                .OrderByDescending(s => s.Count)
                .ToList();

            if (spans.Count == 0)
            {
                return Bounds.Min.Y;
            }

            var low = spans[0].Low;
            var high = spans[0].High;

            for (var joined = true; joined;)
            {
                joined = false;

                foreach (var span in spans)
                {
                    if (span.Low <= high && span.High >= low && (span.Low < low || span.High > high))
                    {
                        low = Math.Min(low, span.Low);
                        high = Math.Max(high, span.High);
                        joined = true;
                    }
                }
            }

            return low;
        }
    }

    /// <summary>Reads a portrait model out of a Paradox mesh file.</summary>
    public static PortraitMesh Load(ReadOnlySpan<byte> bytes)
    {
        var asset = PdxAssetReader.Read(bytes);
        var parts = new List<MeshPart>();

        foreach (var shape in asset.Descendants())
        {
            if (shape.Child("mesh") is not { } mesh)
            {
                continue;
            }

            var positions = ReadVector3(mesh.Floats("p"));
            var triangles = mesh.Ints("tri");

            if (positions.Length == 0 || triangles is not { Length: > 0 })
            {
                continue;
            }

            var material = mesh.Child("material");

            parts.Add(new MeshPart(
                shape.Name,
                positions,
                ReadVector3(mesh.Floats("n")),
                ReadVector2(mesh.Floats("u0")),
                triangles,
                material?.String("diff"),
                KindOf(material?.String("shader"))));
        }

        return new PortraitMesh(parts);
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
