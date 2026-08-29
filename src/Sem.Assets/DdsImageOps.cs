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
