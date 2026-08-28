using System.IO;
using Sem.Io;
using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// Saves the designs file back where it came from.
/// </summary>
/// <remarks>
/// This is the one place in the whole project that writes to the player's real empire designs, and
/// it is careful about it: the new contents are staged beside the file and swapped in, the previous
/// version is kept, and a copy goes to the app's own archive as well. Losing a file full of
/// hand-built empires is the worst thing this app could do.
/// </remarks>
public sealed class DesktopFileExchange(SafeFile file, string designsPath) : IFileExchange
{
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly string _designsPath = designsPath ?? throw new ArgumentNullException(nameof(designsPath));

    /// <inheritdoc />
    public string SaveVerb => "Save";

    /// <summary>The file being edited.</summary>
    public string DesignsPath => _designsPath;

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
    public Task SaveAsync(string fileName, byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        // Kept before anything is replaced, so a save that goes wrong still leaves a way back.
        Archive(contents);

        _file.ReplaceAtomically(_designsPath, contents, DatedBackupPath());
        return Task.CompletedTask;
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
