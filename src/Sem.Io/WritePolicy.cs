using System.Collections.Immutable;

namespace Sem.Io;

/// <summary>
/// Decides which paths this process may write to. Every write in the solution goes through
/// <see cref="SafeFile"/>, which consults a policy first.
/// </summary>
/// <remarks>
/// <para>
/// The project rule this enforces: development never modifies the real Stellaris install or
/// the user's real empire presets. Work happens against copies under the repo sandbox.
/// </para>
/// <para>
/// Forbidden roots always beat allowed roots, so registering the game install with
/// <see cref="Forbidding"/> makes writes into it impossible even if a caller later allows a
/// parent directory by mistake.
/// </para>
/// </remarks>
public sealed class WritePolicy
{
    private readonly ImmutableArray<string> _allowed;
    private readonly ImmutableArray<string> _forbidden;

    private WritePolicy(ImmutableArray<string> allowed, ImmutableArray<string> forbidden, string description)
    {
        _allowed = allowed;
        _forbidden = forbidden;
        Description = description;
    }

    /// <summary>A policy that refuses every write. The safe default.</summary>
    public static WritePolicy DenyAll { get; } = new([], [], "deny-all");

    /// <summary>Human-readable name, included in refusal messages.</summary>
    public string Description { get; }

    /// <summary>Normalised roots writes are permitted under.</summary>
    public IReadOnlyList<string> AllowedRoots => _allowed;

    /// <summary>Normalised roots writes are refused under, whatever else is allowed.</summary>
    public IReadOnlyList<string> ForbiddenRoots => _forbidden;

    /// <summary>
    /// The development policy: writes are confined to the repo sandbox, the process temp
    /// directory, and the local application-data cache.
    /// </summary>
    public static WritePolicy ForDevelopment(string sandboxRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxRoot);

        return new WritePolicy([], [], "development")
            .Allowing(sandboxRoot, Path.GetTempPath(), LocalCacheRoot());
    }

    /// <summary>
    /// The shipped-application policy: writes are confined to the local application-data cache
    /// and the temp directory until the user picks a designs file, which the host then adds
    /// with <see cref="Allowing"/>.
    /// </summary>
    public static WritePolicy ForApplication()
    {
        return new WritePolicy([], [], "application")
            .Allowing(Path.GetTempPath(), LocalCacheRoot());
    }

    /// <summary>Returns a copy of this policy that also permits writes under <paramref name="roots"/>.</summary>
    public WritePolicy Allowing(params IEnumerable<string> roots)
    {
        var added = Normalize(roots);
        return added.IsEmpty ? this : new WritePolicy(Merge(_allowed, added), _forbidden, Description);
    }

    /// <summary>Returns a copy of this policy that refuses writes under <paramref name="roots"/>.</summary>
    public WritePolicy Forbidding(params IEnumerable<string> roots)
    {
        var added = Normalize(roots);
        return added.IsEmpty ? this : new WritePolicy(_allowed, Merge(_forbidden, added), Description);
    }

    /// <summary>Returns a copy of this policy under a different name.</summary>
    public WritePolicy Named(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new WritePolicy(_allowed, _forbidden, description);
    }

    /// <summary>True when <paramref name="path"/> may be written to.</summary>
    public bool IsWritable(string path) => Validate(path) is null;

    /// <summary>
    /// Returns why <paramref name="path"/> may not be written to, or <see langword="null"/>
    /// when the write is permitted.
    /// </summary>
    public string? Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "The path is empty.";
        }

        string normalized;
        try
        {
            normalized = PathNormalizer.Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"The path could not be resolved: {ex.Message}";
        }

        foreach (var root in _forbidden)
        {
            if (PathNormalizer.IsWithin(root, normalized))
            {
                return $"It is inside the protected location '{root}', which is never writable.";
            }
        }

        foreach (var root in _allowed)
        {
            if (PathNormalizer.IsWithin(root, normalized))
            {
                return null;
            }
        }

        return _allowed.Length == 0
            ? "No write locations are permitted by this policy."
            : $"It is outside every permitted write location ({string.Join("; ", _allowed)}).";
    }

    /// <summary>Throws <see cref="ForbiddenWriteException"/> unless <paramref name="path"/> may be written to.</summary>
    public void EnsureWritable(string path)
    {
        var reason = Validate(path);
        if (reason is not null)
        {
            throw new ForbiddenWriteException(path, reason, Description);
        }
    }

    /// <summary>Root of this application's cache under the local application-data folder.</summary>
    public static string LocalCacheRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StellarisEmpireManager");

    private static ImmutableArray<string> Normalize(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                builder.Add(PathNormalizer.Normalize(root));
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> Merge(ImmutableArray<string> existing, ImmutableArray<string> added)
    {
        var builder = existing.ToBuilder();
        foreach (var root in added)
        {
            if (!builder.Contains(root, StringComparer.FromComparison(PathNormalizer.Comparison)))
            {
                builder.Add(root);
            }
        }

        return builder.ToImmutable();
    }
}
