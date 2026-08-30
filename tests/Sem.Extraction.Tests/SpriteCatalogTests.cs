using Sem.Assets;
using Sem.Extraction.Extractors;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// Much of the game's artwork is packed several pictures to a file and referred to only by sprite
/// name. Resolving those names is what decides whether the designer shows the right picture at all,
/// so these check against artwork whose contents are known.
/// </summary>
public sealed class SpriteCatalogTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static SpriteCatalog? Catalog { get; } =
        InstallRoot is null ? null : SpriteCatalog.Read(LayeredContent.ForInstall(InstallRoot));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PlanetPicturesResolveToTheirFrameOfTheSharedStrip()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        // The frame numbers come from the order the game declares them in interface/planet.gfx.
        (string Sprite, int Frame)[] expected =
        [
            ("GFX_planet_type_desert", 1),
            ("GFX_planet_type_continental", 4),
            ("GFX_planet_type_ocean", 6),
            ("GFX_planet_type_gaia", 8),
            ("GFX_planet_type_habitat", 22),
        ];

        foreach (var (sprite, frame) in expected)
        {
            var resolved = Catalog!.Resolve(sprite);

            Assert.NotNull(resolved);
            Assert.Equal("gfx/interface/icons/planet_type_icons.dds", resolved.Texture);
            Assert.Equal(frame, resolved.Frame);
            Assert.Equal(46, resolved.FrameCount);
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheLargePlanetPicturesComeFromTheLargerStrip()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        var resolved = Catalog!.Resolve("GFX_planet_type_desert_big");

        Assert.NotNull(resolved);
        Assert.Equal("gfx/interface/icons/planet_type_big_icons.dds", resolved.Texture);
        Assert.Equal(1, resolved.Frame);
        Assert.Equal(46, resolved.FrameCount);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void CuttingOutAPlanetFrameGivesARoundPlanet()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        var frame = Catalog!.Resolve("GFX_planet_type_ocean_big")!;
        var strip = DdsReader.Read(SafeFile.ReadAllBytes(
            Path.Combine(InstallRoot!, frame.Texture.Replace('/', Path.DirectorySeparatorChar))));

        Assert.Equal((3496, 76), (strip.Width, strip.Height));

        var planet = DdsImageOps.Frame(strip, frame.Frame, frame.FrameCount);

        Assert.Equal((76, 76), (planet.Width, planet.Height));

        // A disc, not a square tile: the corners are empty and the middle is not. This is the whole
        // point of the change, since the artwork we used before was an opaque square of ground.
        Assert.Equal(0, planet[0, 0].A);
        Assert.Equal(0, planet[75, 0].A);
        Assert.Equal(0, planet[0, 75].A);
        Assert.Equal(0, planet[75, 75].A);
        Assert.True(planet[38, 38].A > 200, "The centre of a planet should be opaque.");

        var opaque = planet.Pixels.Where((_, i) => i % 4 == 3).Count(a => a > 128);
        var coverage = opaque / (double)(planet.Width * planet.Height);
        Assert.InRange(coverage, 0.55, 0.80);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void InlineTextIconsResolveDespiteTheInconsistentSpelling()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        // texticons.gfx spells the property textureFile on some entries and texturefile on others,
        // within the same file. Both of these must resolve or half the icons in an effects list go
        // missing.
        foreach (var code in new[] { "energy", "pop", "job", "minerals", "unity" })
        {
            var resolved = Catalog!.Resolve($"GFX_text_{code}");

            Assert.NotNull(resolved);
            Assert.EndsWith(".dds", resolved.Texture, StringComparison.OrdinalIgnoreCase);
            Assert.True(resolved.IsWholeTexture, $"GFX_text_{code} should be a file of its own.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsTheWholeInstallationsSprites()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        Assert.True(Catalog!.Count > 5000, $"Expected thousands of sprites, found {Catalog.Count}.");

        var sheeted = Catalog.Sprites.Values.Count(s => !s.IsWholeTexture);
        Assert.True(sheeted > 300, $"Expected hundreds of sheet frames, found {sheeted}.");
    }

    [Fact]
    public void AnUnknownSpriteNameResolvesToNothingRatherThanGuessing()
    {
        Assert.Null(SpriteCatalog.Empty.Resolve("GFX_not_a_real_sprite"));
        Assert.Null(SpriteCatalog.Empty.Resolve(null));
    }

    [Fact]
    public void AFrameOutsideTheStripIsRefused()
    {
        var image = new DdsImage(100, 10, new byte[100 * 10 * 4]);

        Assert.Throws<InvalidDataException>(() => DdsImageOps.Frame(image, 0, 10));
        Assert.Throws<InvalidDataException>(() => DdsImageOps.Frame(image, 11, 10));
    }

    [Fact]
    public void SplittingAChannelKeepsThatChannelsShapeAndDiscardsTheOthers()
    {
        // One pixel: red at full, green half, blue none.
        var image = new DdsImage(1, 1, [0, 128, 255, 255]);

        Assert.Equal(255, DdsImageOps.AlphaFromChannel(image, ColorChannel.Red)[0, 0].A);
        Assert.Equal(128, DdsImageOps.AlphaFromChannel(image, ColorChannel.Green)[0, 0].A);
        Assert.Equal(0, DdsImageOps.AlphaFromChannel(image, ColorChannel.Blue)[0, 0].A);

        Assert.True(DdsImageOps.HasContent(image, ColorChannel.Red));
        Assert.False(DdsImageOps.HasContent(image, ColorChannel.Blue));
    }
}
