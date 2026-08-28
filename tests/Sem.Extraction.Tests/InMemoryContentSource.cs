using System.Text;
using Sem.Extraction;

namespace Sem.Extraction.Tests;

/// <summary>
/// A content layer built from strings, so extraction can be tested without a game installation.
/// </summary>
public sealed class InMemoryContentSource : IContentSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string Name => "in-memory";

    /// <summary>Adds a file, given its content as text.</summary>
    public InMemoryContentSource Add(string relativePath, string content)
    {
        _files[Normalize(relativePath)] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <summary>Adds a file, given its content as bytes.</summary>
    public InMemoryContentSource Add(string relativePath, byte[] content)
    {
        _files[Normalize(relativePath)] = content;
        return this;
    }

    /// <summary>Wraps this layer as a content stack.</summary>
    public LayeredContent AsContent() => new([this]);

    /// <inheritdoc />
    public bool Contains(string relativePath) => _files.ContainsKey(Normalize(relativePath));

    /// <inheritdoc />
    public bool ContainsDirectory(string relativeDirectory)
    {
        var prefix = Normalize(relativeDirectory) + "/";
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public byte[] Read(string relativePath) => _files.TryGetValue(Normalize(relativePath), out var content)
        ? content
        : throw new FileNotFoundException(relativePath);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string relativeDirectory, string pattern, bool recursive = false)
    {
        var prefix = Normalize(relativeDirectory) + "/";
        var extension = pattern.TrimStart('*');

        foreach (var path in _files.Keys.Order(StringComparer.Ordinal))
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (recursive || !path[prefix.Length..].Contains('/', StringComparison.Ordinal))
            {
                yield return path;
            }
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');
}
