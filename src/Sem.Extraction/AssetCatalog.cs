using Sem.Assets;
using Sem.Extraction.Extractors;

namespace Sem.Extraction;

/// <summary>One image to copy out of the game and convert.</summary>
/// <param name="Source">Where it lives in the installation, as a relative path.</param>
/// <param name="Destination">Where it goes in the extracted assets, as a relative path.</param>
/// <param name="MaxDimension">A size to scale down to, when the full resolution is not needed.</param>
/// <param name="Frame">
/// Which slice of the source to take, when the source holds several pictures side by side. Null
/// takes the whole texture.
/// </param>
/// <param name="Channel">
/// A single colour channel to take as the image's transparency, discarding the rest. Used for the
/// flag backgrounds, which are three separate masks packed into one picture's channels.
/// </param>
public sealed record AssetRequest(
    string Source,
    string Destination,
    int? MaxDimension = null,
    SpriteFrame? Frame = null,
    ColorChannel? Channel = null);

/// <summary>One picture in a stack, and the colour the game paints it with.</summary>
/// <param name="Source">Where it lives in the installation, as a relative path.</param>
/// <param name="Frame">Which slice of the source to take, or null for the whole texture.</param>
/// <param name="Tint">A colour to paint it with, or null to leave it as it was drawn.</param>
public sealed record AssetLayer(
    string Source,
    SpriteFrame? Frame = null,
    (byte R, byte G, byte B, byte A)? Tint = null);

/// <summary>Several pictures stacked into one, as the game's own icon scripts describe them.</summary>
/// <param name="Layers">The pictures, bottom first.</param>
/// <param name="Destination">Where the result goes, as a relative path.</param>
public sealed record CompositeRequest(IReadOnlyList<AssetLayer> Layers, string Destination);

/// <summary>
/// Collects the images the extracted data refers to, so they can be converted afterwards.
/// </summary>
/// <remarks>
/// The database stores only where an image will be, not where it came from. Registering both here
/// keeps the mapping in one place, and lets an icon that turns out not to exist resolve to nothing
/// rather than to a path that will fail to load.
/// </remarks>
public sealed class AssetCatalog(LayeredContent content, SpriteCatalog? sprites = null)
{
    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly Dictionary<string, AssetRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CompositeRequest> _composites = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _missing = [];

    /// <summary>The images to convert, one per destination.</summary>
    public IReadOnlyCollection<AssetRequest> Requests => _requests.Values;

    /// <summary>The images to build by stacking others, one per destination.</summary>
    public IReadOnlyCollection<CompositeRequest> Composites => _composites.Values;

    /// <summary>Sources that were referenced but are not in the installation.</summary>
    public IReadOnlyList<string> Missing => _missing;

    /// <summary>The sprite names this installation defines, for resolving <c>GFX_</c> references.</summary>
    public SpriteCatalog Sprites { get; } = sprites ?? SpriteCatalog.Empty;

    /// <summary>
    /// Registers the picture a <c>GFX_</c> sprite name refers to, cutting it out of a shared texture
    /// where that is where it lives.
    /// </summary>
    /// <remarks>
    /// This is how anything the game names by sprite should be registered. Guessing at a file path
    /// from the name works for the icons that happen to have their own file and silently fails for
    /// the ones that do not, which is how the planet pictures came to be the wrong artwork.
    /// </remarks>
    /// <param name="frame">
    /// Which slice of a sheet to take, counting from one, where the caller wants a different one
    /// from the sprite's own. The leader classes are one picture each of a strip of four, and the
    /// spawn setting is one of three; in both cases the slice is chosen elsewhere than in the
    /// sprite, so it has to be said here.
    /// </param>
    public string? RegisterSprite(
        string? spriteName,
        string destination,
        int? maxDimension = null,
        int? frame = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (Sprites.Resolve(spriteName) is not { } sprite)
        {
            _missing.Add(spriteName is { Length: > 0 } name ? $"sprite {name}" : destination);
            return null;
        }

        if (!_content.Contains(sprite.Texture))
        {
            _missing.Add(sprite.Texture);
            return null;
        }

        // A slice the sheet does not hold is refused rather than cut anyway: the envoy asks for the
        // fifth of four, and a picture made from past the end of the strip would be worse than none.
        if (frame is { } asked && (asked < 1 || asked > sprite.FrameCount))
        {
            _missing.Add($"sprite {spriteName} frame {asked} of {sprite.FrameCount}");
            return null;
        }

        var wanted = frame is { } slice ? sprite with { Frame = slice } : sprite;

        _requests[destination] = new AssetRequest(
            wanted.Texture,
            destination,
            maxDimension,
            wanted.IsWholeTexture ? null : wanted);

        return destination;
    }

    /// <summary>
    /// Registers a picture built by stacking several sprites, each optionally painted.
    /// </summary>
    /// <remarks>
    /// A sprite that cannot be resolved drops out of the stack rather than losing the whole icon:
    /// the layers are independent by construction, and a trait wearing its background and glyph
    /// without its tier marker is far better than one wearing nothing.
    /// </remarks>
    /// <param name="layers">
    /// What to stack, bottom first, each naming a sprite. A tint of null leaves the layer as drawn.
    /// </param>
    public string? RegisterLayers(
        IEnumerable<(string? Sprite, (byte R, byte G, byte B, byte A)? Tint)> layers,
        string destination)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var resolved = new List<AssetLayer>();

        foreach (var (spriteName, tint) in layers)
        {
            if (Sprites.Resolve(spriteName) is not { } sprite)
            {
                _missing.Add(spriteName is { Length: > 0 } name ? $"sprite {name}" : destination);
                continue;
            }

            if (!_content.Contains(sprite.Texture))
            {
                _missing.Add(sprite.Texture);
                continue;
            }

            resolved.Add(new AssetLayer(sprite.Texture, sprite.IsWholeTexture ? null : sprite, tint));
        }

        if (resolved.Count == 0)
        {
            return null;
        }

        _composites[destination] = new CompositeRequest(resolved, destination);
        return destination;
    }

    /// <summary>
    /// Registers one colour channel of a texture as a standalone transparency mask.
    /// </summary>
    public string? RegisterChannel(
        string source,
        string destination,
        ColorChannel channel,
        int? maxDimension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (!_content.Contains(source))
        {
            _missing.Add(source);
            return null;
        }

        _requests[destination] = new AssetRequest(source, destination, maxDimension, Channel: channel);
        return destination;
    }

    /// <summary>
    /// Registers an image and returns where it will be, or null when the installation does not
    /// have it. A caller storing the result gets an icon path only when there is really an icon.
    /// </summary>
    public string? Register(string source, string destination, int? maxDimension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (!_content.Contains(source))
        {
            _missing.Add(source);
            return null;
        }

        _requests[destination] = new AssetRequest(source, destination, maxDimension);
        return destination;
    }

    /// <summary>
    /// Registers the first of several candidate sources that exists, which suits the icons the game
    /// names by convention but occasionally overrides.
    /// </summary>
    public string? RegisterFirst(IEnumerable<string> sources, string destination, int? maxDimension = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (var source in sources)
        {
            if (_content.Contains(source))
            {
                return Register(source, destination, maxDimension);
            }
        }

        _missing.Add(destination);
        return null;
    }
}
