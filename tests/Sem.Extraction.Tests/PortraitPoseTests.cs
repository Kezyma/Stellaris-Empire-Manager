using System.Numerics;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction.Tests;

/// <summary>
/// A portrait's vertices are not stored where they are drawn. The model holds each bone's way out
/// of the space it was modelled in, and the animation holds the pose that puts it back; neither
/// half is any use alone. Without both, a fifth of the species render outside the frame.
/// </summary>
public sealed class PortraitPoseTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static string Model(string relative) =>
        Path.Combine(InstallRoot!, "gfx", "models", "portraits", relative.Replace('/', Path.DirectorySeparatorChar));

    [SkippableTheory]
    [Trait("Category", "RealData")]

    // One modelled around its origin, and two modelled far above it. All three should end up in the
    // same place once posed, which is the whole point of posing them.
    [InlineData("human/new_human/human_01_female_portrait.mesh", "human/new_human/human_01_female_portrait_idle1.anim")]
    [InlineData("mammalian/mammalian_01_portrait.mesh", "mammalian/mammalian_01_portrait_happy.anim")]
    [InlineData("reptilian/reptilian_16_portrait.mesh", "reptilian/reptilian_16_portrait_happy.anim")]
    public void PosingPutsEverySpeciesInTheSamePlace(string mesh, string animation)
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        Skip.If(!File.Exists(Model(mesh)) || !File.Exists(Model(animation)), "Model not in this installation.");

        var pose = PortraitPose.Read(SafeFile.ReadAllBytes(Model(animation)));
        Assert.True(pose.Count > 0, "The animation should describe a skeleton.");

        var posed = pose.ApplyTo(PortraitMesh.Load(SafeFile.ReadAllBytes(Model(mesh))));
        var (min, max) = posed.Bounds;

        // Standing on its own origin, give or take: the game draws every portrait from that point,
        // so a model still sitting thirty units up has not been posed at all.
        Assert.InRange(min.Y, -6f, 4f);
        Assert.InRange(max.Y - min.Y, 5f, 30f);
    }

    [SkippableTheory]
    [Trait("Category", "RealData")]

    // Four models whose mesh and animation disagree about the Maya namespace their joints are in —
    // the mesh bare and the animation prefixed, or the other way about. Matched strictly, every bone
    // misses and the figure is left small and low in the corner of its frame, which is exactly how
    // these four looked.
    [InlineData(
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_05.mesh",
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_05_idle1.anim")]
    [InlineData(
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_06.mesh",
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_06_idle1.anim")]
    [InlineData(
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_09.mesh",
        "mammalian/mammalian_ar/mammalian_ar_01_portrait_09_idle1.anim")]
    [InlineData(
        "biogenesis/bio_01_portrait_01_f_01.mesh",
        "biogenesis/bio_01_portrait_01_f_01_idle1.anim")]
    public void ARigWhoseTwoHalvesDisagreeAboutTheirNamespaceIsStillPosed(string mesh, string animation)
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        Skip.If(!File.Exists(Model(mesh)) || !File.Exists(Model(animation)), "Model not in this installation.");

        var pose = PortraitPose.Read(SafeFile.ReadAllBytes(Model(animation)));
        var loaded = PortraitMesh.Load(SafeFile.ReadAllBytes(Model(mesh)));

        // The premise: not one bone of this model is named as its own animation names it.
        Assert.NotEmpty(loaded.Bones);
        Assert.DoesNotContain(loaded.Bones, b => pose.BoneNames.Contains(b.Name));

        // Placement only. These four carry pieces the renderer never draws — a part whose texture
        // the portrait replaces, a scrap left over from the rig — and those stretch the model's box
        // well past the figure inside it. Where the figure stands is the thing that was wrong.
        var min = pose.ApplyTo(loaded).Bounds.Min;

        Assert.InRange(min.Y, -8f, 4f);
    }

    [Fact]
    public void ARigWithTwoBonesOfTheSameBareNameIsMatchedStrictly()
    {
        // Dropping the namespace is only safe while it leaves the names apart. Where it does not,
        // there is no way to tell which bone was meant, and guessing is worse than not posing.
        var pose = PortraitPose.Of(new Dictionary<string, Matrix4x4>
        {
            ["left:Chest_joint"] = Matrix4x4.CreateTranslation(0, 10, 0),
            ["right:Chest_joint"] = Matrix4x4.CreateTranslation(0, -10, 0),
            ["spine"] = Matrix4x4.CreateTranslation(0, 3, 0),
        });

        Assert.False(pose.Describes("Chest_joint"));
        Assert.True(pose.Describes("anything:spine"));
        Assert.True(pose.Describes("left:Chest_joint"));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AModelWithNoSkeletonIsLeftWhereItIs()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The gestalt councillors are a single flat card with no bones. There is nothing to pose,
        // and pretending otherwise would move them for no reason.
        var flat = PortraitMesh.Load(SafeFile.ReadAllBytes(Model("paragon/portrait_gestalt_node.mesh")));
        var posed = PortraitPose.None.ApplyTo(flat);

        Assert.Equal(flat.Bounds, posed.Bounds);
    }
}
