namespace Sem.Ui.Services;

/// <summary>
/// The edge a window is being resized from.
/// </summary>
/// <remarks>
/// Named rather than left to the host's own numbering, so the page can say which grip was taken
/// hold of without knowing anything about the window underneath it.
/// </remarks>
public enum WindowEdge
{
    /// <summary>The left edge.</summary>
    Left,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The top left corner.</summary>
    TopLeft,

    /// <summary>The top right corner.</summary>
    TopRight,

    /// <summary>The bottom edge.</summary>
    Bottom,

    /// <summary>The bottom left corner.</summary>
    BottomLeft,

    /// <summary>The bottom right corner.</summary>
    BottomRight,
}

/// <summary>
/// The window the app is drawn in, on a host that draws one of its own.
/// </summary>
/// <remarks>
/// <para>
/// Only the desktop registers this, and the header asks for it rather than requiring it: a browser
/// tab has no window to minimise, and where there is no service the buttons are not drawn at all.
/// </para>
/// <para>
/// <see cref="IFileExchange.SavesInPlace"/> is not the test for that, however much it looks like
/// one. It is false on the desktop as well whenever no designs file was found, which is still a
/// desktop window with no title bar of its own and no other way to be closed.
/// </para>
/// </remarks>
public interface IWindowControls
{
    /// <summary>Raised when the window is maximised or restored, however that came about.</summary>
    /// <remarks>
    /// Not only by the button beside it: dragging to the top of the screen, the snap shortcuts and
    /// the taskbar all maximise a window, and the glyph has to agree with what the window did.
    /// </remarks>
    event EventHandler? StateChanged;

    /// <summary>Whether the window currently fills the screen.</summary>
    bool IsMaximised { get; }

    /// <summary>Sends the window to the taskbar.</summary>
    void Minimise();

    /// <summary>Fills the screen, or gives back the size the window had before it did.</summary>
    void ToggleMaximise();

    /// <summary>
    /// Closes the window, asking whatever the window itself asks before it goes.
    /// </summary>
    /// <remarks>
    /// This is a close, not an exit: the desktop refuses to close over unsaved work unless the
    /// player says so, and that question belongs to the window rather than to this button.
    /// </remarks>
    void Close();

    /// <summary>Begins moving the window, as though the press had landed on a title bar.</summary>
    void BeginDrag();

    /// <summary>Begins resizing the window, as though the press had landed on that border.</summary>
    void BeginResize(WindowEdge edge);
}
