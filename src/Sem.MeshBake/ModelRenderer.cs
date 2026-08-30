using System.Numerics;
using Sem.Assets;

namespace Sem.MeshBake;

/// <summary>How a solid model is drawn.</summary>
public sealed record ModelSettings
{
    /// <summary>Width of the finished image, before supersampling.</summary>
    public int Width { get; init; } = 320;

    /// <summary>Height of the finished image, before supersampling.</summary>
    public int Height { get; init; } = 200;

    /// <summary>
    /// How much larger to draw before scaling down.
    /// </summary>
    /// <remarks>
    /// A hull is all long straight edges at shallow angles, which is the worst case for a rasteriser
    /// with no antialiasing of its own, so this matters more here than it does for a portrait.
    /// </remarks>
    public int Supersample { get; init; } = 3;

    /// <summary>
    /// Which way the camera looks, in the model's own space.
    /// </summary>
    /// <remarks>
    /// Down and along, from above the model's left shoulder — the three-quarter view a ship is
    /// recognised from, and the one the game's own fleet view settles into. Ships are modelled with
    /// their length along z and their beam along x, so a direction with weight in all three shows
    /// the length, the beam and the top at once. A view straight down any one axis would show a
    /// silhouette, and half the sets have much the same silhouette.
    /// </remarks>
    public Vector3 Gaze { get; init; } = Vector3.Normalize(new Vector3(-0.62f, -0.5f, 0.75f));

    /// <summary>Which way is up, so the model does not roll.</summary>
    public Vector3 Up { get; init; } = Vector3.UnitY;

    /// <summary>How much empty space to leave around the model, as a multiple of its size.</summary>
    public double Margin { get; init; } = 1.06;

    /// <summary>How much light reaches a surface facing away from the lamp.</summary>
    public double Ambient { get; init; } = 0.34;

    /// <summary>
    /// The direction light comes from.
    /// </summary>
    /// <remarks>
    /// Over the viewer's left shoulder and from above, which is where the game puts its own key
    /// light. Unlike a portrait — whose parts are flat cards all facing the viewer — a hull has real
    /// normals, so this is what gives it its shape.
    /// </remarks>
    public Vector3 LightDirection { get; init; } = Vector3.Normalize(new Vector3(-0.45f, 0.72f, -0.53f));
}

/// <summary>
/// Draws a solid model into an image.
/// </summary>
/// <remarks>
/// <para>
/// Beside <see cref="PortraitRenderer"/> rather than inside it, because a ship is not a portrait.
/// Every portrait part is a flat card at a single depth, which is what lets that renderer sort the
/// parts once and paint them back to front with no depth buffer at all. A corvette hull is eight
/// thousand vertices wrapped around itself: sorted painting draws the far side over the near one,
/// and no ordering of whole parts can fix it, because the parts overlap themselves.
/// </para>
/// <para>
/// So this keeps a depth per pixel and tests each one. The projection is orthographic and fitted to
/// what the camera actually sees — every vertex is turned into camera space, the extent measured,
/// and the scale chosen from that — so a model comes out filling its frame whatever size it was
/// modelled at, which is the opposite of what a portrait wants and the right thing for a picker.
/// </para>
/// </remarks>
public sealed class ModelRenderer(ModelSettings? settings = null)
{
    private readonly ModelSettings _settings = settings ?? new ModelSettings();

    /// <summary>
    /// A part worth drawing: one with a texture, a shape and coordinates to look it up by.
    /// </summary>
    /// <remarks>
    /// Ship meshes carry collision hulls alongside the visible geometry — a bare box named
    /// <c>c_body</c> or <c>pCube3</c>, with no texture, no normals and no coordinates. The game
    /// never draws them and neither does this. Having no texture is the test, since that is the one
    /// thing every collision hull lacks and every visible part has.
    /// </remarks>
    public static bool IsVisible(MeshPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.Texture is { Length: > 0 }
            && part.Positions.Length > 0
            && part.TexCoords.Length > 0;
    }

