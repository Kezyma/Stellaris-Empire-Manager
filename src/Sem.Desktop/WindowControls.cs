using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// The window buttons drawn in the designer's own header, driving the real window behind them.
/// </summary>
/// <remarks>
/// <para>
/// No interop with the page at all. Blazor runs in this process, so a button in the header calls
/// straight into WPF - the same arrangement <see cref="DesktopFileExchange"/> uses for saving.
/// </para>
/// <para>
/// Moving and resizing are handed to Windows rather than done by hand, by telling it the press
/// landed on the caption or on a particular border. Everything a window is expected to do then
/// follows for free: snapping to an edge, the half-screen preview, shake, the double-click, and
/// restoring by dragging a maximised window away from the top.
/// </para>
/// <para>
/// WPF's own <see cref="Window.DragMove"/> cannot be used for this. It asserts that the left button
/// is down, and WPF never saw it go down: the press landed on the web view's window, which is a
/// child window of its own, so the mouse belongs to that and not to anything WPF is watching. The
/// same fact is why the header draws its own resize grips - <c>WindowChrome</c>'s borders are hit
/// tested by the window underneath the web view, which never sees the pointer.
/// </para>
/// </remarks>
internal sealed class WindowControls : IWindowControls
{
    private const uint WmNcLButtonDown = 0x00A1;
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private readonly Window _window;

    /// <summary>Takes the window this is to drive, and follows what it does.</summary>
    public WindowControls(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _window.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public bool IsMaximised => _window.WindowState == WindowState.Maximized;

    /// <inheritdoc />
    public void Minimise() => _window.WindowState = WindowState.Minimized;

    /// <inheritdoc />
    public void ToggleMaximise() =>
        _window.WindowState = IsMaximised ? WindowState.Normal : WindowState.Maximized;

    /// <inheritdoc />
    public void Close() => _window.Close();

    /// <inheritdoc />
    public void BeginDrag() => Begin(HtCaption);

    /// <inheritdoc />
    public void BeginResize(WindowEdge edge) => Begin(edge switch
    {
        WindowEdge.Left => HtLeft,
        WindowEdge.Right => HtRight,
        WindowEdge.Top => HtTop,
        WindowEdge.TopLeft => HtTopLeft,
        WindowEdge.TopRight => HtTopRight,
        WindowEdge.Bottom => HtBottom,
        WindowEdge.BottomLeft => HtBottomLeft,
        _ => HtBottomRight,
    });

    /// <summary>
    /// Tells the window the press it is already holding landed on part of its frame.
    /// </summary>
    /// <remarks>
    /// The release of the capture is the part that is easy to leave out and impossible to work
    /// around afterwards: the web view captured the mouse when the button went down, and Windows
    /// will not begin a move or a size for a window that does not have it.
    /// </remarks>
    private void Begin(int area)
    {
        var handle = new WindowInteropHelper(_window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmNcLButtonDown, area, IntPtr.Zero);
    }

    /// <summary>
    /// Keeps a maximised window inside the screen it is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A window with no title bar maximises to the whole monitor rather than to the part of it the
    /// taskbar leaves, and to seven pixels beyond each edge besides - which is the sizing border it
    /// still has, and which is invisible only while the frame is drawing it. The WindowChrome does
    /// not answer this one: Windows asks the window how large "maximised" is, and something has to
    /// answer.
    /// </para>
    /// <para>
    /// Measured rather than assumed. Without this, a 1920x1080 screen with a 48px taskbar was given
    /// a 1934x1094 window: clipped on all four sides, with the taskbar buried under it.
    /// </para>
    /// <para>
    /// The monitor is asked each time rather than remembered, because the answer changes - a second
    /// screen has its own resolution and its own taskbar, and a window is maximised on whichever it
    /// is on at the time.
    /// </para>
    /// </remarks>
    internal static void KeepMaximisedInsideTheScreen(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                source.AddHook(OnWindowMessage);
            }
        };
    }

    private static IntPtr OnWindowMessage(
        IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);

        if (monitor == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (!GetMonitorInfo(monitor, ref info))
        {
            return IntPtr.Zero;
        }

        var bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        // Relative to the monitor rather than to the desktop, which is what this field means and is
        // the whole difference on a second screen.
        bounds.MaxPosition.X = info.Work.Left - info.Monitor.Left;
        bounds.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
        bounds.MaxSize.X = info.Work.Right - info.Work.Left;
        bounds.MaxSize.Y = info.Work.Bottom - info.Work.Top;

        Marshal.StructureToPtr(bounds, lParam, fDeleteOld: false);

        handled = true;
        return IntPtr.Zero;
    }

    // The fields are filled in by Windows through the marshaller, which the compiler cannot see.
#pragma warning disable CS0649
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }
#pragma warning restore CS0649

    // DllImport rather than LibraryImport, which generates its marshalling into unsafe code and so
    // asks for unsafe to be turned on across the whole project. Nothing crosses here but integers
    // and one fixed-shape record, so there is no marshalling worth generating.
#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint fallback);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#pragma warning restore SYSLIB1054
}
