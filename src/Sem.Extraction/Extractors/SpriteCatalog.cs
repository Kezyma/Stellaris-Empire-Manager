using Sem.Clausewitz;

namespace Sem.Extraction.Extractors;

/// <summary>Where a named sprite's picture actually comes from.</summary>
/// <param name="Texture">The texture holding it, as a path into the installation.</param>
/// <param name="Frame">Which frame, counting from one.</param>
/// <param name="FrameCount">How many frames the texture is divided into across its width.</param>
public sealed record SpriteFrame(string Texture, int Frame, int FrameCount)
{
    /// <summary>Whether the whole texture is the picture, rather than a slice of it.</summary>
    public bool IsWholeTexture => FrameCount <= 1;

    /// <summary>
    /// How wide a border the sprite keeps unstretched when it is drawn at any size, or zero.
    /// </summary>
    /// <remarks>
    /// The game's surfaces — the tiles a window or a button is drawn on — are declared as
    /// <c>corneredTileSpriteType</c> with a <c>borderSize</c>, which means the corners are drawn as
    /// they are and only the middle is stretched. That is nine-slicing, and it is the same number
    /// CSS wants for <c>border-image-slice</c>, so it is carried through rather than recovered by
    /// guessing at the texture.
    /// </remarks>
    public (int X, int Y) BorderSize { get; init; }
}

/// <summary>
/// Resolves the <c>GFX_</c> names the game's data refers to into textures, and into which slice of
/// one where a texture holds several pictures side by side.
/// </summary>
/// <remarks>
/// Much of the game's artwork is not one file per picture. Planet types, for instance, are 46
/// pictures in a single strip, and the data refers to them by sprite name only. Anything that wants
/// to show one has to come through here, so this is deliberately general rather than a planet
/// special case: the same mechanism carries the icons that appear inline in the game's text.
/// </remarks>
public sealed class SpriteCatalog
{
    private readonly Dictionary<string, SpriteFrame> _resolved;

    private SpriteCatalog(Dictionary<string, SpriteFrame> resolved) => _resolved = resolved;

    /// <summary>A catalogue that knows no sprites, for callers that have not read one.</summary>
    public static SpriteCatalog Empty { get; } =
        new(new Dictionary<string, SpriteFrame>(StringComparer.OrdinalIgnoreCase));

    /// <summary>How many sprites were resolved to a texture.</summary>
    public int Count => _resolved.Count;

    /// <summary>Every sprite name that resolved, with where its picture is.</summary>
    public IReadOnlyDictionary<string, SpriteFrame> Sprites => _resolved;

    /// <summary>Where a sprite's picture is, or null when the name is not defined.</summary>
    public SpriteFrame? Resolve(string? spriteName) =>
        spriteName is { Length: > 0 } name && _resolved.TryGetValue(name, out var frame) ? frame : null;

    /// <summary>
    /// Reads every sprite the installation declares.
    /// </summary>
    /// <remarks>
    /// Two passes, because a sprite may name a sheet that is declared after it, or in another file
    /// entirely. The first pass records what each declaration says; the second follows the sheet
    /// references, which is also where a reference that goes nowhere is dropped.
    /// </remarks>
    public static SpriteCatalog Read(LayeredContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var declarations = new Dictionary<string, Declaration>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in content.EnumerateFiles("interface", "*.gfx", recursive: true))
        {
            CwDocument document;

            try
            {
                document = CwDocument.Parse(content.Read(path), CwParseOptions.Lenient);
            }
            catch (Exception ex) when (ex is CwSyntaxException or IOException)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                Collect(node, declarations);
            }
        }

        var resolved = new Dictionary<string, SpriteFrame>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, _) in declarations)
        {
            if (Resolve(name, declarations, depth: 0) is { } frame)
            {
                resolved[name] = frame;
            }
        }

        return new SpriteCatalog(resolved);
    }

    /// <summary>
    /// Walks a file's tree collecting anything that declares a sprite name.
    /// </summary>
    /// <remarks>
    /// Sprites sit inside <c>spriteTypes</c> blocks, but the kinds of block that hold them vary and
    /// new ones appear between game versions, so this looks for the shape rather than the wrapper:
    /// any block naming itself and carrying either a texture or a sheet reference.
    /// </remarks>
    private static void Collect(CwNode node, Dictionary<string, Declaration> declarations)
    {
        if (node.Block is not { } body)
        {
            return;
        }

        if (Property(body, "name") is { Length: > 0 } name)
        {
            var texture = Property(body, "texturefile");
            var sheet = Property(body, "sprite_sheet_sprite_type");

            if (texture is { Length: > 0 } || sheet is { Length: > 0 })
            {
                // First declaration wins, matching the load order the caller enumerated in.
                declarations.TryAdd(name, new Declaration(
                    Texture: texture?.Replace('\\', '/'),
                    Sheet: sheet,
                    Frames: Number(Property(body, "noOfFrames")),
                    DefaultFrame: Number(Property(body, "default_frame")),
                    Border: Border(body)));
            }
        }

        foreach (var child in body.Nodes)
        {
            Collect(child, declarations);
        }
    }

    /// <summary>
    /// Reads a property regardless of how its name is capitalised.
    /// </summary>
    /// <remarks>
    /// Not fussiness: <c>texticons.gfx</c> spells it <c>textureFile</c> on some entries and
    /// <c>texturefile</c> on others, within the same file.
    /// </remarks>
    private static string? Property(CwBlock block, string key) =>
        block.Nodes.FirstOrDefault(n =>
            n.Key is { } k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))?.ScalarValue;

    private static int? Number(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static SpriteFrame? Resolve(
        string name,
        Dictionary<string, Declaration> declarations,
        int depth)
    {
        // A sheet reference that points back at itself would otherwise never return.
        if (depth > 8 || !declarations.TryGetValue(name, out var declaration))
        {
            return null;
        }

        if (declaration.Texture is { Length: > 0 } texture)
        {
            return new SpriteFrame(texture, declaration.DefaultFrame ?? 1, declaration.Frames ?? 1)
            {
                BorderSize = declaration.Border,
            };
        }

        if (declaration.Sheet is not { Length: > 0 } sheet ||
            Resolve(sheet, declarations, depth + 1) is not { } parent)
        {
            return null;
        }

        // The sheet supplies the texture and how it is divided; this sprite says which slice.
        return parent with { Frame = declaration.DefaultFrame ?? 1 };
    }

    /// <summary>
    /// The unstretched border a tiled surface keeps, which most sprites do not declare.
    /// </summary>
    /// <remarks>
    /// Written as <c>borderSize = { x = 8 y = 8 }</c>. Nothing in the installation gives one axis
    /// without the other, but each is read on its own so that a file which did would still be
    /// understood rather than silently losing half of it.
    /// </remarks>
    private static (int X, int Y) Border(CwBlock body)
    {
        var border = body.Nodes.FirstOrDefault(n =>
            n.Key is { } k && string.Equals(k, "borderSize", StringComparison.OrdinalIgnoreCase))?.Block;

        return border is null ? default : (Number(Property(border, "x")) ?? 0, Number(Property(border, "y")) ?? 0);
    }

    /// <summary>What one sprite declaration said, before sheet references are followed.</summary>
    private sealed record Declaration(
        string? Texture,
        string? Sheet,
        int? Frames,
        int? DefaultFrame,
        (int X, int Y) Border);
}
