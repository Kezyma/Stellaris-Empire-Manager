namespace Sem.GameData;

/// <summary>
/// Draws an empire flag from its parts.
/// </summary>
/// <remarks>
/// <para>
/// Flags are not shipped as finished pictures. A flag is a greyscale background shape and a
/// transparent emblem, tinted with colours the player chooses, and pre-baking every combination of
/// seventy-two colours across hundreds of shapes is not worth contemplating. So the parts are
/// shipped and the flag is drawn when it is shown.
/// </para>
/// <para>
/// Deliberately free of any imaging dependency, so the same code runs in the desktop app and in
/// the browser rather than being written twice and drifting apart.
/// </para>
/// </remarks>
public static class FlagComposer
{
    /// <summary>The placeholder an unused colour slot holds.</summary>
    public const string EmptyColor = "null";

    /// <summary>What is used when a design names a colour the game does not define.</summary>
    private static readonly (byte R, byte G, byte B) Fallback = (128, 128, 128);

    /// <summary>
    /// Draws a flag into a pixel buffer, four bytes per pixel in blue, green, red, alpha order.
    /// </summary>
    /// <param name="background">
    /// The background shape, greyscale, where brightness chooses between the first two colours.
    /// </param>
    /// <param name="emblem">
    /// The emblem laid over it, where transparency decides what shows through. May be null for a
    /// flag that is only a background.
    /// </param>
    /// <param name="colors">The design's four colour slots, unused ones holding the placeholder.</param>
    /// <param name="palette">The named colours the game defines.</param>
    /// <param name="width">Width of the output.</param>
    /// <param name="height">Height of the output.</param>
    public static byte[] Compose(
        FlagLayer background,
        FlagLayer? emblem,
        IReadOnlyList<string> colors,
        IReadOnlyDictionary<string, FlagColorDefinition> palette,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var primary = Resolve(colors, 0, palette);
        var secondary = Resolve(colors, 1, palette);

        // The emblem takes the third colour, falling back to the primary when the player left the
        // slot empty, which is what an empire with a two-colour scheme does.
        var emblemColor = Resolve(colors, 2, palette) ?? primary ?? Fallback;

        // The background's bright areas take the first colour and its dark areas the second,
        // which is why a solid background comes out entirely in the primary colour.
        var light = primary ?? Fallback;
        var dark = secondary ?? ((byte)0, (byte)0, (byte)0);

        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;

                // Brightness picks a point between the two background colours.
                var (br, bg, bb, _) = background.Sample(x, y, width, height);
                var brightness = Luminance(br, bg, bb);

                var r = Mix(dark.Item1, light.R, brightness);
                var g = Mix(dark.Item2, light.G, brightness);
                var b = Mix(dark.Item3, light.B, brightness);

                if (emblem is not null)
                {
                    var (_, _, _, alpha) = emblem.Sample(x, y, width, height);
                    if (alpha > 0)
                    {
                        var weight = alpha / 255.0;
                        r = Mix(r, emblemColor.R, weight);
                        g = Mix(g, emblemColor.G, weight);
                        b = Mix(b, emblemColor.B, weight);
                    }
                }

                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>Reads a colour slot, treating the placeholder and unknown names as absent.</summary>
    public static (byte R, byte G, byte B)? Resolve(
        IReadOnlyList<string> colors,
        int slot,
        IReadOnlyDictionary<string, FlagColorDefinition> palette)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(palette);

        if (slot < 0 || slot >= colors.Count)
        {
            return null;
        }

        var name = colors[slot];
        if (string.IsNullOrEmpty(name) || name == EmptyColor)
        {
            return null;
        }

        return palette.TryGetValue(name, out var color) ? (color.Red, color.Green, color.Blue) : null;
    }

    /// <summary>Perceived brightness, weighted the way an eye responds to each channel.</summary>
    private static double Luminance(byte r, byte g, byte b) =>
        ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255.0;

    private static byte Mix(byte from, byte to, double weight) =>
        (byte)Math.Clamp(Math.Round(from + ((to - from) * weight)), 0, 255);
}

/// <summary>
/// One image making up a flag, as decoded pixels.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Pixels">Four bytes per pixel in blue, green, red, alpha order.</param>
public sealed record FlagLayer(int Width, int Height, byte[] Pixels)
{
    /// <summary>
    /// Reads a pixel, scaling to the output size so layers of different resolutions can be
    /// combined. Backgrounds are 400 pixels square and emblems come in several sizes.
    /// </summary>
    public (byte R, byte G, byte B, byte A) Sample(int x, int y, int outputWidth, int outputHeight)
    {
        // A layer with no pixels reads as transparent rather than throwing. Math.Clamp raises when
        // its minimum exceeds its maximum, which a width of zero makes it do — so an empty or
        // unreadable layer crashed the whole flag instead of leaving a hole in it.
        if (Width <= 0 || Height <= 0)
        {
            return (0, 0, 0, 0);
        }

        var sourceX = outputWidth == Width ? x : Math.Clamp(x * Width / outputWidth, 0, Width - 1);
        var sourceY = outputHeight == Height ? y : Math.Clamp(y * Height / outputHeight, 0, Height - 1);

        var offset = ((sourceY * Width) + sourceX) * 4;

        return offset + 3 < Pixels.Length
            ? (Pixels[offset + 2], Pixels[offset + 1], Pixels[offset], Pixels[offset + 3])
            : ((byte)0, (byte)0, (byte)0, (byte)0);
    }
}
