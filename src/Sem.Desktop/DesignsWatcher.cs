using System.IO;
using System.Windows.Threading;

namespace Sem.Desktop;

/// <summary>
/// Watches the player's designs file and says so once whoever was writing it has finished.
/// </summary>
/// <remarks>
/// <para>
/// There is a second writer, and it is the game: Stellaris rewrites this file every time an empire
/// is created or edited in it. Keeping the app in step with that is the whole point of watching.
/// </para>
/// <para>
/// One write is never one event. A replacement is a delete, a create and a rename; a plain save is
/// several writes and a size change; and the file is readable in the middle of all of it, half
/// written. So nothing is reported until four hundred milliseconds have passed with nothing further
/// happening, and the reader is expected to find a file it cannot parse anyway and try again.
/// </para>
/// <para>
/// The settling is a <see cref="DispatcherTimer"/> rather than a timer of its own, which is what
/// puts the callback on the thread the app renders on. File system events arrive on the thread pool,
/// and the session they would go on to change belongs to the UI thread.
/// </para>
/// </remarks>
internal sealed class DesignsWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _settled;

    /// <summary>Starts watching one file in one folder.</summary>
    public DesignsWatcher(string directory, string fileName, Action onChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(onChanged);

        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;

        _settled = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };

        _settled.Tick += (_, _) =>
        {
            _settled.Stop();
            onChanged();
        };

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            // Renames and creations included, because a careful writer - this app among them -
            // stages the new contents beside the file and swaps them in rather than writing over it.
            NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.FileName
                | NotifyFilters.CreationTime,
        };

        _watcher.Changed += Touched;
        _watcher.Created += Touched;
        _watcher.Deleted += Touched;
        _watcher.Renamed += Touched;

        // Last, so that no event can arrive before the timer it would restart exists.
        _watcher.EnableRaisingEvents = true;
    }

    private void Touched(object sender, FileSystemEventArgs e) =>
        _settled.Dispatcher.BeginInvoke(() =>
        {
            _settled.Stop();
            _settled.Start();
        });

    /// <summary>Stops watching, and drops anything that was about to be reported.</summary>
    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        // On the dispatcher, because a timer may only be stopped from the thread it belongs to.
        _settled.Dispatcher.BeginInvoke(_settled.Stop);
    }
}
