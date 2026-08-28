using Sem.GameData;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// The flag drawing maths. Free of any imaging dependency so the same code runs on the desktop and
/// in the browser, and testable without either.
/// </summary>
public sealed class FlagComposerTests
{
    private static readonly Dictionary<string, FlagColorDefinition> Palette = new(StringComparer.Ordinal)
    {
        ["red"] = new FlagColorDefinition("red", 158, 22, 22),
        ["black"] = new FlagColorDefinition("black", 27, 27, 27),
        ["white"] = new FlagColorDefinition("white", 255, 255, 255),
        ["blue"] = new FlagColorDefinition("blue", 20, 40, 200),
    };

    /// <summary>A one-pixel layer of a single shade.</summary>
    private static FlagLayer Solid(byte value, byte alpha = 255) =>
        new(1, 1, [value, value, value, alpha]);

    private static (byte R, byte G, byte B) Pixel(byte[] composed) =>
        (composed[2], composed[1], composed[0]);

    [Fact]
    public void ABrightBackgroundTakesTheFirstColour()
    {
        var composed = FlagComposer.Compose(Solid(255), null, ["red", "black", "null", "null"], Palette, 1, 1);

        Assert.Equal((158, 22, 22), Pixel(composed));
    }

    [Fact]
    public void ADarkBackgroundTakesTheSecondColour()
    {
        var composed = FlagComposer.Compose(Solid(0), null, ["red", "black", "null", "null"], Palette, 1, 1);

        Assert.Equal((27, 27, 27), Pixel(composed));
    }

    [Fact]
    public void AMidToneBackgroundSitsBetweenTheTwoColours()
    {
        var composed = FlagComposer.Compose(Solid(128), null, ["white", "black", "null", "null"], Palette, 1, 1);
        var (r, g, b) = Pixel(composed);

        Assert.InRange(r, 100, 180);
        Assert.Equal(r, g);
        Assert.Equal(g, b);
    }

    [Fact]
    public void TheEmblemTakesTheThirdColourWhereItIsOpaque()
    {
        var composed = FlagComposer.Compose(
            Solid(255),
            Solid(255),
            ["black", "black", "red", "null"],
            Palette,
            1,
            1);

        Assert.Equal((158, 22, 22), Pixel(composed));
    }

    [Fact]
    public void TheEmblemLeavesTheBackgroundAloneWhereItIsTransparent()
    {
        var composed = FlagComposer.Compose(
            Solid(255),
            Solid(255, alpha: 0),
            ["red", "black", "white", "null"],
            Palette,
            1,
            1);

        Assert.Equal((158, 22, 22), Pixel(composed));
    }

    [Fact]
    public void AnEmblemWithNoColourOfItsOwnUsesThePrimary()
    {
        // An empire with a two-colour scheme leaves the third slot empty, and the emblem must
        // still be drawn in something.
        var composed = FlagComposer.Compose(
            Solid(0),
            Solid(255),
            ["blue", "black", "null", "null"],
            Palette,
            1,
            1);

        Assert.Equal((20, 40, 200), Pixel(composed));
    }

    [Fact]
    public void AFlagIsAlwaysOpaque()
    {
        var composed = FlagComposer.Compose(
            Solid(128, alpha: 0),
            Solid(128, alpha: 0),
            ["red", "black", "white", "null"],
            Palette,
            2,
            2);

        Assert.All(composed.Where((_, i) => i % 4 == 3), a => Assert.Equal(255, a));
    }

    [Fact]
    public void LayersOfDifferentSizesAreCombined()
    {
        // Backgrounds are 400 pixels square and emblems come in several sizes.
        var background = new FlagLayer(2, 2, new byte[2 * 2 * 4]);
        Array.Fill(background.Pixels, (byte)255);

        var emblem = Solid(255);

        var composed = FlagComposer.Compose(
            background, emblem, ["red", "black", "white", "null"], Palette, 4, 4);

        Assert.Equal(4 * 4 * 4, composed.Length);
        Assert.Equal((255, 255, 255), Pixel(composed));
    }

    [Fact]
    public void AnUnknownColourNameDoesNotBreakTheFlag()
    {
        // A design naming a colour a later patch removed should still draw something.
        var composed = FlagComposer.Compose(
            Solid(255), null, ["not_a_colour", "black", "null", "null"], Palette, 1, 1);

        Assert.Equal(4, composed.Length);
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("", null)]
    [InlineData("red", "red")]
    public void EmptySlotsAreReadAsAbsentRatherThanAsAColour(string value, string? expected)
    {
        var resolved = FlagComposer.Resolve([value], 0, Palette);

        Assert.Equal(expected is null, resolved is null);
    }

    [Fact]
    public void AskingForASlotBeyondTheEndIsAbsentRatherThanAnError()
    {
        Assert.Null(FlagComposer.Resolve(["red"], 3, Palette));
    }
}