    /// <summary>Draws the model, or returns null when nothing about it can be drawn.</summary>
    public DdsImage? Render(PortraitMesh mesh, IReadOnlyDictionary<string, DdsImage> textures)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(textures);

        var parts = mesh.Parts.Where(IsVisible).ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        var width = _settings.Width * _settings.Supersample;
        var height = _settings.Height * _settings.Supersample;

        var camera = Camera.LookingAlong(_settings.Gaze, _settings.Up);

        if (Fit(parts, camera, width, height) is not { } frame)
        {
            return null;
        }

        var pixels = new byte[width * height * 4];

        // Everything starts infinitely far away, so the first surface to land on a pixel wins it and
        // anything behind that surface loses.
        var depth = new float[width * height];
        Array.Fill(depth, float.PositiveInfinity);

        foreach (var part in parts)
        {
            if (textures.GetValueOrDefault(part.Texture!) is not { } texture)
            {
                continue;
            }

            DrawPart(part, texture, camera, frame, pixels, depth, width, height);
        }

        return Raster.Downsample(pixels, width, height, _settings.Supersample);
    }

    /// <summary>Where the camera stands: three directions that turn the model to face it.</summary>
    private readonly record struct Camera(Vector3 Right, Vector3 Up, Vector3 Forward)
    {
        public static Camera LookingAlong(Vector3 gaze, Vector3 up)
        {
            var forward = Vector3.Normalize(gaze);

            // A camera looking straight up has no sideways to speak of, so it borrows one.
            var reference = Math.Abs(Vector3.Dot(forward, Vector3.Normalize(up))) > 0.999f
                ? Vector3.UnitZ
                : up;

            var right = Vector3.Normalize(Vector3.Cross(reference, forward));

            return new Camera(right, Vector3.Cross(forward, right), forward);
        }

        /// <summary>A point in the model's space, seen from here.</summary>
        public Vector3 Seen(Vector3 point) =>
            new(Vector3.Dot(point, Right), Vector3.Dot(point, Up), Vector3.Dot(point, Forward));
    }

    /// <summary>How to turn a point the camera sees into a pixel.</summary>
    private readonly record struct Frame(float Scale, Vector2 Centre, Vector2 Middle)
    {
        public Vector3 Project(Vector3 seen) =>
            new(
                ((seen.X - Centre.X) * Scale) + Middle.X,

                // Screen rows run downwards while the model's own up does not.
                Middle.Y - ((seen.Y - Centre.Y) * Scale),
                seen.Z);
    }

    /// <summary>
    /// Chooses the scale and the centre so the whole model lands inside the picture.
    /// </summary>
    /// <remarks>
    /// Measured from what the camera sees rather than from the model's own bounding box, because a
    /// box that fits the model does not fit its shadow on the screen: a long hull seen corner-on
    /// covers far less than its diagonal. Fitting the projection is exact and costs one pass over
    /// the vertices.
    /// </remarks>
    private Frame? Fit(IEnumerable<MeshPart> parts, Camera camera, int width, int height)
    {
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);

        foreach (var part in parts)
        {
            foreach (var position in part.Positions)
            {
                var seen = camera.Seen(position);
                min = Vector2.Min(min, new Vector2(seen.X, seen.Y));
                max = Vector2.Max(max, new Vector2(seen.X, seen.Y));
            }
        }

        var extent = max - min;

        if (!float.IsFinite(extent.X) || extent.X <= 0 || extent.Y <= 0)
        {
            return null;
        }

        var margin = (float)Math.Max(_settings.Margin, 1);

        return new Frame(
            Math.Min(width / (extent.X * margin), height / (extent.Y * margin)),
            (min + max) / 2,
            new Vector2(width / 2f, height / 2f));
    }

    private void DrawPart(
        MeshPart part,
        DdsImage texture,
        Camera camera,
        Frame frame,
        byte[] pixels,
        float[] depth,
        int width,
        int height)
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
                frame.Project(camera.Seen(part.Positions[a])),
                frame.Project(camera.Seen(part.Positions[b])),
                frame.Project(camera.Seen(part.Positions[c])),
            ];

            Span<Vector2> uv = [UvOf(part, a), UvOf(part, b), UvOf(part, c)];
            Span<float> light = [LightOf(part, a), LightOf(part, b), LightOf(part, c)];

            Rasterise(screen, uv, light, texture, pixels, depth, width, height);
        }
    }

    private static Vector2 UvOf(MeshPart part, int index) =>
        index < part.TexCoords.Length ? part.TexCoords[index] : Vector2.Zero;

    /// <summary>
    /// How lit a vertex is.
    /// </summary>
    /// <remarks>
    /// A part with no normals is drawn flat rather than black — some small fittings are modelled
    /// without them, and an unlit fitting reads as a hole in the hull.
    /// </remarks>
    private float LightOf(MeshPart part, int index)
    {
        if (index >= part.Normals.Length)
        {
            return 1f;
        }

        var normal = part.Normals[index];

        if (normal.LengthSquared() < 1e-8f)
        {
            return 1f;
        }

        var lambert = Math.Max(0f, Vector3.Dot(Vector3.Normalize(normal), _settings.LightDirection));

        return (float)(_settings.Ambient + ((1 - _settings.Ambient) * lambert));
    }

    /// <summary>Fills one triangle, keeping whichever surface is nearest at each pixel.</summary>
    private static void Rasterise(
        ReadOnlySpan<Vector3> screen,
        ReadOnlySpan<Vector2> uv,
        ReadOnlySpan<float> light,
        DdsImage texture,
        byte[] pixels,
        float[] depth,
        int width,
        int height)
    {
        var minX = Math.Max(0, (int)MathF.Floor(Math.Min(screen[0].X, Math.Min(screen[1].X, screen[2].X))));
        var maxX = Math.Min(width - 1, (int)MathF.Ceiling(Math.Max(screen[0].X, Math.Max(screen[1].X, screen[2].X))));
        var minY = Math.Max(0, (int)MathF.Floor(Math.Min(screen[0].Y, Math.Min(screen[1].Y, screen[2].Y))));
        var maxY = Math.Min(height - 1, (int)MathF.Ceiling(Math.Max(screen[0].Y, Math.Max(screen[1].Y, screen[2].Y))));

        var area = Raster.Edge(screen[0], screen[1], screen[2]);

        if (Math.Abs(area) < 1e-6f)
        {
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var point = new Vector3(x + 0.5f, y + 0.5f, 0);

                var w0 = Raster.Edge(screen[1], screen[2], point) / area;
                var w1 = Raster.Edge(screen[2], screen[0], point) / area;
                var w2 = Raster.Edge(screen[0], screen[1], point) / area;

                // Either winding is accepted, since a hull's far side is wound the other way and
                // both sides are wanted — the depth test is what decides which one is seen.
                if (w0 < 0 || w1 < 0 || w2 < 0)
                {
                    continue;
                }

                var z = (w0 * screen[0].Z) + (w1 * screen[1].Z) + (w2 * screen[2].Z);
                var slot = (y * width) + x;

                if (z >= depth[slot])
                {
                    continue;
                }

                var (r, g, b, a) = Raster.Sample(texture, (w0 * uv[0]) + (w1 * uv[1]) + (w2 * uv[2]));

                if (a == 0)
                {
                    continue;
                }

                var shade = (w0 * light[0]) + (w1 * light[1]) + (w2 * light[2]);
                var offset = slot * 4;
                var alpha = a / 255f;

                pixels[offset] = Raster.Blend(pixels[offset], b * shade, alpha);
                pixels[offset + 1] = Raster.Blend(pixels[offset + 1], g * shade, alpha);
                pixels[offset + 2] = Raster.Blend(pixels[offset + 2], r * shade, alpha);
                pixels[offset + 3] = (byte)Math.Min(255, pixels[offset + 3] + a);

                // Only a surface you cannot see through closes the pixel off. A canopy or an engine
                // glow lets what is behind it keep drawing, which is what makes it read as glass.
                if (a >= 250)
                {
                    depth[slot] = z;
                }
            }
        }
    }
}
