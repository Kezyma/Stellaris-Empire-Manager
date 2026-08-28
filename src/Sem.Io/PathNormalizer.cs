namespace Sem.Io;

/// <summary>
/// Path normalisation shared by every containment check in this assembly.
/// </summary>
/// <remarks>
/// Containment decisions guard the user's game install and empire presets, so they
/// resolve reparse points before comparing. This machine's Documents folder is a
/// OneDrive redirect, which is exactly the case a naive string prefix test misses.
/// </remarks>
public static class PathNormalizer
{
    /// <summary>Comparison used for all path equality and prefix tests.</summary>
    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Produces an absolute path with links resolved and any trailing separator removed,
    /// so that two spellings of the same location compare equal.
    /// </summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return TrimTrailingSeparator(ResolveLinks(Path.GetFullPath(path)));
    }

    /// <summary>
    /// Tests whether <paramref name="candidate"/> is <paramref name="root"/> or sits beneath it.
    /// Both arguments must already be normalised.
    /// </summary>
    public static bool IsWithin(string root, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        if (candidate.Equals(root, Comparison))
        {
            return true;
        }

        if (!candidate.StartsWith(root, Comparison))
        {
            return false;
        }

        // A drive root such as "C:\" already ends in a separator; anything longer is inside it.
        if (EndsWithSeparator(root))
        {
            return true;
        }

        // Guards against "C:\foo" appearing to contain "C:\foobar".
        var next = candidate[root.Length];
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }

    private static bool EndsWithSeparator(string path) =>
        path.Length > 0 &&
        (path[^1] == Path.DirectorySeparatorChar || path[^1] == Path.AltDirectorySeparatorChar);

    private static string TrimTrailingSeparator(string path)
    {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && path.Equals(root, Comparison))
        {
            return path;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? path : trimmed;
    }

    /// <summary>
    /// Resolves symlinks and junctions on the deepest existing ancestor of
    /// <paramref name="fullPath"/>, then re-appends the not-yet-existing remainder.
    /// Targets of a write usually do not exist yet, so resolving the whole path directly
    /// is not an option.
    /// </summary>
    private static string ResolveLinks(string fullPath)
    {
        var remainder = new Stack<string>();
        var current = fullPath;

        while (true)
        {
            var isFile = File.Exists(current);
            if (isFile || Directory.Exists(current))
            {
                try
                {
                    var target = isFile
                        ? File.ResolveLinkTarget(current, returnFinalTarget: true)?.FullName
                        : Directory.ResolveLinkTarget(current, returnFinalTarget: true)?.FullName;

                    if (!string.IsNullOrEmpty(target))
                    {
                        current = Path.GetFullPath(target);
                    }
                }
                catch (IOException)
                {
                    // Unresolvable link: fall back to the literal path rather than failing the check.
                }
                catch (UnauthorizedAccessException)
                {
                }

                break;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent.Equals(current, Comparison))
            {
                break;
            }

            remainder.Push(Path.GetFileName(current));
            current = parent;
        }

        while (remainder.Count > 0)
        {
            current = Path.Combine(current, remainder.Pop());
        }

        return current;
    }
}
