using System.Reflection;
using Sem.Extraction;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// The textures a mesh declaration puts on its own named parts.
/// </summary>
/// <remarks>
/// <para>
/// A <c>pdxmesh</c> is a mesh file plus a set of <c>meshsettings</c>, and several declarations may
/// name one file. The Extradimensionals are the case the game actually ships: four portraits, one
/// model, and eleven lines of <c>texture_diffuse</c> each to tell them apart. Read only by file,
/// all four wore whatever the shared model bakes - which is the fourth one's skin, so even
/// <c>exd1</c> came out as <c>exd4</c>.
/// </para>
/// <para>
/// Two things are asserted here because two things went wrong the first time this was attempted.
/// The declarations must be keyed by <em>name</em>, or four blocks sharing a file overwrite each
/// other and one picture comes out again; and each declaration keeps its own table, or the generic
/// shape names every portrait family uses - <c>bodyShape</c>, <c>headShape</c> - leak across
/// families and repaint portraits that were never wrong.
/// </para>
/// </remarks>
public sealed class PortraitMeshSettingsTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    /// <summary>
    /// Reaches the index the baker builds for itself.
    /// </summary>
    /// <remarks>
    /// Reflected for the reason <see cref="PortraitOverrideTests"/> reflects: the alternative was
    /// opening up an internal that is what the baker is made of rather than a surface anything else
    /// should call.
    /// </remarks>
    private static IReadOnlyDictionary<(string, int), string>? Declared(
        PortraitBaker baker, string mesh)
    {
        var index = typeof(PortraitBaker)
            .GetMethod("BuildIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(baker, [])!;

        return (IReadOnlyDictionary<(string, int), string>?)index.GetType()
            .GetMethod("Declared", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(index, [mesh]);
    }

    private static PortraitBaker Baker(string gfx)
    {
        var content = new InMemoryContentSource()
            .Add("gfx/models/portraits/test/_test_meshes.gfx", gfx);

        // Measuring and reading draw nothing, so nothing here may write either.
        return new PortraitBaker(content.AsContent(), new SafeFile(WritePolicy.DenyAll));
    }

    /// <summary>Two declarations over one file keep their own textures.</summary>
    [Fact]
    public void DeclarationsSharingOneFileAreToldApartByName()
    {
        var baker = Baker("""
            objectTypes = {
                pdxmesh = {
                    name = "portrait_blue_mesh"
                    file = "gfx/models/portraits/test/shared_portrait.mesh"
                    meshsettings = { name = "bodyShape"  texture_diffuse = "blue.dds" }
                    meshsettings = { name = "headShape"  texture_diffuse = "blue.dds" }
                }

                pdxmesh = {
                    name = "portrait_orange_mesh"
                    file = "gfx/models/portraits/test/shared_portrait.mesh"
                    meshsettings = { name = "bodyShape"  texture_diffuse = "orange.dds" }
                    meshsettings = { name = "headShape"  texture_diffuse = "orange.dds" }
                }
            }
            """);

        Assert.Equal("blue.dds", Declared(baker, "portrait_blue_mesh")![("bodyShape", 0)]);
        Assert.Equal("orange.dds", Declared(baker, "portrait_orange_mesh")![("bodyShape", 0)]);
    }

    /// <summary>
    /// And a shape name one family uses says nothing about another's.
    /// </summary>
    /// <remarks>
    /// The mammalian declarations carry no <c>meshsettings</c> at all, so nothing may reach them -
    /// and the first attempt at this repainted two of them, which is how a flat table keyed on
    /// <c>bodyShape</c> alone announces itself.
    /// </remarks>
    [Fact]
    public void ADeclarationSaysNothingAboutAnyOther()
    {
        var baker = Baker("""
            objectTypes = {
                pdxmesh = {
                    name = "portrait_painted_mesh"
                    file = "gfx/models/portraits/test/painted.mesh"
                    meshsettings = { name = "bodyShape"  texture_diffuse = "painted.dds" }
                }

                pdxmesh = {
                    name = "portrait_bare_mesh"
                    file = "gfx/models/portraits/test/bare.mesh"
                }
            }
            """);

        Assert.Null(Declared(baker, "portrait_bare_mesh"));
        Assert.Null(Declared(baker, "portrait_absent_mesh"));
    }

    /// <summary>
    /// A setting with no diffuse, or no name, is not an answer about colour.
    /// </summary>
    /// <remarks>
    /// The lithoids declare named settings carrying only a normal and a specular map, and the
    /// backgrounds declare unnamed ones meaning the mesh entire. Neither says which texture a given
    /// shape wears, which is the only question being asked here.
    /// </remarks>
    [Fact]
    public void ASettingThatNamesNoTextureOrNoShapeIsSkipped()
    {
        var baker = Baker("""
            objectTypes = {
                pdxmesh = {
                    name = "portrait_partial_mesh"
                    file = "gfx/models/portraits/test/partial.mesh"
                    meshsettings = { name = "bodyShape"  texture_normal = "bumps.dds" }
                    meshsettings = { texture_diffuse = "whole.dds" }
                    meshsettings = { name = "headShape"  texture_diffuse = "face.dds" }
                }
            }
            """);

        var declared = Declared(baker, "portrait_partial_mesh")!;

        Assert.Equal("face.dds", declared[("headShape", 0)]);
        Assert.Single(declared);
    }

    /// <summary>
    /// The game's own four, which is what this was all for.
    /// </summary>
    /// <remarks>
    /// The one place in the whole of the game where several portrait keys share a mesh and are told
    /// apart by their declarations. Everything else that repeats a picture repeats it on purpose:
    /// robot1-3 are aliases kept for pre-1.8 saves, and the two salvagers that match are given the
    /// same texture by name.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheFourExtradimensionalsDeclareFourDifferentSkins()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var baker = new PortraitBaker(
            LayeredContent.ForInstall(InstallRoot!), new SafeFile(WritePolicy.DenyAll));

        for (var colour = 1; colour <= 4; colour++)
        {
            var declared = Declared(baker, $"portrait_extradimensional_0{colour}_mesh");

            Assert.NotNull(declared);
            Assert.Equal($"extradimensional_0{colour}.dds", declared![("bodyShape", 0)]);

            // The fiery head, which the model itself leaves blank - so this is the one part that
            // has never been drawn on any of them, and it differs by colour too.
            Assert.Equal(
                $"extradimensional_0{colour}_flowmap_fire_head_color.dds",
                declared[("hairShape", 0)]);
        }
    }

    /// <summary>And the mammalians, whose declarations carry no settings at all.</summary>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void NoMammalianDeclarationSaysAnythingAboutItsParts()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var baker = new PortraitBaker(
            LayeredContent.ForInstall(InstallRoot!), new SafeFile(WritePolicy.DenyAll));

        for (var face = 1; face <= 9; face++)
        {
            Assert.Null(Declared(baker, $"portrait_mammalian_0{face}_mesh"));
        }
    }
}
