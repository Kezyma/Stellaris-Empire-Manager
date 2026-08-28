using Sem.Io;

namespace Sem.Extraction;

/// <summary>
/// A place game content is read from. Read-only by design: extraction must never be able to alter
/// an installation.
/// </summary>
/// <remarks>
/// Sources are layered so mods can be added later without changing any extraction stage. A stage
/// asks for <c>common/traits</c> and gets whatever the topmost layer provides, exactly as the game
/// resolves overrides.
/// </remarks>
public interface IContentSource
{
    /// <summary>A name for this layer, used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Whether a file exists in this layer.</summary>
    bool Contains(string relativePath);

    /// <summary>Reads a file's bytes. Throws when it is not present.</summary>
    byte[] Read(string relativePath);

    /// <summary>
    /// Lists files matching a pattern, as paths relative to the layer root, using forward slashes.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string relativeDirectory, string pattern, bool recursive = false);

    /// <summary>Whether a directory exists in this layer.</summary>
    bool ContainsDirectory(string relativeDirectory);
}

/// <summary>A content layer backed by a directory on disk, such as a game installation.</summary>
public sealed class DirectoryContentSource : IContentSource
{
    private readonly string _root;

    public DirectoryContentSource(string root, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Content directory '{root}' does not exist.");
        }

        _root = PathNormalizer.Normalize(root);
        Name = name ?? Path.GetFileName(_root);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The directory this layer reads from.</summary>
    public string Root => _root;

    /// <inheritdoc />
    public bool Contains(string relativePath) => File.Exists(Resolve(relativePath));

    /// <inheritdoc />
    public bool ContainsDirectory(string relativeDirectory) => Directory.Exists(Resolve(relativeDirectory));

    /// <inheritdoc />
    public byte[] Read(string relativePath) => SafeFile.ReadAllBytes(Resolve(relativePath));

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string relativeDirectory, string pattern, bool recursive = false)
    {
        var directory = Resolve(relativeDirectory);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Ordered by name: the game loads a directory alphabetically and lets later files win.
        foreach (var path in Directory.EnumerateFiles(directory, pattern, option).Order(StringComparer.Ordinal))
        {
            yield return Path.GetRelativePath(_root, path).Replace('\\', '/');
        }
    }

    private string Resolve(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

/// <summary>
/// Several content layers stacked, with later layers overriding earlier ones by relative path.
/// </summary>
/// <remarks>
/// With one layer this is just the base game. The stacking exists so mod folders can be appended
/// later without any extraction stage needing to know they exist.
/// </remarks>
public sealed class LayeredContent(IReadOnlyList<IContentSource> layers)
{
    private readonly IReadOnlyList<IContentSource> _layers = layers?.Count > 0
        ? layers
        : throw new ArgumentException("At least one content layer is required.", nameof(layers));

    /// <summary>Builds a single-layer stack over a game installation.</summary>
    public static LayeredContent ForInstall(string installRoot) =>
        new([new DirectoryContentSource(installRoot, "base game")]);

    /// <summary>The layers, lowest first.</summary>
    public IReadOnlyList<IContentSource> Layers => _layers;

    /// <summary>Whether any layer provides this file.</summary>
    public bool Contains(string relativePath) => _layers.Any(l => l.Contains(relativePath));

    /// <summary>Whether any layer provides this directory.</summary>
    public bool ContainsDirectory(string relativeDirectory) =>
        _layers.Any(l => l.ContainsDirectory(relativeDirectory));

    /// <summary>Reads a file from the topmost layer that provides it.</summary>
    public byte[] Read(string relativePath)
    {
        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i].Contains(relativePath))
            {
                return _layers[i].Read(relativePath);
            }
        }

        throw new FileNotFoundException($"No content layer provides '{relativePath}'.", relativePath);
    }

    /// <summary>
    /// Lists the files in a directory across all layers, in load order, with each relative path
    /// appearing once even if several layers provide it.
    /// </summary>
    public IReadOnlyList<string> EnumerateFiles(
        string relativeDirectory,
        string pattern = "*.txt",
        bool recursive = false)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var layer in _layers)
        {
            foreach (var path in layer.EnumerateFiles(relativeDirectory, pattern, recursive))
            {
                if (seen.Add(path))
                {
                    ordered.Add(path);
                }
            }
        }

        ordered.Sort(StringComparer.Ordinal);
        return ordered;
    }
}
