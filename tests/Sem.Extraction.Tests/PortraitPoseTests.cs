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
