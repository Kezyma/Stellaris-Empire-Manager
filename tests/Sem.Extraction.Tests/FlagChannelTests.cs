using Sem.Assets;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// A flag background is three separate shapes packed into one file's colour channels, which the
/// game multiplies by three chosen colours and adds together. Splitting them apart is what lets a
/// flag be drawn correctly, so the split has to keep each shape whole and put it in the right file.
/// </summary>
public sealed class FlagChannelTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static DdsImage Read(string relativePath) =>
        DdsReader.Read(SafeFile.ReadAllBytes(
            Path.Combine(InstallRoot!, relativePath.Replace('/', Path.DirectorySeparatorChar))));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ATwoToneBackgroundIsTwoShapes_NotOneShapeAtTwoBrightnesses()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // This is the whole reason for splitting the channels. The horizontal background is half
        // pure red and half pure green — two halves selecting two different colours. Read as
        // brightness instead, red is 0.21 and green 0.72, so the flag comes out as two shades of the
        // primary colour and the secondary never appears at all.
        var image = Read("flags/backgrounds/horizontal.dds");

        Assert.Equal(0.5, Coverage(DdsImageOps.AlphaFromChannel(image, ColorChannel.Red)), tolerance: 0.02);
        Assert.Equal(0.5, Coverage(DdsImageOps.AlphaFromChannel(image, ColorChannel.Green)), tolerance: 0.02);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheThirdColourNeverShowsOnAFlag()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The renderer supports three channels because the game's shader does, but no background the
        // game ships puts anything in the third. That is consistent with its flag editor offering
        // exactly two colour buttons, and it is why the third stored colour is free to be what it
        // actually is — the empire's colour on the galaxy map.
        var backgrounds = Directory
            .EnumerateFiles(Path.Combine(InstallRoot!, "flags", "backgrounds"), "*.dds")
            .ToList();

        var usingBlue = backgrounds
            .Where(path => DdsImageOps.HasContent(DdsReader.Read(SafeFile.ReadAllBytes(path)), ColorChannel.Blue))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(backgrounds.Count > 50, $"Expected the full set of backgrounds, found {backgrounds.Count}.");

        // One compressed background carries a trace of it, which is the codec rather than the artist.
        Assert.True(
            usingBlue.Count <= 1,
            $"{usingBlue.Count} backgrounds use the third colour: {string.Join(", ", usingBlue)}");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ASolidBackgroundIsAllOneColourAndNoneOfTheOthers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var image = Read("flags/backgrounds/00_solid.dds");

        Assert.Equal(1.0, Coverage(DdsImageOps.AlphaFromChannel(image, ColorChannel.Red)), tolerance: 0.01);
        Assert.Equal(0.0, Coverage(DdsImageOps.AlphaFromChannel(image, ColorChannel.Green)), tolerance: 0.01);
        Assert.Equal(0.0, Coverage(DdsImageOps.AlphaFromChannel(image, ColorChannel.Blue)), tolerance: 0.01);
    }

    private static double Coverage(DdsImage mask)
    {
        var opaque = 0;

        for (var i = 3; i < mask.Pixels.Length; i += 4)
        {
            if (mask.Pixels[i] > 128)
            {
                opaque++;
            }
        }

        return opaque / (double)(mask.Width * mask.Height);
    }
}
