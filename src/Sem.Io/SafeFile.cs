namespace Sem.Io;

/// <summary>
/// The only file-writing surface in the solution. Every write is checked against a
/// <see cref="WritePolicy"/> first, and destructive replacements go through a temp file so an
/// interrupted save cannot leave a half-written empire designs file behind.
/// </summary>
/// <remarks>
/// Reads are deliberately unguarded but always share-friendly: the game, OneDrive and antivirus
/// may hold the same files open, and this process must never block them or fail because of them.
/// </remarks>
public sealed class SafeFile(WritePolicy policy)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The policy consulted before every write.</summary>
    public WritePolicy Policy { get; } = policy ?? throw new ArgumentNullException(nameof(policy));

    /// <summary>Opens a file for reading without preventing other processes from using it.</summary>
    public static FileStream OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>Reads a whole file without preventing other processes from using it.</summary>
    public static byte[] ReadAllBytes(string path)
    {
        using var stream = OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Creates a directory, after checking the policy permits writing there.</summary>
    public void CreateDirectory(string path)
    {
        Policy.EnsureWritable(path);
        Directory.CreateDirectory(path);
    }

    /// <summary>Writes a file outright, creating the parent directory if needed.</summary>
    public void WriteAllBytes(string path, ReadOnlySpan<byte> content)
    {
        Policy.EnsureWritable(path);
        EnsureParentDirectory(path);

        var bytes = content.ToArray();
        Retry(() => File.WriteAllBytes(path, bytes), path);
    }

    /// <summary>Copies a file. Only the destination is policy-checked; the source is read-only.</summary>
    public void Copy(string sourcePath, string destinationPath, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        Policy.EnsureWritable(destinationPath);
        EnsureParentDirectory(destinationPath);

        Retry(
            () =>
            {
                using var source = OpenRead(sourcePath);
                using var destination = new FileStream(
                    destinationPath,
                    overwrite ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                source.CopyTo(destination);
            },
            destinationPath);
    }

    /// <summary>
    /// Where to keep what a replacement writes over: beside the file, under its own name and the
    /// moment it was replaced.
    /// </summary>
    /// <param name="path">The file about to be replaced.</param>
    /// <param name="moment">When, for a caller that needs to say; otherwise now.</param>
    /// <returns>A path to hand <see cref="ReplaceAtomically"/>, or null if there is no folder.</returns>
    /// <remarks>
    /// <para>
    /// Beside the file and named after it, so the two sort together in a folder the player already
    /// has reason to look in, and so the name says at a glance what it is a copy of. The game names
    /// its own backups this way.
    /// </para>
    /// <para>
    /// Timed as well as dated, which is the part that had to be learnt. A name carrying only the
    /// date is one file per day: a session that saves six times either keeps the first state and
    /// refuses the rest or keeps overwriting until only the last survives, and the one that is
    /// wanted back is almost always one of the four in between.
    /// </para>
    /// <para>
    /// HH rather than hh, which is the twelve-hour clock. It collides twice a day and does it in
    /// silence, taking the morning's copy away at the same minute of the afternoon - the one thing
    /// a timed backup exists to prevent.
    /// </para>
    /// </remarks>
    public static string? DatedBackupPath(string path, DateTime? moment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Path.GetDirectoryName(path) is not { Length: > 0 } directory)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        return Path.Combine(directory, $"{name}_{moment ?? DateTime.Now:yyMMdd_HHmmss}{extension}");
    }

    /// <summary>
    /// Replaces a file's contents as close to atomically as Windows allows: the new content is
    /// staged next to the target, then swapped in. If <paramref name="backupPath"/> is given, the
    /// previous contents are preserved there.
    /// </summary>
    public void ReplaceAtomically(string path, ReadOnlySpan<byte> content, string? backupPath = null)
    {
        Policy.EnsureWritable(path);
        if (backupPath is not null)
        {
            Policy.EnsureWritable(backupPath);
        }

        EnsureParentDirectory(path);

        // Stage in the target's own directory so the swap stays on one volume.
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var staged = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllBytes(staged, content.ToArray());

            Retry(
                () =>
                {
                    if (File.Exists(path))
                    {
                        File.Replace(staged, path, backupPath, ignoreMetadataErrors: true);
                    }
                    else
                    {
                        if (backupPath is not null && File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        File.Move(staged, path);
                    }
                },
                path);
        }
        finally
        {
            TryDelete(staged);
        }
    }

    private void EnsureParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            Policy.EnsureWritable(parent);
            Directory.CreateDirectory(parent);
        }
    }

    /// <summary>
    /// Retries transient sharing violations. OneDrive, the game itself and antivirus scanners all
    /// hold brief locks on these files, and a first-attempt failure is routine rather than fatal.
    /// </summary>
    private static void Retry(Action action, string path)
    {
        var delay = InitialRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(delay);
                delay *= 2;
            }
            // Both kinds are retried above, and only one of them was wrapped on the way out - a
            // final UnauthorizedAccessException escaped raw, with none of the explanation and none
            // of the reassurance that the original file is intact.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"Failed to write '{path}' after {MaxAttempts} attempts. It may be open in another " +
                    "program, or still syncing. The original file has not been modified.",
                    ex);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover staging file is harmless; failing the save over it would not be.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
