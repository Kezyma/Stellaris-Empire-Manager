namespace Sem.Ui.Services;

/// <summary>
/// Whether the empire list has its editor open over it.
/// </summary>
/// <remarks>
/// <para>
/// The editor is a dialog on the list rather than a page of its own, so most of what opens it is the
/// list's own business and needs nothing from here: a card was pressed, or a link was opened. Two
/// things are not. The header's Create builds an empire from wherever it is pressed, and the
/// advanced editor is a real page that has to be able to send the reader back to the one they came
/// from - and both of those arrive at the list as a fresh navigation, with no way to say why.
/// </para>
/// <para>
/// Not the address, which was the other candidate. The address only ever carries a saved empire, for
/// the reason the address rewrite sets out at length, so an editor holding unsaved work could not be
/// described by it - and reopening the list would either lose the work or add a second copy of it.
/// </para>
/// </remarks>
public sealed class EditorState
{
    /// <summary>Whether the list should draw its editor over itself.</summary>
    public bool IsOpen { get; set; }
}
