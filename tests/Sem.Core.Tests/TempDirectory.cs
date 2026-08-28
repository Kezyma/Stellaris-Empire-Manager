namespace Sem.Core.Tests;

/// <summary>A throwaway directory that deletes itself at the end of a test.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path to the directory.</summary>
    public string Path { get; }

    /// <summary>Combines a relative path onto this directory.</summary>
    public string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Leftover temp files are not worth failing a test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
