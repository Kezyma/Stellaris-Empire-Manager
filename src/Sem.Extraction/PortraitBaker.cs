using Sem.Assets;
using Sem.Clausewitz;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction;

/// <summary>What came of drawing the portraits.</summary>
/// <param name="Rendered">How many likenesses were produced.</param>
/// <param name="Bytes">Their total size.</param>
/// <param name="Failures">Portraits that could not be drawn, with the reason.</param>
public sealed record PortraitBakeReport(int Rendered, long Bytes, IReadOnlyList<string> Failures);

/// <summary>How far a posed, scaled portrait reaches from its own origin.</summary>
/// <param name="Key">The portrait.</param>
/// <param name="Rise">How far the highest point stands above the origin, in model units.</param>
/// <param name="Drop">How far the lowest point hangs below it, negative where nothing does.</param>
/// <param name="Clipped">
/// Whether it reached the edge of even the measuring frame, in which case what it reports is a floor
/// rather than the truth.
/// </param>
public sealed record PortraitExtent(string Key, float Rise, float Drop, bool Clipped);

/// <summary>What a portrait is wearing, as its own definition describes it.</summary>
/// <param name="Character">The body texture.</param>
/// <param name="Clothes">The clothing texture.</param>
/// <param name="Attachment">Hair, horns, a hat — whatever is fixed to the head.</param>
/// <remarks>
/// Held apart rather than resolved into the mesh because the three are chosen independently. Drawing
/// the same model in different clothes, which the ruler's appearance will want, is then a matter of
/// a different set rather than a different renderer.
/// </remarks>
public sealed record PortraitTextures(string? Character, string? Clothes, string? Attachment)
{
    /// <summary>A portrait whose definition says nothing, leaving the mesh to supply everything.</summary>
    public static PortraitTextures None { get; } = new(null, null, null);

    /// <summary>The texture for one kind of part, or null when the definition names none.</summary>
    public string? For(PartKind kind) => kind switch
    {
        PartKind.Clothes => Clothes,
        PartKind.Attachment => Attachment,
        _ => Character,
    };
}

/// <summary>
/// Draws every portrait the game defines, once, so the designer has faces to show.
/// </summary>
/// <remarks>
/// Getting from a portrait's key to something that can be drawn takes three hops through the game's
/// files: the portrait names an entity, the entity names a mesh, and a separate file says where that
/// mesh lives. What the model is wearing takes a fourth, through the portrait's own definition. All
/// of it is ordinary script, so all of it is read the same way as everything else.
/// </remarks>
public sealed class PortraitBaker(LayeredContent content, SafeFile file)
{
    private const string ModelRoot = "gfx/models/portraits";

    /// <summary>The value a portrait uses to say it wears nothing of a given kind.</summary>
    private const string NoTexture = "no_texture";

    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly PortraitRenderer _renderer = new();

    /// <summary>
    /// Decoded textures, kept only while nearby portraits are still using them.
    /// </summary>
    /// <remarks>
    /// A skin texture is a megapixel or more once decoded, and the game has hundreds. Holding them
    /// all would cost well over a gigabyte for no benefit: portraits are drawn in order and the
    /// ones that share a texture sit together, so a small cache catches almost every reuse.
    /// </remarks>
    private readonly Dictionary<string, DdsImage?> _textures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many decoded textures to keep before starting again.</summary>
    private const int TextureCacheLimit = 24;

    /// <summary>
    /// Draws each portrait in the database and records where its likeness went.
    /// </summary>
    /// <returns>The portraits, with thumbnails filled in where one could be drawn.</returns>
    public (IReadOnlyList<PortraitDefinition> Portraits, PortraitBakeReport Report) Bake(
        IReadOnlyList<PortraitDefinition> portraits,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(portraits);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var index = BuildIndex();
        var wardrobes = ReadWardrobes();

        var results = new List<PortraitDefinition>(portraits.Count);
        var failures = new List<string>();
        var rendered = 0;
        long bytes = 0;

        for (var i = 0; i < portraits.Count; i++)
        {
            var portrait = portraits[i];

            if (i % 50 == 0)
            {
                progress?.Report($"Drawing portraits ({i} of {portraits.Count})");
            }

            // A group has no model of its own and wears the likeness of the portrait it defaults to.
            if (portrait.IsGroup)
            {
                results.Add(portrait);
                continue;
            }

            try
            {
                if (index.Model(portrait.Key) is not { } model)
                {
                    failures.Add($"{portrait.Key}: no model");
                    results.Add(portrait);
                    continue;
                }

                var png = Draw(
                    model.Path,
                    wardrobes.GetValueOrDefault(portrait.Key) ?? PortraitTextures.None,
                    (float)model.Scale,
                    PoseFor(index, model.Mesh));
                var destination = $"portraits/{portrait.Key}.png";

                _file.WriteAllBytes(Path.Combine(outputDirectory, destination), png);

                rendered++;
                bytes += png.Length;
                results.Add(portrait with { Thumbnail = destination });
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // One portrait that will not draw must not cost the player all the others.
                failures.Add($"{portrait.Key}: {ex.Message}");
                results.Add(portrait);
            }
        }

        return (results, new PortraitBakeReport(rendered, bytes, failures));
    }

