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

    /// <summary>
    /// Reads the copy as an empire in its own right.
    /// </summary>
    /// <remarks>
    /// For asking questions of the empire as it was rather than for putting it back: what its
    /// modifiers came to before the last few edits, so the designer can show what those edits
    /// changed. The design returned is not in any file and belongs to nobody — editing it would edit
    /// the copy, which is why nothing here hands one out to be edited.
    /// </remarks>
    public EmpireDesign ToDesign() => new(Entry.Clone());
}
