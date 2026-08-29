using System.Numerics;
using Sem.Assets;

namespace Sem.MeshBake;

/// <summary>How a portrait is drawn.</summary>
public sealed record RenderSettings
{
    /// <summary>Width of the finished image, before supersampling.</summary>
    public int Width { get; init; } = 165;

    /// <summary>Height of the finished image, before supersampling.</summary>
    public int Height { get; init; } = 220;

    /// <summary>
    /// How much larger to draw before scaling down. Edges of a model rasterised at thumbnail size
    /// are harsh; drawing bigger and shrinking is the cheapest way to soften them.
    /// </summary>
    public int Supersample { get; init; } = 3;

    /// <summary>
    /// How much of the model, measured in its own units, the frame's height covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One value for every portrait, which is the point: a species is the size it was modelled at
    /// rather than the size of the frame. Fitting each one to its own bounds — which is what this
    /// used to do — is why a small species came out as large as a big one.
    /// </para>
    /// <para>
    /// The game's own frame is 380 pixels at a scale of 24, so it shows 15.8 units. Measured against
    /// all 406 portraits that clips a quarter of them, which suits a game that shows a character in
    /// a room rather better than it suits a picker where the face is the whole point. Twenty units
    /// leaves 4% clipped — the ones with antennae and wings, which the game clips too — and still
    /// puts the median portrait at about two thirds of the frame's height.
    /// </para>
    /// </remarks>
    public double VisibleHeight { get; init; } = 20;

    /// <summary>
    /// How far above the frame's bottom edge the model's feet sit, as a fraction of the height.
    /// </summary>
    /// <remarks>
    /// The game nudges the character down by 20 pixels of its 380, which keeps a portrait from
    /// looking as though it is balanced on the edge.
    /// </remarks>
    public double BottomMargin { get; init; } = -20.0 / 380.0;

    /// <summary>How much light reaches a surface facing away from the lamp.</summary>
    public double Ambient { get; init; } = 0.55;

    /// <summary>
    /// The direction light comes from, over the viewer's left shoulder.
    /// </summary>
    /// <remarks>
    /// Pointing back along negative z, which is where the viewer stands. The parts are flat planes
    /// all facing that way, so this makes little difference to the result — but a lamp behind the
    /// model would leave every one of them at the ambient level, which is not what it is for.
    /// </remarks>
    public Vector3 LightDirection { get; init; } = Vector3.Normalize(new Vector3(-0.4f, 0.5f, -1f));
}

/// <summary>
/// Draws a portrait model into an image.
/// </summary>
/// <remarks>
/// <para>
/// Every portrait in Stellaris is a model rather than a picture, so a designer that wants to show
/// one has to draw it. The models turn out not to be sculpted in the round: each part sits at a
/// single constant depth, making a portrait a stack of flat cut-outs, and each cut-out's shape
/// comes from the transparency of the texture painted on it rather than from its geometry.
/// </para>
/// <para>
/// So this draws them the way they are built. Parts are sorted from the back forwards and painted
/// over one another, transparency and all. Nearer parts have the smaller depth, which is how the
/// outfit comes to sit in front of the body while the part named "outfit behind" sits behind it.
/// </para>
/// <para>
/// The result is a likeness rather than a screenshot: no animation, no skeleton, no shaders. It is
/// enough to tell one species from another in a picker, which is what a portrait list is for.
/// </para>
/// </remarks>
public sealed class PortraitRenderer(RenderSettings? settings = null)
{
    private readonly RenderSettings _settings = settings ?? new RenderSettings();

    /// <summary>
    /// Draws a portrait, taking each part's texture from the supplied lookup.
    /// </summary>
    /// <param name="mesh">The model to draw.</param>
    /// <param name="textures">
    /// The textures to wear, by the file name the model asks for. A part whose texture is missing
    /// is skipped, because without transparency to shape it a cut-out is only a rectangle.
    /// </param>
    /// <param name="modelScale">
    /// How much the entity scales this model by. Species are not modelled to a common size and the
    /// game takes the difference out here, so a small species is drawn small rather than blown up to
    /// fill the frame.
    /// </param>
    public DdsImage Render(
        PortraitMesh mesh,
        IReadOnlyDictionary<string, DdsImage> textures,
        float modelScale = 1f)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(textures);