    /// <summary>
    /// How far each portrait reaches above and below its own origin, once posed and scaled.
    /// </summary>
    /// <remarks>
    /// This is what the frame has to cover. It is measured rather than assumed because the game's
    /// own answer — a 380-pixel box at scale 24, with the figure standing 20 pixels up it — is the
    /// size of a window the game then crops, not the size of the figures inside it. Portraits taller
    /// than it lose their heads and shorter ones float above the bottom edge.
    /// </remarks>
    public IReadOnlyList<PortraitExtent> Measure(IEnumerable<string> keys, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var index = BuildIndex();
        var wardrobes = ReadWardrobes();
        var extents = new List<PortraitExtent>();

        // Deliberately far larger than anything could need, so nothing is cut and the measurement is
        // of the portrait rather than of the frame. Drawn rather than taken from the model's bounds
        // for two reasons: a part whose texture is missing is not drawn at all, and a sliver of
        // geometry a pixel wide all but disappears once the drawing is scaled down. Both would report
        // a figure reaching places nothing can be seen. Supersampled as the real thing is, so what is
        // measured is what a viewer would see.
        var settings = new RenderSettings
        {
            Width = (int)(Window * PerUnit),
            Height = (int)(Sweep * PerUnit),
            VisibleHeight = Sweep,
            BottomMargin = -Below / Sweep,
        };

        var renderer = new PortraitRenderer(settings);
        var origin = settings.Height * (1 - (Below / Sweep));
        var measured = 0;

        foreach (var key in keys)
        {
            if (index.Model(key) is not { } model)
            {
                continue;
            }

            if (++measured % 50 == 0)
            {
                progress?.Report($"Measuring portraits ({measured})");
            }

            try
            {
                var image = Draw(
                    renderer,
                    model.Path,
                    wardrobes.GetValueOrDefault(key) ?? PortraitTextures.None,
                    (float)model.Scale,
                    PoseFor(index, model.Mesh));

                var (top, bottom) = Ink(image);

                if (top > bottom)
                {
                    continue;
                }

                extents.Add(new PortraitExtent(
                    key,
                    (float)((origin - top) / PerUnit),
                    (float)((bottom - origin) / PerUnit),
                    top <= 0 || bottom >= settings.Height - 1));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // A model that will not read cannot say how tall it is; the rest still can.
            }
        }

        return extents;
    }

    /// <summary>How many model units the measuring frame covers, and how much of that is below the origin.</summary>
    /// <remarks>
    /// Twice as tall as the game's own frame and six times as deep, so that nothing reaches the edge
    /// and every measurement is of the portrait rather than of the frame.
    /// </remarks>
    private const double Sweep = 30;

    private const double Below = 5;

    /// <summary>How wide the measuring frame is, in model units.</summary>
    /// <remarks>
    /// Not as generous as the height, and deliberately so. A portrait tile is narrow, and a plume or
    /// a wing held out to one side falls outside it. Measured in a wide frame that plume is counted,
    /// and every species would be shrunk to make room for something nobody would ever see. This is
    /// about what a finished tile shows: its own height, in units, times its width over its height.
    /// </remarks>
    private const double Window = 20 * 165.0 / 220.0;

    /// <summary>How many pixels the measuring frame gives each model unit.</summary>
    private const int PerUnit = 20;

