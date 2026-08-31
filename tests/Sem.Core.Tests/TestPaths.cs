using Sem.Io;

namespace Sem.Core.Tests;

/// <summary>
/// Locates the corpora tests read from: the sandbox copies made by <c>devsync</c>, and the real
/// installation for the wider sweep. Everything here is read-only.
/// </summary>
public static class TestPaths
{
    /// <summary>The repository sandbox, or null when the tests are not running inside the repo.</summary>
    public static SandboxLayout? Sandbox { get; } = TryDiscoverSandbox();

    /// <summary>The real Stellaris installation, or null when it is not present.</summary>
    public static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    /// <summary>
    /// The player's current empire designs, as copied into the sandbox. Never the originals.
    /// </summary>
    /// <remarks>
    /// The one live file, not the dated backups beside it. Those hold empires from earlier versions
    /// of the game and earlier ideas of the player's, and a corpus test that swept them in was
    /// measuring against ninety-two empires the player no longer has.
    /// </remarks>
    public static IReadOnlyList<string> SandboxDesignFiles =>
        EnumerateSandboxFiles("userdata", "user_empire_designs_v3.4.txt");

    /// <summary>Copies of the built-in prescripted empire files. Empty when absent.</summary>
    public static IReadOnlyList<string> SandboxPrescriptedFiles =>
        EnumerateSandboxFiles(Path.Combine("gamefiles", "prescripted_countries"), "*.txt");

    /// <summary>Explains how to populate the sandbox, for skip messages.</summary>
    public const string SandboxMissingMessage =
        "Sandbox copies are missing. Run: dotnet run --project src/Sem.Cli -- devsync";

    private static SandboxLayout? TryDiscoverSandbox()
    {
        try
        {
            return SandboxLayout.Discover(AppContext.BaseDirectory);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> EnumerateSandboxFiles(string relativeDirectory, string pattern)
    {
        if (Sandbox is null)
        {
            return [];
        }

        var directory = Path.Combine(Sandbox.Root, relativeDirectory);

        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)]
            : [];
    }
}
