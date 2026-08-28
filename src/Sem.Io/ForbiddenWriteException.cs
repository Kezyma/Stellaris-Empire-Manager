namespace Sem.Io;

/// <summary>
/// Thrown when a write is attempted against a path the active <see cref="WritePolicy"/> refuses.
/// </summary>
public sealed class ForbiddenWriteException : IOException
{
    public ForbiddenWriteException(string path, string reason, string policyDescription)
        : base($"Refused to write to '{path}'. {reason} (policy: {policyDescription})")
    {
        Path = path;
        Reason = reason;
        PolicyDescription = policyDescription;
    }

    /// <summary>The path the caller tried to write to, as supplied.</summary>
    public string Path { get; }

    /// <summary>Why the policy refused it.</summary>
    public string Reason { get; }

    /// <summary>Human-readable name of the policy that refused it.</summary>
    public string PolicyDescription { get; }
}
