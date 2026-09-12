namespace Sem.Io;

/// <summary>
/// Thrown when a write is attempted against a path the active <see cref="WritePolicy"/> refuses.
/// </summary>
public sealed class ForbiddenWriteException : IOException
{
    /// <summary>Records a write the policy refused, and why.</summary>
    /// <param name="path">The path the caller tried to write to, as supplied.</param>
    /// <param name="reason">Why the policy refused it.</param>
    /// <param name="policyDescription">Which policy was in force, for the message.</param>
    public ForbiddenWriteException(string path, string reason, string policyDescription)
        : base($"Refused to write to '{path}'. {reason} (policy: {policyDescription})")
    {
        Path = path;
        Reason = reason;
    }

    /// <summary>The path the caller tried to write to, as supplied.</summary>
    public string Path { get; }

    /// <summary>Why the policy refused it.</summary>
    public string Reason { get; }
}
