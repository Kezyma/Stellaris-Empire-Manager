namespace Sem.Clausewitz;

/// <summary>Thrown when Paradox script cannot be parsed.</summary>
public sealed class CwSyntaxException(string message, int offset = -1) : FormatException(message)
{
    /// <summary>Character offset the problem was found at, or -1 when not known.</summary>
    public int Offset { get; } = offset;
}
