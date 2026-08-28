namespace Sem.Extraction;

/// <summary>One image to copy out of the game and convert.</summary>
/// <param name="Source">Where it lives in the installation, as a relative path.</param>
/// <param name="Destination">Where it goes in the extracted assets, as a relative path.</param>
/// <param name="MaxDimension">A size to scale down to, when the full resolution is not needed.</param>
public sealed record AssetRequest(string Source, string Destination, int? MaxDimension = null);

/// <summary>
/// Collects the images the extracted data refers to, so they can be converted afterwards.
/// </summary>
/// <remarks>
/// The database stores only where an image will be, not where it came from. Registering both here
/// keeps the mapping in one place, and lets an icon that turns out not to exist resolve to nothing
/// rather than to a path that will fail to load.
/// </remarks>
public sealed class AssetCatalog(LayeredContent content)
{
    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly Dictionary<string, AssetRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _missing = [];

    /// <summary>The images to convert, one per destination.</summary>
    public IReadOnlyCollection<AssetRequest> Requests => _requests.Values;

    /// <summary>Sources that were referenced but are not in the installation.</summary>
    public IReadOnlyList<string> Missing => _missing;

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
