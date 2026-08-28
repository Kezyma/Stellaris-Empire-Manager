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
            catch (IOException ex)
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
