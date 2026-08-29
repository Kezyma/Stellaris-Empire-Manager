using System.Numerics;

namespace Sem.MeshBake;

/// <summary>One part of a portrait: its geometry and the texture it wears.</summary>
/// <param name="Name">The part's name, such as <c>bodyShape</c> or <c>outfitShape</c>.</param>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals, used for shading.</param>
/// <param name="TexCoords">Texture coordinates, one per vertex.</param>
/// <param name="Triangles">Vertex indices, three per triangle.</param>
/// <param name="Texture">The diffuse texture's file name, as the model names it.</param>
public sealed record MeshPart(
    string Name,
    Vector3[] Positions,
    Vector3[] Normals,
    Vector2[] TexCoords,
    int[] Triangles,
    string? Texture);

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

            parts.Add(new MeshPart(
                shape.Name,
                positions,
                ReadVector3(mesh.Floats("n")),
                ReadVector2(mesh.Floats("u0")),
                triangles,
                mesh.Child("material")?.String("diff")));
        }

        return new PortraitMesh(parts);
    }

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
