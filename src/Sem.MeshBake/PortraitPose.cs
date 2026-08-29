using System.Numerics;

namespace Sem.MeshBake;

/// <summary>
/// The pose a portrait's skeleton rests in, read from one of its animations.
/// </summary>
/// <remarks>
/// <para>
/// A portrait's vertices are not drawn where they are stored. They are stored in whatever space the
/// artist modelled them in — one reptilian sits forty units above its own origin — and the skeleton
/// carries them back. Half of that transform is in the model, as each bone's way out of model
/// space; the other half is the pose, which is in the animation files.
/// </para>
/// <para>
/// Every animation for a model opens with the same rest pose, before any frame is applied, so any
/// of them will do. It is the pose the game starts from, and posing a portrait in it is what puts
/// every species in the same place without measuring anything.
/// </para>
/// </remarks>
public sealed class PortraitPose
{
    private readonly Dictionary<string, Matrix4x4> _bones;

    private PortraitPose(Dictionary<string, Matrix4x4> bones) => _bones = bones;

    /// <summary>A pose that moves nothing, for models with no skeleton.</summary>
    public static PortraitPose None { get; } = new(new Dictionary<string, Matrix4x4>(StringComparer.Ordinal));

    /// <summary>How many bones the pose describes.</summary>
    public int Count => _bones.Count;

    /// <summary>Reads the rest pose out of an animation file.</summary>
    public static PortraitPose Read(ReadOnlySpan<byte> bytes)
    {
        var asset = PdxAssetReader.Read(bytes);
        var bones = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

        if (asset.Child("info") is not { } info)
        {
            return new PortraitPose(bones);
        }

        foreach (var bone in info.Children)
        {
            if (bone.Floats("t") is not { Length: >= 3 } t ||
                bone.Floats("q") is not { Length: >= 4 } q)
            {
                continue;
            }

            var scale = bone.Floats("s")?.FirstOrDefault() ?? 1f;

            // Scale, then turn, then move — the order a bone's own transform is built in.
            bones[bone.Name] =
                Matrix4x4.CreateScale(scale == 0 ? 1 : scale) *
                Matrix4x4.CreateFromQuaternion(new Quaternion(q[0], q[1], q[2], q[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        return new PortraitPose(bones);
    }

    /// <summary>
    /// Moves a model's vertices into the place the game draws them.
    /// </summary>
    /// <remarks>
    /// Each bone's transform is the pose accumulated down the chain of its parents, applied after
    /// the model's own way out of that bone's space. A vertex is then the weighted sum of what each
    /// of its bones does to it, which is what makes a shoulder follow both the arm and the chest.
    /// </remarks>
    public PortraitMesh ApplyTo(PortraitMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_bones.Count == 0 || mesh.Bones.Count == 0)
        {
            return mesh;
        }

        var skin = SkinMatrices(mesh);

        return mesh with
        {
            Parts = [.. mesh.Parts.Select(part => Pose(part, skin))],
        };
    }

    private Matrix4x4[] SkinMatrices(PortraitMesh mesh)
    {
        var world = new Matrix4x4[mesh.Bones.Count];
        var skin = new Matrix4x4[mesh.Bones.Count];

        for (var i = 0; i < mesh.Bones.Count; i++)
        {
            var bone = mesh.Bones[i];
            var local = _bones.GetValueOrDefault(bone.Name, Matrix4x4.Identity);

            // Parents always come first in the file, so this needs only one pass.
            world[i] = bone.Parent >= 0 && bone.Parent < i
                ? local * world[bone.Parent]
                : local;

            skin[i] = bone.InverseBind * world[i];
        }

        return skin;
    }

    private static MeshPart Pose(MeshPart part, Matrix4x4[] skin)
    {
        var influences = part.Influences;

        if (influences <= 0 || part.BoneWeights.Length < part.BoneIndices.Length)
        {
            return part;
        }

        var positions = new Vector3[part.Positions.Length];
        var normals = new Vector3[part.Normals.Length];

        for (var v = 0; v < part.Positions.Length; v++)
        {
            var moved = Vector3.Zero;
            var turned = Vector3.Zero;
            var total = 0f;

            for (var j = 0; j < influences; j++)
            {
                var slot = (v * influences) + j;
                var bone = part.BoneIndices[slot];
                var weight = part.BoneWeights[slot];

                if (weight == 0 || bone < 0 || bone >= skin.Length)
                {
                    continue;
                }

                moved += weight * Vector3.Transform(part.Positions[v], skin[bone]);

                if (v < normals.Length)
                {
                    turned += weight * Vector3.TransformNormal(part.Normals[v], skin[bone]);
                }

                total += weight;
            }

            // A vertex no bone claims stays where it was drawn rather than collapsing to the origin.
            positions[v] = total > 0 ? moved / total : part.Positions[v];

            if (v < normals.Length)
            {
                normals[v] = total > 0 && turned != Vector3.Zero
                    ? Vector3.Normalize(turned)
                    : part.Normals[v];
            }
        }

        return part with { Positions = positions, Normals = normals };
    }
}
