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
    private readonly List<string> _missing = [];

    /// <summary>The images to convert, one per destination.</summary>
    public IReadOnlyCollection<AssetRequest> Requests => _requests.Values;

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
    public string? RegisterSprite(string? spriteName, string destination, int? maxDimension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (Sprites.Resolve(spriteName) is not { } frame)
        {
            _missing.Add(spriteName is { Length: > 0 } name ? $"sprite {name}" : destination);
            return null;
        }

        if (!_content.Contains(frame.Texture))
        {
            _missing.Add(frame.Texture);
            return null;
        }

        _requests[destination] = new AssetRequest(
            frame.Texture,
            destination,
            maxDimension,
            frame.IsWholeTexture ? null : frame);

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