        var width = _settings.Width * _settings.Supersample;
        var height = _settings.Height * _settings.Supersample;

        var pixels = new byte[width * height * 4];
        var (scale, offset) = Frame(mesh, width, height, modelScale);

        // Furthest first, so nearer parts paint over what is behind them. Depth decreases towards
        // the viewer: hair sits at a lower z than the face it falls across.
        foreach (var part in mesh.Parts.OrderByDescending(AverageDepth))
        {
            if (part.Texture is not { } name || textures.GetValueOrDefault(name) is not { } texture)
            {
                continue;
            }

            DrawPart(part, texture, pixels, width, height, scale, offset);
        }

        return Downsample(pixels, width, height, _settings.Supersample);
    }

    private static float AverageDepth(MeshPart part) =>
        part.Positions.Length == 0 ? 0 : part.Positions.Average(p => p.Z);

    /// <summary>
    /// Works out where the model goes in the image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every portrait is built the same way and shown from the same side, so orientation is not in
    /// question. The camera sits on the negative z side looking towards positive z; model x runs to
    /// the right of the picture and model y runs up it.
    /// </para>
    /// <para>
    /// The scale is the same for every portrait, which is the point: a species is as big as it was
    /// modelled rather than as big as the frame. It stands on the bottom edge, centred on its own
    /// width, and anything taller than the frame is cropped at the top — which is what the game
    /// does, and what it counts on when it gives a species antennae.
    /// </para>
    /// </remarks>
    private (float Scale, Vector2 Offset) Frame(
        PortraitMesh mesh,
        int width,
        int height,
        float modelScale)
    {
        var (min, max) = mesh.Bounds;

        var scale = (float)(height / Math.Max(_settings.VisibleHeight, 0.0001)) * Math.Max(modelScale, 0.01f);
        var centreX = (min.X + max.X) / 2;
        var feet = height * (1 + (float)_settings.BottomMargin);

        return (
            scale,
            new Vector2((width / 2f) - (centreX * scale), feet + (mesh.Footing * scale)));
    }

    private void DrawPart(
        MeshPart part,
        DdsImage texture,
        byte[] pixels,
        int width,
        int height,
        float scale,
        Vector2 offset)
    {
        for (var i = 0; i + 2 < part.Triangles.Length; i += 3)
        {
            var a = part.Triangles[i];
            var b = part.Triangles[i + 1];
            var c = part.Triangles[i + 2];

            if (a >= part.Positions.Length || b >= part.Positions.Length || c >= part.Positions.Length)
            {
                continue;
            }

            Span<Vector3> screen =
            [
                Project(part.Positions[a], scale, offset),
                Project(part.Positions[b], scale, offset),
                Project(part.Positions[c], scale, offset),
            ];

            Span<Vector2> uv =
            [
                UvOf(part, a),
                UvOf(part, b),
                UvOf(part, c),
            ];

            Span<float> light =
            [
                LightOf(part, a),
                LightOf(part, b),
                LightOf(part, c),
            ];

            Rasterise(screen, uv, light, texture, pixels, width, height);
        }
    }

    private static Vector3 Project(Vector3 position, float scale, Vector2 offset) =>
        new(
            (position.X * scale) + offset.X,

            // Screen coordinates run downwards while the model's do not.
            offset.Y - (position.Y * scale),
            position.Z);

    private static Vector2 UvOf(MeshPart part, int index) =>
        index < part.TexCoords.Length ? part.TexCoords[index] : Vector2.Zero;

    private float LightOf(MeshPart part, int index)
    {
        if (index >= part.Normals.Length)
        {
            return 1f;
        }

        var lambert = Math.Max(
            0f,
            Vector3.Dot(Vector3.Normalize(part.Normals[index]), _settings.LightDirection));

        return (float)(_settings.Ambient + ((1 - _settings.Ambient) * lambert));
    }

    /// <summary>Fills one triangle, blending it over whatever is already there.</summary>
    private static void Rasterise(
        ReadOnlySpan<Vector3> screen,
        ReadOnlySpan<Vector2> uv,
        ReadOnlySpan<float> light,
        DdsImage texture,
        byte[] pixels,
        int width,
        int height)
    {
        var minX = Math.Max(0, (int)MathF.Floor(Math.Min(screen[0].X, Math.Min(screen[1].X, screen[2].X))));
        var maxX = Math.Min(width - 1, (int)MathF.Ceiling(Math.Max(screen[0].X, Math.Max(screen[1].X, screen[2].X))));
        var minY = Math.Max(0, (int)MathF.Floor(Math.Min(screen[0].Y, Math.Min(screen[1].Y, screen[2].Y))));
        var maxY = Math.Min(height - 1, (int)MathF.Ceiling(Math.Max(screen[0].Y, Math.Max(screen[1].Y, screen[2].Y))));

        var area = Edge(screen[0], screen[1], screen[2]);
        if (Math.Abs(area) < 1e-6f)
        {
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var point = new Vector3(x + 0.5f, y + 0.5f, 0);

                var w0 = Edge(screen[1], screen[2], point) / area;
                var w1 = Edge(screen[2], screen[0], point) / area;
                var w2 = Edge(screen[0], screen[1], point) / area;

                if (w0 < 0 || w1 < 0 || w2 < 0)
                {
                    continue;
                }

                var (r, g, b, a) = Sample(texture, (w0 * uv[0]) + (w1 * uv[1]) + (w2 * uv[2]));

                // A part's shape comes from its texture's transparency, not its geometry, so a
                // transparent sample means there is nothing here to draw.
                if (a == 0)
                {
                    continue;
                }

                var shade = (w0 * light[0]) + (w1 * light[1]) + (w2 * light[2]);
                var offset = ((y * width) + x) * 4;
                var alpha = a / 255f;

                pixels[offset] = Blend(pixels[offset], b * shade, alpha);
                pixels[offset + 1] = Blend(pixels[offset + 1], g * shade, alpha);
                pixels[offset + 2] = Blend(pixels[offset + 2], r * shade, alpha);
                pixels[offset + 3] = (byte)Math.Min(255, pixels[offset + 3] + a);
            }
        }
    }

    private static float Edge(Vector3 a, Vector3 b, Vector3 c) =>
        ((c.X - a.X) * (b.Y - a.Y)) - ((c.Y - a.Y) * (b.X - a.X));

    private static (byte R, byte G, byte B, byte A) Sample(DdsImage texture, Vector2 uv)
    {
        // Both the coordinates and the image rows run downwards, so nothing is flipped: these
        // models follow the same convention their textures are stored in.
        var x = (int)(Wrap(uv.X) * (texture.Width - 1));
        var y = (int)(Wrap(uv.Y) * (texture.Height - 1));

        var (b, g, r, a) = texture[Math.Clamp(x, 0, texture.Width - 1), Math.Clamp(y, 0, texture.Height - 1)];
        return (r, g, b, a);
    }

    private static float Wrap(float value)
    {
        var wrapped = value - MathF.Floor(value);
        return float.IsFinite(wrapped) ? wrapped : 0f;
    }

    private static byte Blend(byte existing, float incoming, float alpha) =>
        (byte)Math.Clamp((existing * (1 - alpha)) + (incoming * alpha), 0, 255);

    /// <summary>Averages each block of pixels down to one, which softens the edges.</summary>
    private static DdsImage Downsample(byte[] pixels, int width, int height, int factor)
    {
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
