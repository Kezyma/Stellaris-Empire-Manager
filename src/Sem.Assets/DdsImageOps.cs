namespace Sem.Assets;

/// <summary>One colour channel of a texture.</summary>
public enum ColorChannel
{
    Red,
    Green,
    Blue,
}

/// <summary>
/// Ways of taking part of a decoded texture, for the two cases where the game packs more than one
/// picture into a single file.
/// </summary>
public static class DdsImageOps
{
    /// <summary>
    /// Takes one picture out of a texture holding several of them side by side.
    /// </summary>
    /// <remarks>
    /// Frames run left to right and are numbered from one. The planet strip, for instance, is 46
    /// frames across 3,496 pixels — 76 apiece — with desert first at the very left.
    /// </remarks>
    /// <param name="image">The whole texture.</param>
    /// <param name="frame">Which frame to take, counting from one.</param>
    /// <param name="frameCount">How many frames the texture is divided into.</param>
    public static DdsImage Frame(DdsImage image, int frame, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(image);

        var width = frameCount > 0 ? image.Width / frameCount : 0;

        if (width <= 0 || frame < 1 || frame > frameCount)
        {
            throw new InvalidDataException(
                $"Frame {frame} of {frameCount} does not fit a texture {image.Width} pixels wide.");
        }

        var left = (frame - 1) * width;
        var pixels = new byte[width * image.Height * 4];

        for (var y = 0; y < image.Height; y++)
        {
            var source = ((y * image.Width) + left) * 4;
            Array.Copy(image.Pixels, source, pixels, y * width * 4, width * 4);
        }

        return new DdsImage(width, image.Height, pixels);
    }

    /// <summary>
    /// Turns one colour channel into a white picture whose transparency is that channel.
    /// </summary>
    /// <remarks>
    /// The flag backgrounds pack three independent shapes into one picture's red, green and blue.
    /// Separating them is what allows each to stencil its own colour, which is what the game's
    /// shader does with the channels directly.
    /// </remarks>
    public static DdsImage AlphaFromChannel(DdsImage image, ColorChannel channel)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Pixels are stored blue, green, red, alpha.
        var offset = channel switch
        {
            ColorChannel.Blue => 0,
            ColorChannel.Green => 1,
            _ => 2,
        };

        var pixels = new byte[image.Pixels.Length];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 255;
            pixels[i + 2] = 255;
            pixels[i + 3] = image.Pixels[i + offset];
        }

        return new DdsImage(image.Width, image.Height, pixels);
    }

    /// <summary>
    /// Paints a picture with a colour, which is how the game colours a shape it draws many times.
    /// </summary>
    /// <remarks>
    /// Multiplied rather than replaced, which is what the game's own <c>color</c> does. The two
    /// readings agree wherever the artwork is white — which is how these shapes are drawn, so that
    /// one background can be mint for a benefit and red for a drawback — and multiplying is the one
    /// that goes on agreeing where the artwork is shaded.
    /// </remarks>
    public static DdsImage Tint(DdsImage image, (byte R, byte G, byte B, byte A) color)
    {
        ArgumentNullException.ThrowIfNull(image);

        var pixels = new byte[image.Pixels.Length];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Pixels are stored blue, green, red, alpha.
            pixels[i] = (byte)(image.Pixels[i] * color.B / 255);
            pixels[i + 1] = (byte)(image.Pixels[i + 1] * color.G / 255);
            pixels[i + 2] = (byte)(image.Pixels[i + 2] * color.R / 255);
            pixels[i + 3] = (byte)(image.Pixels[i + 3] * color.A / 255);
        }

        return new DdsImage(image.Width, image.Height, pixels);
    }

    /// <summary>
    /// Draws one picture over another, centred, keeping whatever shows through.
    /// </summary>
    /// <remarks>
    /// The layers of an icon are authored at their own sizes and the game centres them on each
    /// other — a trait's background is 29 across, its councillor badge 32 — so a stack is as wide as
    /// its widest layer rather than as its first. Ordinary source-over compositing, with the result
    /// left unmultiplied so it can be stacked again.
    /// </remarks>
    public static DdsImage Over(DdsImage under, DdsImage over)
    {
        ArgumentNullException.ThrowIfNull(under);
        ArgumentNullException.ThrowIfNull(over);

        var width = Math.Max(under.Width, over.Width);
        var height = Math.Max(under.Height, over.Height);
        var pixels = new byte[width * height * 4];

        Draw(under);
        Draw(over);

        return new DdsImage(width, height, pixels);

        void Draw(DdsImage layer)
        {
            var offsetX = (width - layer.Width) / 2;
            var offsetY = (height - layer.Height) / 2;

            for (var y = 0; y < layer.Height; y++)
            {
                for (var x = 0; x < layer.Width; x++)
                {
                    var source = ((y * layer.Width) + x) * 4;
                    var alpha = layer.Pixels[source + 3];

                    if (alpha == 0)
                    {
                        continue;
                    }

                    var target = (((y + offsetY) * width) + x + offsetX) * 4;

                    for (var channel = 0; channel < 3; channel++)
                    {
                        pixels[target + channel] = (byte)(
                            ((layer.Pixels[source + channel] * alpha) + (pixels[target + channel] * (255 - alpha)))
                            / 255);
                    }

                    pixels[target + 3] = (byte)(alpha + (pixels[target + 3] * (255 - alpha) / 255));
                }
            }
        }
    }

    /// <summary>Whether a channel carries any shape at all.</summary>
    public static bool HasContent(DdsImage image, ColorChannel channel)
    {
        ArgumentNullException.ThrowIfNull(image);

        var offset = channel switch
        {
            ColorChannel.Blue => 0,
            ColorChannel.Green => 1,
            _ => 2,
        };

        for (var i = offset; i < image.Pixels.Length; i += 4)
        {
            if (image.Pixels[i] > 8)
            {
                return true;
            }
        }

        return false;
    }
}