    /// <summary>
    /// The first and last rows of an image with anything visible drawn on them.
    /// </summary>
    /// <remarks>
    /// The threshold is what separates a portrait from the haze a stray triangle leaves behind once
    /// the drawing has been scaled down. Framing around haze is framing around nothing.
    /// </remarks>
    private static (int Top, int Bottom) Ink(DdsImage image)
    {
        var top = image.Height;
        var bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A > 32)
                {
                    top = Math.Min(top, y);
                    bottom = y;
                    break;
                }
            }
        }

        return (top, bottom);
    }

    /// <summary>The rest pose belonging to a mesh, or none where it has no animation.</summary>
    private PortraitPose PoseFor(ModelIndex index, string mesh) =>
        index.MeshAnimations.GetValueOrDefault(mesh) is { } animation &&
        index.AnimationPaths.GetValueOrDefault(animation) is { } path
            ? LoadPose(path)
            : PortraitPose.None;

    /// <summary>Everything needed to get from a portrait's key to a model that can be drawn.</summary>
    private sealed record ModelIndex(
        Func<string, bool> Exists,
        Dictionary<string, string> PortraitEntities,
        Dictionary<string, string> Entities,
        Dictionary<string, string> Meshes,
        Dictionary<string, string> Attachments,
        Dictionary<string, double> EntityScales,
        Dictionary<string, double> MeshScales,
        Dictionary<string, string> MeshAnimations,
        Dictionary<string, string> AnimationPaths)
    {
        /// <summary>The model a portrait wears, and how much it is scaled by.</summary>
        public (string Mesh, string Path, double Scale)? Model(string portrait) =>
            PortraitEntities.GetValueOrDefault(portrait) is { } entity ? Of(entity) : null;

        /// <summary>
        /// An entity need not carry the model itself. One molluscoid keeps an empty locator and hangs
        /// its portrait off it as an attachment, so where an entity names no model that can be drawn
        /// the search carries on into whatever it attaches. Scales multiply on the way down, since
        /// every entity in the chain is entitled to one.
        /// </summary>
        private (string Mesh, string Path, double Scale)? Of(string entity, double inherited = 1, int depth = 0)
        {
            var scale = inherited * EntityScales.GetValueOrDefault(entity, 1);

            if (Entities.GetValueOrDefault(entity) is { } mesh &&
                Meshes.GetValueOrDefault(mesh) is { } path &&
                Exists(path))
            {
                return (mesh, path, scale * MeshScales.GetValueOrDefault(mesh, 1));
            }

            // Bounded in case a pair of entities ever attach one another.
            return depth < 4 && Attachments.GetValueOrDefault(entity) is { } attached
                ? Of(attached, scale, depth + 1)
                : null;
        }
    }

    private ModelIndex BuildIndex() => new(
        _content.Contains,
        ReadPortraitEntities(),
        ReadEntities(),
        ReadMeshPaths(),
        ReadAttachments(),
        ReadScales("*.asset", "entity"),
        ReadScales("*.gfx", "pdxmesh"),
        ReadMeshAnimations(),
        ReadAnimationPaths());

    /// <summary>
    /// The rest pose from an animation, kept because a model's animations are read once but its
    /// portraits many times — the human model dresses a dozen of them.
    /// </summary>
    private PortraitPose LoadPose(string path)
    {
        if (_poses.TryGetValue(path, out var cached))
        {
            return cached;
        }

        PortraitPose pose;

        try
        {
            pose = _content.Contains(path) ? PortraitPose.Read(_content.Read(path)) : PortraitPose.None;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            // A portrait drawn in the pose it was modelled in is worth more than none at all.
            pose = PortraitPose.None;
        }

        _poses[path] = pose;
        return pose;
    }

    private readonly Dictionary<string, PortraitPose> _poses = new(StringComparer.OrdinalIgnoreCase);

    private byte[] Draw(string meshPath, PortraitTextures wearing, float scale, PortraitPose pose) =>
        PngWriter.Encode(Draw(_renderer, meshPath, wearing, scale, pose));

    private DdsImage Draw(
        PortraitRenderer renderer,
        string meshPath,
        PortraitTextures wearing,
        float scale,
        PortraitPose pose)
    {
        var mesh = pose.ApplyTo(PortraitMesh.Load(_content.Read(meshPath)));

        // Each part is told what it is actually wearing before anything is drawn, so the renderer
        // has one job and a portrait in different clothes is the same call with a different set.
        var dressed = new PortraitMesh(
            [.. mesh.Parts.Select(p => p with { Texture = TextureFor(p, wearing) })]);

        var textures = new Dictionary<string, DdsImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in dressed.Parts.Select(p => p.Texture).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (LoadTexture(path) is { } texture)
            {
                textures[path] = texture;
            }
        }

        return renderer.Render(dressed, textures, scale);
    }

    /// <summary>
    /// Finds the texture a part should wear.
    /// </summary>
    /// <remarks>
    /// What the portrait says it is wearing comes first, because the mesh's own texture is a default
    /// the definition is entitled to override — several humanoids are modelled in a general's coat
    /// and dressed by their portrait in a ruler's. Where the portrait says nothing, the mesh's
    /// texture stands; and since that is a bare file name which may live in any of the portrait
    /// folders, it is looked up by name rather than assumed to sit beside its mesh.
    /// </remarks>
    private string? TextureFor(MeshPart part, PortraitTextures wearing)
    {
        if (wearing.For(part.Kind) is { Length: > 0 } chosen)
        {
            return chosen;
        }

        return part.Texture is { Length: > 0 } own ? FindTexture(own) : null;
    }

    /// <summary>
    /// Finds a texture named without a path.
    /// </summary>
    /// <remarks>
    /// Meshes name their textures by file name alone, and those files are not always in the folder
    /// the mesh is in: a humanoid portrait wears a coat kept with the mammalian art. Looking only
    /// beside the mesh is why clothing went missing, and why portraits whose torso and sleeves share
    /// one such texture came out as a head and a pair of floating hands.
    /// </remarks>
    private string? FindTexture(string name) =>
        name.Contains('/') ? name : TextureIndex.GetValueOrDefault(Path.GetFileName(name));

    /// <summary>Every texture in the portrait folders, by file name.</summary>
    private Dictionary<string, string> TextureIndex =>
        _textureIndex ??= BuildTextureIndex();

    private Dictionary<string, string>? _textureIndex;

    private Dictionary<string, string> BuildTextureIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _content.EnumerateFiles(ModelRoot, "*.dds", recursive: true))
        {
            // First wins, which matches the load order the caller enumerated in.
            index.TryAdd(Path.GetFileName(path), path);
        }

        return index;
    }

    private DdsImage? LoadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var cached))
        {
            return cached;
        }

        DdsImage? image = null;

        if (_content.Contains(path))
        {
            try
            {
                image = DdsReader.Read(_content.Read(path));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                image = null;
            }
        }

        // Emptied rather than evicted one at a time: the next species brings its own textures, so
        // what is held is about to become useless anyway.
        if (_textures.Count >= TextureCacheLimit)
        {
            _textures.Clear();
        }

        _textures[path] = image;
        return image;
    }

    /// <summary>
    /// What each portrait says it is wearing.
    /// </summary>
    /// <remarks>
    /// Most of the artwork is not named by the mesh at all. A portrait names its own body texture,
    /// and points at selectors for its clothes and its hair; the psionic portraits name nothing in
    /// their meshes whatsoever, which is why they came out blank.
    /// </remarks>
    private Dictionary<string, PortraitTextures> ReadWardrobes()
    {
        var selectors = ReadSelectors();
        var wardrobes = new Dictionary<string, PortraitTextures>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles("gfx/portraits/portraits", "*.txt"))
        {
            var document = TryParse(path);

            foreach (var node in document?.Nodes ?? [])
            {
                if (node.Key != "portraits" || node.Block is null)
                {
                    continue;
                }

                foreach (var portrait in node.Block.Nodes)
                {
                    if (portrait.Key is not { Length: > 0 } key || portrait.Block is not { } body)
                    {
                        continue;
                    }

                    wardrobes.TryAdd(key, new PortraitTextures(
                        ReadCharacterTexture(body),
                        Selected(selectors, body.GetString("clothes_selector")),
                        Selected(selectors, body.GetString("attachment_selector"))));
                }
            }
        }

        return wardrobes;
    }

    /// <summary>
    /// The body texture a portrait names.
    /// </summary>
    /// <remarks>
    /// Written either as a plain list of paths or, where a portrait changes with an empire's
    /// evolution, as blocks tying each texture to a stage. The first is the one an empire starts
    /// with either way.
    /// </remarks>
    private static string? ReadCharacterTexture(CwBlock body)
    {
        if (body.GetBlock("character_textures") is not { } textures)
        {
            return null;
        }

        foreach (var node in textures.Nodes)
        {
            if (!node.IsAssignment && node.ScalarValue is { Length: > 0 } path)
            {
                return path;
            }

            if (node.Block?.GetString("texture") is { Length: > 0 } tied)
            {
                return tied;
            }
        }

        return null;
    }

    /// <summary>The texture a named selector chooses, or null when there is none.</summary>
    private static string? Selected(IReadOnlyDictionary<string, string> selectors, string? name) =>
        name is { Length: > 0 } && !string.Equals(name, NoTexture, StringComparison.Ordinal)
            ? selectors.GetValueOrDefault(name)
            : null;

    /// <summary>
    /// What each asset selector chooses for an empire being designed.
    /// </summary>
    /// <remarks>
    /// A selector holds a texture for every situation the game might ask about — a pop of some
    /// stratum, a leader of some class, a ruler under some government. One of those situations is
    /// ours, and the game names it: <c>game_setup</c>, commented in its own files as running with a
    /// species and a government but no country. Its default is what the empire designer shows, and
    /// the plain default stands in where a selector does not mention us.
    /// </remarks>
    private Dictionary<string, string> ReadSelectors()
    {
        var selectors = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles("gfx/portraits/asset_selectors", "*.txt"))
        {
            var document = TryParse(path);

            foreach (var node in document?.Nodes ?? [])
            {
                if (node.Key is not { Length: > 0 } key || node.Block is not { } body)
                {
                    continue;
                }

                var chosen = body.GetBlock("game_setup")?.GetString("default")
                             ?? body.GetString("default");

                if (chosen is { Length: > 0 })
                {
                    selectors.TryAdd(key, chosen);
                }
            }
        }

        return selectors;
    }

    /// <summary>Portrait key to the entity it wears, from the portrait definitions.</summary>
    private Dictionary<string, string> ReadPortraitEntities()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles("gfx/portraits/portraits", "*.txt"))
        {
            var document = TryParse(path);

            foreach (var node in document?.Nodes ?? [])
            {
                if (node.Key != "portraits" || node.Block is null)
                {
                    continue;
                }

                foreach (var portrait in node.Block.Nodes)
                {
                    if (portrait.Key is { Length: > 0 } key &&
                        portrait.Block?.GetString("entity") is { Length: > 0 } entity)
                    {
                        map.TryAdd(key, entity);
                    }
                }
            }
        }

        return map;
    }

    /// <summary>Entity name to the mesh it uses, from the entity definitions.</summary>
    private Dictionary<string, string> ReadEntities() =>
        ReadNameToValue("*.asset", blockKey: "entity", valueKey: "pdxmesh");

    /// <summary>
    /// Entity name to the entity it hangs off itself, where it has one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written as a locator's name against the entity fixed to it:
    /// <c>attach = { "root" = "portrait_molluscoid_05_portrait_entity" }</c>. Only one portrait in
    /// the game is built this way, and it is built that way entirely — its own <c>pdxmesh</c> is the
    /// empty <c>locator_mesh</c>, so without following the attachment there is nothing to draw.
    /// </para>
    /// <para>
    /// The locator it attaches to has a position, which is not applied. It moves the figure sideways
    /// only, and the thumbnail centres the figure on the frame regardless.
    /// </para>
    /// </remarks>
    private Dictionary<string, string> ReadAttachments()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, "*.asset", recursive: true))
        {
            foreach (var node in TryParse(path)?.Nodes ?? [])
            {
                Collect(node);
            }
        }

        return map;

        void Collect(CwNode node)
        {
            if (node.Block is not { } body)
            {
                return;
            }

            if (string.Equals(node.Key, "entity", StringComparison.Ordinal) &&
                body.GetString("name") is { Length: > 0 } name)
            {
                var attached = body.GetBlock("attach")?.Nodes
                    .Select(n => n.ScalarValue)
                    .FirstOrDefault(v => v is { Length: > 0 });

                if (attached is { Length: > 0 })
                {
                    map.TryAdd(name, attached);
                }

                return;
            }

            foreach (var child in body.Nodes)
            {
                Collect(child);
            }
        }
    }

    /// <summary>Animation name to the file holding it.</summary>
    /// <remarks>
    /// Written beside the asset that names it rather than as a path from the game's root, so the
    /// folder it was found in has to travel with it.
    /// </remarks>
    private Dictionary<string, string> ReadAnimationPaths()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, "*.asset", recursive: true))
        {
            if (TryParse(path) is not { } document)
            {
                continue;
            }

            var directory = path[..path.LastIndexOf('/')];

            foreach (var node in document.Nodes)
            {
                if (node.Key == "animation" && node.Block is { } body &&
                    body.GetString("name") is { Length: > 0 } name &&
                    body.GetString("file") is { Length: > 0 } file)
                {
                    map.TryAdd(name, $"{directory}/{file.Replace('\\', '/')}");
                }
            }
        }

        return map;
    }

    /// <summary>Mesh name to an animation it uses, any of which carries the rest pose.</summary>
    private Dictionary<string, string> ReadMeshAnimations()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, "*.gfx", recursive: true))
        {
            if (TryParse(path) is not { } document)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                Collect(node);
            }
        }

        return map;

        void Collect(CwNode node)
        {
            if (node.Block is not { } body)
            {
                return;
            }

            if (string.Equals(node.Key, "pdxmesh", StringComparison.Ordinal) &&
                body.GetString("name") is { Length: > 0 } name)
            {
                // Any of a model's animations will do: they all open from the same rest pose.
                var animation = body.Nodes
                    .FirstOrDefault(n => n.Key == "animation")?.Block?.GetString("type");

                if (animation is { Length: > 0 })
                {
                    map.TryAdd(name, animation);
                }

                return;
            }

            foreach (var child in body.Nodes)
            {
                Collect(child);
            }
        }
    }

    /// <summary>
    /// How much each entity and each mesh is scaled by.
    /// </summary>
    /// <remarks>
    /// Species are not modelled to a common size — a human is seventeen units of model and a
    /// synthetic twenty-one — and the difference is taken out by a scale on the entity, sometimes
    /// again on the mesh. Ignoring it is what made a small species draw as large as a big one.
    /// </remarks>
    private Dictionary<string, double> ReadScales(string pattern, string blockKey)
    {
        var scales = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, pattern, recursive: true))
        {
            if (TryParse(path) is not { } document)
            {
                continue;
            }

            // These files declare their own variables, which is how a dozen human portraits share
            // one scale written in a single place.
            var variables = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var node in document.Nodes)
            {
                if (node.Key is { Length: > 1 } name && name[0] == '@' &&
                    double.TryParse(node.ScalarValue, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    variables[name] = value;
                }
            }

            foreach (var node in document.Nodes)
            {
                Collect(node);
            }

            void Collect(CwNode node)
            {
                if (node.Block is not { } body)
                {
                    return;
                }

                if (string.Equals(node.Key, blockKey, StringComparison.Ordinal) &&
                    body.GetString("name") is { Length: > 0 } name &&
                    body.GetString("scale") is { Length: > 0 } scale)
                {
                    if (variables.TryGetValue(scale, out var referenced))
                    {
                        scales.TryAdd(name, referenced);
                    }
                    else if (double.TryParse(scale, System.Globalization.CultureInfo.InvariantCulture, out var literal))
                    {
                        scales.TryAdd(name, literal);
                    }

                    return;
                }

                foreach (var child in body.Nodes)
                {
                    Collect(child);
                }
            }
        }

        return scales;
    }

    /// <summary>Mesh name to the file that holds it, from the mesh declarations.</summary>
    private Dictionary<string, string> ReadMeshPaths()
    {
        var map = ReadNameToValue("*.gfx", blockKey: "pdxmesh", valueKey: "file");

        foreach (var key in map.Keys)
        {
            map[key] = map[key].Replace('\\', '/');
        }

        return map;
    }

    /// <summary>
    /// Collects <c>name</c> to some other field from every block of a given kind, wherever it sits.
    /// </summary>
    /// <remarks>
    /// Entities are declared at the top of their file while meshes are wrapped in an
    /// <c>objectTypes</c> block, so the search covers the whole tree rather than assuming a depth.
    /// </remarks>
    private Dictionary<string, string> ReadNameToValue(string pattern, string blockKey, string valueKey)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, pattern, recursive: true))
        {
            if (TryParse(path) is not { } document)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                Collect(node);
            }
        }

        return map;

        void Collect(CwNode node)
        {
            if (node.Block is not { } body)
            {
                return;
            }

            if (string.Equals(node.Key, blockKey, StringComparison.Ordinal) &&
                body.GetString("name") is { Length: > 0 } name &&
                body.GetString(valueKey) is { Length: > 0 } value)
            {
                map.TryAdd(name, value);
                return;
            }

            foreach (var child in body.Nodes)
            {
                Collect(child);
            }
        }
    }

    private CwDocument? TryParse(string path)
    {
        try
        {
            return CwDocument.Parse(_content.Read(path), CwParseOptions.Lenient);
        }
        catch (Exception ex) when (ex is CwSyntaxException or IOException)
        {
            return null;
        }
    }
}
