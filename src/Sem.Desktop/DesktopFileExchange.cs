using System.IO;
using Sem.Io;
using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// Saves the designs file back where it came from.
/// </summary>
/// <remarks>
/// This is the careful way to write the player's real empire designs, and the only one that is:
/// the new contents are staged beside the file and swapped in, the previous version is kept, and a
/// copy goes to the app's own archive as well. Losing a file full of hand-built empires is the
/// worst thing this app could do.
///
/// The web app's Export can now reach that file too, through the browser's own save dialog, but it
/// arrives with none of this - no dated backup, no archive, and no write policy, because none of
/// that code exists in a tab. See docs/file-safety.md.
/// </remarks>
public sealed class DesktopFileExchange(SafeFile file, string designsPath) : IFileExchange
{
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly string _designsPath = designsPath ?? throw new ArgumentNullException(nameof(designsPath));

    /// <summary>Where a copy of each replaced version goes, out of the player's folder.</summary>
    private readonly FileArchive _archive = new(
        file!, Path.Combine(WritePolicy.LocalCacheRoot(), "archive"));

    /// <summary>
    /// What the file held the last time this app read or wrote it.
    /// </summary>
    /// <remarks>
    /// Kept here rather than by the thing that polls, because it has to exist whether or not
    /// anything is polling. The sync only runs when the player has switched it on, and a write
    /// going over somebody else's is no less of a loss for the switch being off - it was simply
    /// nobody's job to notice.
    /// </remarks>
    private byte[]? _baseline;

    /// <inheritdoc />
    public bool SavesInPlace => true;

    /// <inheritdoc />
    public string SaveVerb => "Save";

    /// <summary>Whether the editor is currently holding work nobody has saved.</summary>
    /// <remarks>
    /// The window reads this when it is asked to close. A browser has beforeunload and the editor
    /// already keeps it in step through <see cref="WarnBeforeLeavingAsync"/>; a WPF window has
    /// Closing, which runs outside Blazor entirely and cannot await a dialog - so the same call that
    /// arms the browser's warning leaves a flag here for the window to read synchronously.
    /// </remarks>
    public bool HasUnsavedWork { get; private set; }

    /// <inheritdoc />
    public Task WarnBeforeLeavingAsync(bool unsaved)
    {
        HasUnsavedWork = unsaved;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The published site, which is where a shared link has to point.
    /// </summary>
    /// <remarks>
    /// The web view serves the app from an origin only this process can reach, so a link built from
    /// the window's own address opened nothing anywhere. The same design read from the same link
    /// works on the site, which is the thing another person can actually be sent to.
    /// </remarks>
    public string? ShareBaseUri => PublishedSite;

    /// <summary>
    /// The address the site is published at, shared with the stand-in used when no designs file was
    /// found - the question is about the host, and both of them are the same host.
    /// </summary>
    internal const string PublishedSite = "https://kezyma.github.io/Stellaris-Empire-Manager/";

    /// <summary>
    /// Puts text on the Windows clipboard.
    /// </summary>
    /// <remarks>
    /// Without this the share button inherited the interface's default of "did not work" and said
    /// nothing about it, so on the desktop it looked identical to the one that does. The clipboard
    /// belongs to the UI thread and can be held by another process, which is what the retry count
    /// is for; a refusal is reported rather than swallowed.
    /// </remarks>
    public Task<bool> CopyToClipboardAsync(string text) => CopyAsync(text);

    /// <summary>
    /// The clipboard itself, which belongs to the window rather than to the designs file - so the
    /// stand-in used when no file was found shares it.
    /// </summary>
    internal static Task<bool> CopyAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null)
        {
            return Task.FromResult(false);
        }

