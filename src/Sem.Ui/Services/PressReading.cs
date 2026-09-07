using Microsoft.AspNetCore.Components.Web;

namespace Sem.Ui.Services;

/// <summary>
/// Whether a press on an option means "tell me about this" or "give me this".
/// </summary>
/// <remarks>
/// <para>
/// A mouse asks both questions and never confuses them: hovering reads and clicking chooses. A
/// finger has one gesture for the two, and the app resolved it by choosing - so the only way to
/// find out what a trait did was to take it, watch the budget move, and take it back. In a picker
/// where the choice spends points and orders a list, that is not browsing.
/// </para>
/// <para>
/// So on a touch screen the first press on a row reads it and the second takes it. Nothing is added
/// to the screen for this, which matters where the list is eighty traits long and every pixel is
/// multiplied by the row - and it works the same on the icon grids, where there is no room to add
/// anything at all.
/// </para>
/// <para>
/// Told apart by the press itself rather than by asking the browser what kind of device this is. A
/// pointer event carries what made it, so a laptop with a touchscreen behaves like a mouse under
/// the mouse and like a finger under the finger, which asking once at startup could not manage. A
/// press with no pointer at all is the keyboard, and that already reads on focus, so it chooses.
/// </para>
/// <para>
/// What has been read is remembered here rather than inferred from whatever the detail panel is
/// showing, and that distinction is the whole of why this works on a real phone. A touch screen
/// sends the mouse events too: a tap fires <c>mouseenter</c> before <c>click</c>, so by the time
/// the press arrived the panel was already describing the row it was about to take, and every first
/// tap looked like a second one. Asked in a browser it passed; asked with a finger it selected
/// everything it touched.
/// </para>
/// </remarks>
public sealed class PressReading
{
    private bool _fromFinger;
    private string? _read;

    /// <summary>Records what the press was made with. Bind to the row's <c>onpointerdown</c>.</summary>
    public void Noted(PointerEventArgs pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);

        _fromFinger = string.Equals(pointer.PointerType, "touch", StringComparison.Ordinal);

        // A mouse arriving means the reading mark belongs to nothing: it is drawn for a finger, and
        // a hybrid machine can change hands between one press and the next.
        if (!_fromFinger)
        {
            _read = null;
        }
    }

    /// <summary>
    /// The same, for a host that sends touches without pointer events.
    /// </summary>
    /// <remarks>
    /// Bind to <c>ontouchstart</c> beside the other. Pointer events are the better answer because
    /// they name what made them, but a browser that has only the old events would otherwise look
    /// like a mouse and choose on the first press - which is the failure this exists to avoid, so
    /// it is worth the second listener.
    /// </remarks>
    public void NotedTouch() => _fromFinger = true;

    /// <summary>
    /// Whether this press should only show the description, leaving the choice to the next one.
    /// </summary>
    /// <remarks>
    /// Records the answer as it gives it, so the next press on the same row chooses. Called once per
    /// press, from the handler rather than from the drawing.
    /// </remarks>
    /// <param name="key">The option being pressed.</param>
    public bool ReadsOnly(string key)
    {
        if (!_fromFinger || string.Equals(_read, key, StringComparison.Ordinal))
        {
            return false;
        }

        _read = key;
        return true;
    }

    /// <summary>
    /// Whether the row should show that it is being read rather than merely pointed at.
    /// </summary>
    /// <remarks>
    /// Only under a finger. A mouse has a hover highlight already, and marking the described row as
    /// well would put two marks on it - or one on a row the pointer left, which reads as a
    /// selection that did not happen.
    /// </remarks>
    public bool IsReading(string key) =>
        _fromFinger && string.Equals(_read, key, StringComparison.Ordinal);
}
