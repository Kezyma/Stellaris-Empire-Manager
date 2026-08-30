using System.Numerics;
using Sem.Assets;

namespace Sem.MeshBake;

/// <summary>
/// The parts of drawing a triangle that do not depend on what is being drawn.
/// </summary>
/// <remarks>
/// Shared because there are two renderers and they must agree. A portrait is a stack of flat cards
/// and a ship is a solid hull, so they differ in how a triangle is placed and in what happens when
/// two of them land on the same pixel — but sampling a texture, blending a colour and shrinking the
/// finished image are the same work either way, and having one copy of it is what keeps a ship the
/// same colour as a portrait.
/// </remarks>
internal static class Raster
{
    /// <summary>
    /// Twice the signed area of a triangle, which is also the edge function the fill uses.
    /// </summary>
    /// <remarks>
    /// Positive on one side of the line and negative on the other, so three of them decide whether
    /// a point is inside, and their ratios to the whole are the barycentric weights.
    /// </remarks>
    public static float Edge(Vector3 a, Vector3 b, Vector3 c) =>
        ((c.X - a.X) * (b.Y - a.Y)) - ((c.Y - a.Y) * (b.X - a.X));

    /// <summary>One texel, in the order the images are stored.</summary>
    public static (byte R, byte G, byte B, byte A) Sample(DdsImage texture, Vector2 uv)
    {
        ArgumentNullException.ThrowIfNull(texture);

        // Both the coordinates and the image rows run downwards, so nothing is flipped: these
        // models follow the same convention their textures are stored in.
        var x = (int)(Wrap(uv.X) * (texture.Width - 1));
        var y = (int)(Wrap(uv.Y) * (texture.Height - 1));

        var (b, g, r, a) = texture[Math.Clamp(x, 0, texture.Width - 1), Math.Clamp(y, 0, texture.Height - 1)];
        return (r, g, b, a);
    }

    /// <summary>A texture coordinate brought back into the nought-to-one range.</summary>
    public static float Wrap(float value)
    {
        var wrapped = value - MathF.Floor(value);
        return float.IsFinite(wrapped) ? wrapped : 0f;
    }

    /// <summary>One channel of a new colour laid over an old one.</summary>
    public static byte Blend(byte existing, float incoming, float alpha) =>
        (byte)Math.Clamp((existing * (1 - alpha)) + (incoming * alpha), 0, 255);

    /// <summary>Averages each block of pixels down to one, which softens the edges.</summary>
    public static DdsImage Downsample(byte[] pixels, int width, int height, int factor)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if (factor <= 1)
        {
            return new DdsImage(width, height, pixels);
        }

        var outWidth = width / factor;
        var outHeight = height / factor;
        var result = new byte[outWidth * outHeight * 4];

        for (var y = 0; y < outHeight; y++)
        {
            for (var x = 0; x < outWidth; x++)
            {
                int b = 0, g = 0, r = 0, a = 0;

                for (var sy = 0; sy < factor; sy++)
                {
                    for (var sx = 0; sx < factor; sx++)
                    {
                        var source = ((((y * factor) + sy) * width) + (x * factor) + sx) * 4;
                        b += pixels[source];
                        g += pixels[source + 1];
                        r += pixels[source + 2];
                        a += pixels[source + 3];
                    }
                }

                var samples = factor * factor;
                var target = ((y * outWidth) + x) * 4;
                result[target] = (byte)(b / samples);
                result[target + 1] = (byte)(g / samples);
                result[target + 2] = (byte)(r / samples);
                result[target + 3] = (byte)(a / samples);
            }
        }

        return new DdsImage(outWidth, outHeight, result);
    }
}