        return dispatcher.InvokeAsync(() =>
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Another process had the clipboard open. Nothing was copied, and the button says so.
                return false;
            }
        }).Task;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every read moves the baseline, which is what makes the check before a write mean anything:
    /// the question it answers is "has anything written this since we last looked", and a look is
    /// exactly what this is. The sync reads through here too, so its polling and this stay in step
    /// without either knowing about the other.
    /// </remarks>
    public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
    {
        if (!File.Exists(_designsPath))
        {
            _baseline = null;

            return Task.FromResult<(string, byte[])?>(null);
        }

        var contents = SafeFile.ReadAllBytes(_designsPath);
        _baseline = contents;

        return Task.FromResult<(string, byte[])?>((Path.GetFileName(_designsPath), contents));
    }

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
        SaveAsync(fileName, contents, backUp: true);

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
    {
        ArgumentNullException.ThrowIfNull(contents);

        // Nothing is written over a change this app has not seen. The game rewrites this file as it
        // exits, so the ordinary way to meet this is to edit here, quit Stellaris, and press Save -
        // which used to put the editor's copy straight over what the game had just written, without
        // a word. The caller is told instead, and asks.
        if (Moved())
        {
            return Task.FromResult(SaveOutcome.Conflicted);
        }

        // A copy of what is about to be written over. This used to be handed the bytes about to be
        // written instead, which is a record of what each save created and no way back from any of
        // them - so the archive is asked for the file rather than told what is in it, and there is
        // no longer a wrong thing to pass. The app's own copy, out of sight and out of the
        // player's folder, and it costs them nothing to have.
        _archive.KeepReplaced(_designsPath);

        _file.ReplaceAtomically(
            _designsPath, contents, backUp ? SafeFile.DatedBackupPath(_designsPath) : null);

        // What was written is what is there, so the next write is measured against this one.
        _baseline = contents;

        // Saved, now that the refusal above is the only other way out. A failure throws rather than
        // returning; the outcomes about dialogs only arise in a browser, where a player may say no.
        return Task.FromResult(SaveOutcome.Saved);
    }

    /// <summary>
    /// Whether the file has been written by something else since this app last looked at it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compared by contents rather than by a timestamp, because a timestamp answers a different
    /// question: a file rewritten with the same bytes has moved by the clock and not at all by
    /// anything a player would call a change, and being asked about that would teach them to
    /// dismiss the question without reading it.
    /// </para>
    /// <para>
    /// Never having read it is not a conflict - there is nothing this app could be overwriting
    /// unseen - and neither is the file having gone, since putting it back takes nothing away.
    /// </para>
    /// </remarks>
    private bool Moved()
    {
        if (_baseline is not { } seen || !File.Exists(_designsPath))
        {
            return false;
        }

        try
        {
            return !SafeFile.Holds(_designsPath, seen);
        }
        catch (IOException)
        {
            // Held open by whatever is writing it, most likely. Refusing is the safe answer: the
            // one thing that must not happen is writing over a change nobody has seen.
            return true;
        }
    }

    /// <inheritdoc />
    public IDisposable? Watch(Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        var directory = Path.GetDirectoryName(_designsPath);

        // The folder rather than the file: a file that does not exist yet still has to be noticed
        // when it arrives, and a watcher cannot be pointed at something that is not there.
        return directory is { Length: > 0 } && Directory.Exists(directory)
            ? new DesignsWatcher(directory, Path.GetFileName(_designsPath), onChanged)
            : null;
    }

    /// <inheritdoc />
    public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind) =>
        ExportFileAsync(fileName, contents, kind);

    /// <summary>
    /// Writes a file the player names, somewhere they choose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing to do with the designs file, which is why it does not go through
    /// <see cref="SaveAsync(string, byte[])"/> - that one holds the path and replaces what is there,
    /// whatever name it is handed. A selection of empires or a picture of one sent through it
    /// would have overwritten the player's whole collection.
    /// </para>
    /// <para>
    /// Least privilege here as well: the policy allows the one directory the player picked in the
    /// dialog and nothing else, built fresh for this write. Shared with the stand-in used when no
    /// designs file was found, because this has nothing to do with there being one.
    /// </para>
    /// </remarks>
    internal static Task<SaveOutcome> ExportFileAsync(string fileName, byte[] contents, ExportKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contents);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null)
        {
            return Task.FromResult(SaveOutcome.Refused);
        }

        // The dialog belongs to the window, as the clipboard does.
        return dispatcher.InvokeAsync(() =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = kind.Extension,
                Filter = $"{kind.Description}|*{kind.Extension}|All files|*.*",
                AddExtension = true,
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog() is not true)
            {
                return SaveOutcome.Cancelled;
            }

            var chosen = dialog.FileName;
            var policy = WritePolicy.ForApplication()
                .Allowing(Path.GetDirectoryName(chosen)!)
                .Named("application (export)");

            new SafeFile(policy).WriteAllBytes(chosen, contents);

            return SaveOutcome.Saved;
        }).Task;
    }
}
