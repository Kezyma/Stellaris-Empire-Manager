using System.Text;
using Sem.Io;

namespace Sem.Core.Tests.Io;

/// <summary>
/// The print that decides whether the extracted game data is still worth keeping.
/// </summary>
/// <remarks>
/// Getting this wrong is expensive in one direction and quietly wrong in the other: printing
/// differently for an unchanged install re-renders nine thousand portraits for nothing, and
/// printing the same for a changed one leaves the designer offering a version of the game the
/// player is not running.
/// </remarks>
public sealed class InstallFingerprintTests
{
    /// <summary>An installation holding one script file, which the walk is meant to find.</summary>
    private static string Install(TempDirectory temp, string folder, string name, string contents)
    {
        var directory = Path.Combine(temp.Path, folder);

        System.IO.Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), Encoding.UTF8.GetBytes(contents));

        return temp.Path;
    }

    /// <summary>The same files print the same, however many times they are looked at.</summary>
    [Fact]
    public void AnUnchangedInstallPrintsTheSame()
    {
        using var temp = new TempDirectory();
        var root = Install(temp, "common", "defines.txt", "NGameplay = { ETHOS_POINTS = 3 }");

        var first = InstallFingerprint.Of(root);

        Assert.NotNull(first);
        Assert.Equal(first, InstallFingerprint.Of(root));
    }

    /// <summary>Anything written to them prints differently.</summary>
    /// <remarks>
    /// Each of these is a way the game changes without its version string moving: a hotfix, a file
    /// Steam repaired, an added artwork folder, something removed.
    /// </remarks>
    [Fact]
    public void AnythingWrittenToThemPrintsDifferently()
    {
        using var temp = new TempDirectory();
        var root = Install(temp, "common", "defines.txt", "NGameplay = { ETHOS_POINTS = 3 }");
        var before = InstallFingerprint.Of(root);

        // Contents. Written longer rather than merely different, because the same length inside the
        // same timestamp tick is the one edit this cannot see - see the remark on the class, and
        // note that no patch writes that way.
        File.WriteAllBytes(
            Path.Combine(root, "common", "defines.txt"),
            Encoding.UTF8.GetBytes("NGameplay = { ETHOS_POINTS = 4 CIVIC_POINTS = 2 }"));

        var edited = InstallFingerprint.Of(root);
        Assert.NotEqual(before, edited);

        // One more file, in a folder the game's own checksum would not look at.
        System.IO.Directory.CreateDirectory(Path.Combine(root, "gfx"));
        File.WriteAllBytes(Path.Combine(root, "gfx", "portrait.dds"), [1, 2, 3]);

        var added = InstallFingerprint.Of(root);
        Assert.NotEqual(edited, added);

        // And one taken away.
        File.Delete(Path.Combine(root, "gfx", "portrait.dds"));
        Assert.Equal(edited, InstallFingerprint.Of(root));
    }

    /// <summary>
    /// A file touched without being changed prints differently, and that is the safe way round.
    /// </summary>
    /// <remarks>
    /// Written down because it is a real cost: Steam restoring a file it decided was damaged moves
    /// a write time without moving a byte, and buys one rebuild nobody needed. The other way round
    /// is a change nobody notices, which is worse and lasts longer.
    /// </remarks>
    [Fact]
    public void ATouchedFileCountsAsAChange()
    {
        using var temp = new TempDirectory();
        var root = Install(temp, "common", "defines.txt", "NGameplay = { }");
        var before = InstallFingerprint.Of(root);

        File.SetLastWriteTimeUtc(Path.Combine(root, "common", "defines.txt"), DateTime.UtcNow.AddHours(1));

        Assert.NotEqual(before, InstallFingerprint.Of(root));
    }

    /// <summary>Folders nothing is extracted from are not looked at.</summary>
    [Fact]
    public void FoldersNothingIsReadFromAreIgnored()
    {
        using var temp = new TempDirectory();
        var root = Install(temp, "common", "defines.txt", "NGameplay = { }");
        var before = InstallFingerprint.Of(root);

        // Saves, logs, the launcher's own database: all of them churn, none of them is read.
        System.IO.Directory.CreateDirectory(Path.Combine(root, "logs"));
        File.WriteAllText(Path.Combine(root, "logs", "game.log"), "a line");

        Assert.Equal(before, InstallFingerprint.Of(root));
    }

    /// <summary>Layers are one print, and each of them counts.</summary>
    /// <remarks>
    /// There is one layer today. The stack exists so that mods can be added later, and a mod that
    /// changed nothing about the print would be a mod whose files were never extracted.
    /// </remarks>
    [Fact]
    public void EveryLayerCounts()
    {
        using var game = new TempDirectory();
        using var mod = new TempDirectory();

        Install(game, "common", "defines.txt", "NGameplay = { }");

        var alone = InstallFingerprint.Of(game.Path);

        Install(mod, "common", "overrides.txt", "NGameplay = { ETHOS_POINTS = 9 }");

        Assert.NotEqual(alone, InstallFingerprint.Of(game.Path, mod.Path));
    }

    /// <summary>An installation that is not there cannot be said to have changed.</summary>
    [Fact]
    public void NothingToWalkIsNotAChange()
    {
        using var temp = new TempDirectory();

        // No folders of interest at all: a print, and the same one twice.
        var empty = InstallFingerprint.Of(temp.Path);

        Assert.NotNull(empty);
        Assert.Equal(empty, InstallFingerprint.Of(temp.Path));
        Assert.Equal(empty, InstallFingerprint.Of(Path.Combine(temp.Path, "not-there")));
    }
}
