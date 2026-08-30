using Sem.Assets;
using Sem.Io;
using SkiaSharp;

namespace Sem.Extraction.Tests;

/// <summary>
/// Decodes real game textures. The formats are simple but the details bite: a background with no
/// alpha channel must come out opaque rather than invisible, and a compressed emblem must keep its
/// transparency.
/// </summary>
public sealed class ImagePipelineTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static DdsImage Read(string relativePath) =>
        DdsReader.Read(SafeFile.ReadAllBytes(Path.Combine(InstallRoot!, relativePath.Replace('/', Path.DirectorySeparatorChar))));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsAnUncompressedIcon()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var image = Read("gfx/interface/icons/ethics/ethic_militarist.dds");

        Assert.Equal((29, 29), (image.Width, image.Height));
        Assert.Equal(image.Width * image.Height * 4, image.Pixels.Length);

        // An icon is mostly transparent around a visible symbol, so both must be present.
        Assert.Contains(image.Pixels.Where((_, i) => i % 4 == 3), a => a == 0);
        Assert.Contains(image.Pixels.Where((_, i) => i % 4 == 3), a => a > 200);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsACompressedEmblemAndKeepsItsTransparency()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var image = Read("flags/human/flag_human_1.dds");

        Assert.Equal((256, 256), (image.Width, image.Height));

        var alphas = image.Pixels.Where((_, i) => i % 4 == 3).ToList();
        Assert.Contains(alphas, a => a < 20);
        Assert.Contains(alphas, a => a > 200);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ABackgroundWithNoAlphaChannelComesOutOpaque()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // Flag backgrounds are 24-bit. Reading a channel that is not there would make every one of
        // them fully transparent, and every flag in the app would lose its background.
        var image = Read("flags/backgrounds/v.dds");

        Assert.Equal((400, 400), (image.Width, image.Height));
        Assert.All(image.Pixels.Where((_, i) => i % 4 == 3), a => Assert.Equal(255, a));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsARoomBackgroundAtTheSizeTheGameStoresIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var image = Read("gfx/portraits/city_sets/default_room.dds");

        Assert.Equal((952, 340), (image.Width, image.Height));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryTextureTheGameShipsForTheDesignerDecodes()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        string[] directories =
        [
            "flags",
            "gfx/interface/icons/ethics",
            "gfx/interface/icons/governments",
            "gfx/interface/icons/origins",
            "gfx/interface/icons/traits",
        ];

        var decoded = 0;
        var failures = new List<string>();

        foreach (var directory in directories)
        {
            var full = Path.Combine(InstallRoot!, directory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(full, "*.dds", SearchOption.AllDirectories))
            {
                // The game ships one empty placeholder with a texture's extension. Nothing can
                // decode no bytes, and nothing references it.
                if (new FileInfo(path).Length == 0)
                {
                    continue;
                }

                try
                {
                    var image = DdsReader.Read(SafeFile.ReadAllBytes(path));
                    Assert.True(image.Width > 0 && image.Height > 0);
                    decoded++;
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                {
                    failures.Add($"{Path.GetRelativePath(InstallRoot!, path)}: {ex.Message}");
                }
            }
        }

        Assert.True(decoded > 1500, $"Expected a large corpus of textures, decoded {decoded}.");
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {decoded} textures failed to decode:\r\n" + string.Join("\r\n", failures.Take(20)));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EncodingProducesAReadablePngOfTheSameSize()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var image = Read("gfx/interface/icons/ethics/ethic_militarist.dds");
        var png = PngWriter.Encode(image);

        using var decoded = SKBitmap.Decode(png);
        Assert.Equal((image.Width, image.Height), (decoded.Width, decoded.Height));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EncodingScalesLargeImagesDownButLeavesSmallOnesAlone()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var room = Read("gfx/portraits/city_sets/default_room.dds");

        using var scaled = SKBitmap.Decode(PngWriter.Encode(room, maxDimension: 480));
        Assert.Equal(480, scaled.Width);
        Assert.Equal(171, scaled.Height);

        // The aspect ratio survives, give or take the rounding to whole pixels.
        Assert.Equal(room.Width / (double)room.Height, scaled.Width / (double)scaled.Height, tolerance: 0.02);

        var icon = Read("gfx/interface/icons/ethics/ethic_militarist.dds");
        using var unscaled = SKBitmap.Decode(PngWriter.Encode(icon, maxDimension: 480));
        Assert.Equal(29, unscaled.Width);
    }

    [Fact]
    public void RefusesSomethingThatIsNotADdsFile()
    {
        Assert.Throws<InvalidDataException>(() => DdsReader.Read("not a texture"u8));
    }

    [Fact]
    public void TintingMultipliesRatherThanReplaces()
    {
        // The game colours one shared shape many ways, which is how one trait background is mint for
        // a benefit and red for a drawback. Multiplying is what its own colour does; on the white
        // artwork these shapes are drawn in it comes out as the colour itself, and on anything
        // shaded it goes on behaving.
        var tinted = DdsImageOps.Tint(Solid(2, 2, 255, 255, 255, 255), (48, 223, 185, 255));

        Assert.Equal([185, 223, 48, 255], tinted.Pixels[..4]);

        var halved = DdsImageOps.Tint(Solid(1, 1, 128, 128, 128, 255), (255, 255, 255, 128));

        Assert.Equal(128, halved.Pixels[3]);
        Assert.Equal(128, halved.Pixels[2]);
    }

    [Fact]
    public void StackingCentresTheLayersAndKeepsWhatShowsThrough()
    {
        // Trait layers are authored at their own sizes — the background is 29 across and the
        // councillor badge 32 — and the game centres them on each other, so a stack is as big as
        // its largest layer rather than as its first.
        var under = Solid(4, 4, 0, 0, 255, 255);
        var over = Solid(2, 2, 255, 0, 0, 255);

        var stacked = DdsImageOps.Over(under, over);

        Assert.Equal((4, 4), (stacked.Width, stacked.Height));

        // The middle is the top layer; the corner is still the bottom one. Read out by name, since
        // a pixel comes back blue first.
        var (_, _, middleRed, _) = stacked[1, 1];
        var (cornerBlue, _, _, _) = stacked[0, 0];

        Assert.Equal(255, middleRed);
        Assert.Equal(255, cornerBlue);
    }

    [Fact]
    public void ATransparentLayerLeavesWhatIsBeneathIt()
    {
        var stacked = DdsImageOps.Over(Solid(2, 2, 10, 20, 30, 255), Solid(2, 2, 0, 0, 0, 0));
        var (blue, green, red, alpha) = stacked[0, 0];

        Assert.Equal((10, 20, 30, 255), (red, green, blue, alpha));
    }

    /// <summary>A picture of one colour, for testing the operations rather than the decoder.</summary>
    private static DdsImage Solid(int width, int height, byte r, byte g, byte b, byte a)
    {
        var pixels = new byte[width * height * 4];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Stored blue, green, red, alpha.
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (b, g, r, a);
        }

        return new DdsImage(width, height, pixels);
    }

    [Fact]
    public void RejectsAnExtendedHeaderClearlyRatherThanReadingRubbish()
    {
        // Stellaris ships none of these, but a mod might, and a wrong picture is worse than none.
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(64).CopyTo(header, 12);
        BitConverter.GetBytes(64).CopyTo(header, 16);
        BitConverter.GetBytes(0x4).CopyTo(header, 80);
        "DX10"u8.CopyTo(header.AsSpan(84));

        var error = Assert.Throws<NotSupportedException>(() => DdsReader.Read(header));
        Assert.Contains("extended header", error.Message, StringComparison.Ordinal);
    }
}
