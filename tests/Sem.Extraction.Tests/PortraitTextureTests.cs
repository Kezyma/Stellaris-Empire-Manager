using System.Numerics;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;
using Sem.Rules;

namespace Sem.Extraction.Tests;

/// <summary>
/// A portrait is not one picture: its skin, its clothes and its hair are chosen separately, and
/// where they are chosen from is not always the mesh. Getting this wrong is what left sixteen
/// portraits blank and stripped the clothing off the rest.
/// </summary>
public sealed class PortraitTextureTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AMeshNamesTheTextureItsShaderCallsFor()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(Path.Combine(
            InstallRoot!, "gfx", "models", "portraits", "humanoid", "humanoid_hp_11.mesh")));

        // The shader is what says whether a part is body, clothing or hair. Without it there is no
        // way to know which of a portrait's three textures a part wants.
        Assert.Contains(mesh.Parts, p => p.Name == "bodyShape" && p.Kind == PartKind.Character);
        Assert.Contains(mesh.Parts, p => p.Name == "outfitShape" && p.Kind == PartKind.Clothes);
        Assert.Contains(mesh.Parts, p => p.Name == "beardsShape" && p.Kind == PartKind.Attachment);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AMeshMayNameNoTextureAtAll()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // These are the portraits that came out blank: nothing in the model says what to wear, and
        // the portrait's own definition has to supply all of it.
        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(Path.Combine(
            InstallRoot!, "gfx", "models", "portraits", "psionics", "psionic_02_portrait.mesh")));

        Assert.NotEmpty(mesh.Parts);
        Assert.All(mesh.Parts, p => Assert.Null(p.Texture));
    }

    [Fact]
    public void APortraitWearsWhatItsDefinitionSaysBeforeWhatItsMeshSays()
    {
        var wearing = new PortraitTextures("skin.dds", "coat.dds", "hair.dds");

        Assert.Equal("skin.dds", wearing.For(PartKind.Character));
        Assert.Equal("coat.dds", wearing.For(PartKind.Clothes));
        Assert.Equal("hair.dds", wearing.For(PartKind.Attachment));
    }

    [Fact]
    public void APortraitThatSaysNothingLeavesItToTheMesh()
    {
        Assert.Null(PortraitTextures.None.For(PartKind.Character));
        Assert.Null(PortraitTextures.None.For(PartKind.Clothes));
        Assert.Null(PortraitTextures.None.For(PartKind.Attachment));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnEntityCarryingNoModelIsFollowedToTheOneThatDoes()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // Slender Molluscoid 05 keeps an empty locator where its model should be and hangs the real
        // one off it as an attachment. Stopping at the locator is why it had no likeness at all.
        // Measuring draws but never writes, so nothing here may write either.
        var baker = new PortraitBaker(LayeredContent.ForInstall(InstallRoot!), new SafeFile(WritePolicy.DenyAll));

        var measured = baker.Measure(["mol5", "mol4"]);

        Assert.Equal(2, measured.Count);
        Assert.All(measured, m => Assert.InRange(m.Rise, 8f, 20f));
    }

    [Fact]
    public void AModelsBoundsCoverEveryPartOfIt()
    {
        var mesh = new PortraitMesh(
        [
            Part("head", count: 279, low: 8.7f, high: 17.3f),
            Part("body", count: 76, low: 0f, high: 12.1f),
        ]);

        var (min, max) = mesh.Bounds;

        Assert.Equal(0f, min.Y);
        Assert.Equal(17.3f, max.Y);
    }

    private static MeshPart Part(string name, int count, float low, float high)
    {
        var positions = new Vector3[count];

        for (var i = 0; i < count; i++)
        {
            positions[i] = new Vector3(0, i == 0 ? low : i == 1 ? high : (low + high) / 2, 0);
        }

        return new MeshPart(name, positions, [], [], [0, 1, 2], null);
    }
}
