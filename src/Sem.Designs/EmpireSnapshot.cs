using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// One empire as it stood at a moment, kept so that it can be put back.
/// </summary>
/// <remarks>
/// This is what makes an explicit save possible: the designer works on the live design and keeps one
/// of these beside it, so "revert" is a real answer and not a promise to remember what was pressed.
/// It holds the empire and nothing else — the rest of the file is not part of what is being edited,
/// and reverting one empire has never meant undoing another.
/// </remarks>
public sealed class EmpireSnapshot
{
    internal EmpireSnapshot(CwNode entry) => Entry = entry;

    /// <summary>The copied entry, complete with its key and every field beneath it.</summary>
    internal CwNode Entry { get; }
}
