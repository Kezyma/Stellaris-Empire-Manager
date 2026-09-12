using Sem.Io;

namespace Sem.Core.Tests.Io;

/// <summary>
/// What a replaced file is kept under.
/// </summary>
/// <remarks>
/// The name is the whole behaviour here, and both of the ways it can be wrong are silent: a name
/// that repeats within a session keeps one save out of six, and a name written on the twelve-hour
/// clock loses the morning's copy to the afternoon's without anything going wrong at the time.
/// </remarks>
public sealed class DatedBackupTests
{
    private static readonly string Designs =
        Path.Combine("C:", "saves", "user_empire_designs_v3.4.txt");

    /// <summary>Beside the file, named after it, carrying the moment it was replaced.</summary>
    [Fact]
    public void TheBackupSitsBesideTheFileAndCarriesItsName()
    {
        var backup = SafeFile.DatedBackupPath(Designs, new DateTime(2026, 8, 28, 14, 6, 3));

        Assert.Equal(
            Path.Combine("C:", "saves", "user_empire_designs_v3.4_260828_140603.txt"),
            backup);
    }

    /// <summary>Two saves a second apart keep two backups, not one.</summary>
    /// <remarks>
    /// The date alone named one file per day, and the old rule then left an existing one alone - so
    /// the first save of a day kept a copy and every save after it kept nothing at all.
    /// </remarks>
    [Fact]
    public void TwoSavesInTheSameDayAreTwoBackups()
    {
        var first = SafeFile.DatedBackupPath(Designs, new DateTime(2026, 8, 28, 14, 6, 3));
        var second = SafeFile.DatedBackupPath(Designs, new DateTime(2026, 8, 28, 14, 6, 4));

        Assert.NotEqual(first, second);
    }

    /// <summary>And morning is not afternoon, which is what the 24-hour clock is here for.</summary>
    [Fact]
    public void TheMorningDoesNotCollideWithTheAfternoon()
    {
        var morning = SafeFile.DatedBackupPath(Designs, new DateTime(2026, 8, 28, 9, 30, 0));
        var afternoon = SafeFile.DatedBackupPath(Designs, new DateTime(2026, 8, 28, 21, 30, 0));

        Assert.EndsWith("_260828_093000.txt", morning, StringComparison.Ordinal);
        Assert.EndsWith("_260828_213000.txt", afternoon, StringComparison.Ordinal);
    }

    /// <summary>The suffix is the file's own, rather than assumed to be a designs file.</summary>
    [Fact]
    public void TheBackupKeepsTheOriginalsExtension()
    {
        var backup = SafeFile.DatedBackupPath(
            Path.Combine("C:", "saves", "notes.md"), new DateTime(2026, 1, 2, 3, 4, 5));

        Assert.Equal(Path.Combine("C:", "saves", "notes_260102_030405.md"), backup);
    }

    /// <summary>A name with no folder has nowhere to put one, and says so.</summary>
    [Fact]
    public void ANameWithNoFolderHasNoBackup() =>
        Assert.Null(SafeFile.DatedBackupPath("designs.txt"));
}
