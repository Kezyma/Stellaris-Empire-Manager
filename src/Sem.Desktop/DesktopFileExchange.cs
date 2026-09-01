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

    /// <inheritdoc />
    public bool SavesInPlace => true;

    /// <summary>The file being edited.</summary>
    public string DesignsPath => _designsPath;

    /// <summary>
    /// The published site, which is where a shared link has to point.
    /// </summary>
    /// <remarks>
    /// The web view serves the app from an origin only this process can reach, so a link built from
    /// the window's own address opened nothing anywhere. The same design read from the same link
    /// works on the site, which is the thing another person can actually be sent to.
    /// </remarks>
    public string? ShareBaseUri => "https://kezyma.github.io/Stellaris-Empire-Manager/";

    /// <summary>
    /// Puts text on the Windows clipboard.
    /// </summary>
    /// <remarks>
    /// Without this the share button inherited the interface's default of "did not work" and said
    /// nothing about it, so on the desktop it looked identical to the one that does. The clipboard
    /// belongs to the UI thread and can be held by another process, which is what the retry count
    /// is for; a refusal is reported rather than swallowed.
    /// </remarks>
    public Task<bool> CopyToClipboardAsync(string text)
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
    public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
    {
        if (!File.Exists(_designsPath))
        {
            return Task.FromResult<(string, byte[])?>(null);
        }

        return Task.FromResult<(string, byte[])?>(
            (Path.GetFileName(_designsPath), SafeFile.ReadAllBytes(_designsPath)));
    }

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        // Kept before anything is replaced, so a save that goes wrong still leaves a way back.
        Archive(contents);

        _file.ReplaceAtomically(_designsPath, contents, DatedBackupPath());

        // Always saved here: there is no dialog to dismiss, and a failure throws rather than
        // returning. The other outcomes only arise in a browser, where the player may say no.
        return Task.FromResult(SaveOutcome.Saved);
    }

    /// <summary>
    /// The dated backup beside the file, following the game's own naming so the two sit together.
    /// An existing one for today is left alone, since it may be the game's.
    /// </summary>
    private string? DatedBackupPath()
    {
        var directory = Path.GetDirectoryName(_designsPath);
        if (directory is null)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(_designsPath);
        var backup = Path.Combine(directory, $"{name}_{DateTime.Now:yyMMdd}.txt");

        return File.Exists(backup) ? null : backup;
    }

    /// <summary>Keeps a copy in the app's own folder, where the game will never overwrite it.</summary>
    private void Archive(byte[] contents)
    {
        var archive = Path.Combine(
            WritePolicy.LocalCacheRoot(),
            "archive",
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{Path.GetFileName(_designsPath)}");

        try
        {
            _file.WriteAllBytes(archive, contents);
            Prune(Path.GetDirectoryName(archive)!);
        }
        catch (IOException)
        {
            // An archive that cannot be written must not stop the save the user asked for.
        }
    }

    /// <summary>Keeps the archive to a useful size rather than letting it grow without end.</summary>
    private static void Prune(string directory, int keep = 20)
    {
        try
        {
            foreach (var stale in new DirectoryInfo(directory)
                         .GetFiles("*.txt")
                         .OrderByDescending(f => f.CreationTimeUtc)
                         .Skip(keep))
            {
                stale.Delete();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
