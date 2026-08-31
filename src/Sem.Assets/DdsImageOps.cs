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

    /// <summary>
    /// Dresses a portrait's skin in one of its ascended forms.
    /// </summary>
    /// <remarks>
    /// This is <c>gfx/FX/pdxmesh.shader</c> written out, from the branch guarded by
    /// <c>CustomDiffuseTexture</c> around line 1160, and it is worth stating exactly because it is
    /// not what the names suggest. The mask does not say where the decal applies. It says where the
    /// decal is <em>blended</em>: a pixel matching the cyan key gets the two mixed, and every other
    /// pixel takes the decal outright. So a decal is a whole replacement skin with the parts that
    /// should show the original underneath painted cyan in its mask.
    ///
    /// The key is <c>{ 0 255 255 255 }</c>, which the portrait database also states as
    /// <c>mask_color</c> in the <c>portrait_evolution</c> block, compared on all four channels
    /// within a tenth. The blend itself is the shader's, unusual middle term and all:
    ///
    /// <code>
    ///     scalar = fa + (1 - fa) * fa / (fa + ba)
    ///     rgb    = lerp(character, decal, scalar)
    ///     a      = fa + (1 - fa) * ba
    /// </code>
    ///
    /// where <c>fa</c> is the decal's alpha and <c>ba</c> the skin's. Guessing it would have got the
    /// masked and unmasked cases the wrong way round.
    /// </remarks>
    /// <param name="character">The skin the portrait wears unascended.</param>
    /// <param name="decal">The ascended skin.</param>
    /// <param name="mask">Which pixels blend rather than replace.</param>
    public static DdsImage BlendEvolution(DdsImage character, DdsImage decal, DdsImage mask)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(decal);
        ArgumentNullException.ThrowIfNull(mask);

        var pixels = new byte[character.Pixels.Length];

        for (var y = 0; y < character.Height; y++)
        {
            for (var x = 0; x < character.Width; x++)
            {
                var i = ((y * character.Width) + x) * 4;
                var d = Sample(decal, x, y, character);
                var m = Sample(mask, x, y, character);

                if (!IsBlendKey(mask.Pixels, m))
                {
                    Array.Copy(decal.Pixels, d, pixels, i, 4);
                    continue;
                }

                float fa = decal.Pixels[d + 3] / 255f;
                float ba = character.Pixels[i + 3] / 255f;
                var total = fa + ba;

                // Both transparent: nothing to mix, and the division below would not be defined.
                var scalar = total > 0.00001f ? fa + ((1f - fa) * fa / total) : 0f;

                for (var c = 0; c < 3; c++)
                {
                    pixels[i + c] = (byte)Math.Clamp(
                        Math.Round(character.Pixels[i + c] + ((decal.Pixels[d + c] - character.Pixels[i + c]) * scalar)),
                        0,
                        255);
                }

                pixels[i + 3] = (byte)Math.Clamp(Math.Round((fa + ((1f - fa) * ba)) * 255f), 0, 255);
            }
        }

        return new DdsImage(character.Width, character.Height, pixels);
    }

    /// <summary>
    /// Where in <paramref name="other"/> the pixel at (x, y) of <paramref name="like"/> falls.
    /// </summary>
    /// <remarks>
    /// Sampled by coordinate rather than read by index, because the shader samples all three at one
    /// UV and they are not all the same size. The synthetic portraits are why: their mask is four
    /// pixels square against a 512-pixel skin, and read by index it would have covered sixteen
    /// pixels of a quarter of a million and left every synthetic ascension looking untouched.
    ///
    /// Nearest neighbour, which for the cases that exist is exact: a mask is either the same size as
    /// the skin or a flat colour blown up from a handful of pixels, and neither wants interpolating.
    /// </remarks>
    private static int Sample(DdsImage other, int x, int y, DdsImage like)
    {
        if (other.Width == like.Width && other.Height == like.Height)
        {
            return ((y * like.Width) + x) * 4;
        }

        var sx = Math.Min(other.Width - 1, x * other.Width / Math.Max(1, like.Width));
        var sy = Math.Min(other.Height - 1, y * other.Height / Math.Max(1, like.Height));

        return ((sy * other.Width) + sx) * 4;
    }

    /// <summary>
    /// Whether a mask pixel is the cyan that means "blend here", within the shader's tenth.
    /// </summary>
    /// <remarks>
    /// Pixels are stored blue, green, red, alpha. A tenth of full scale is 25.5, so blue, green and
    /// alpha must exceed 229.5 and red must fall below 25.5.
    /// </remarks>
    private static bool IsBlendKey(byte[] pixels, int offset) =>
        pixels[offset] > 229.5f &&
        pixels[offset + 1] > 229.5f &&
        pixels[offset + 2] < 25.5f &&
        pixels[offset + 3] > 229.5f;
}
