using System.Text;
using Sem.Io;

namespace Sem.Core.Tests.Io;

public sealed class SafeFileTests
{
    [Fact]
    public void WriteAllBytes_RefusesAndCreatesNothingOutsideThePolicy()
    {
        using var allowed = new TempDirectory();
        using var outside = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(allowed.Path));

        var target = outside.Combine("user_empire_designs_v3.4.txt");

        Assert.Throws<ForbiddenWriteException>(() => file.WriteAllBytes(target, "data"u8));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void WriteAllBytes_CreatesMissingParentDirectories()
    {
        using var temp = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path));

        var target = temp.Combine("nested", "deeper", "designs.txt");
        file.WriteAllBytes(target, "payload"u8);

        Assert.Equal("payload", File.ReadAllText(target));
    }

    [Fact]
    public void ReplaceAtomically_ReplacesContentAndKeepsTheOldVersionAsABackup()
    {
        using var temp = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path));

        var target = temp.Combine("designs.txt");
        var backup = temp.Combine("designs_260828.txt");
        File.WriteAllText(target, "original");

        file.ReplaceAtomically(target, "updated"u8, backup);

        Assert.Equal("updated", File.ReadAllText(target));
        Assert.Equal("original", File.ReadAllText(backup));
    }

    [Fact]
    public void ReplaceAtomically_WorksWhenTheTargetDoesNotExistYet()
    {
        using var temp = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path));

        var target = temp.Combine("new-designs.txt");
        file.ReplaceAtomically(target, "fresh"u8);

        Assert.Equal("fresh", File.ReadAllText(target));
    }

    [Fact]
    public void ReplaceAtomically_LeavesNoStagingFilesBehind()
    {
        using var temp = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path));

        var target = temp.Combine("designs.txt");
        file.ReplaceAtomically(target, "one"u8);
        file.ReplaceAtomically(target, "two"u8);

        var leftovers = Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories);
        Assert.Empty(leftovers);
    }

    [Fact]
    public void ReplaceAtomically_RefusesWhenOnlyTheBackupPathIsOutsideThePolicy()
    {
        using var allowed = new TempDirectory();
        using var outside = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(allowed.Path));

        var target = allowed.Combine("designs.txt");
        File.WriteAllText(target, "original");

        Assert.Throws<ForbiddenWriteException>(
            () => file.ReplaceAtomically(target, "updated"u8, outside.Combine("backup.txt")));

        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Copy_ChecksTheDestinationButReadsAnySource()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();
        var file = new SafeFile(WritePolicy.DenyAll.Allowing(destination.Path));

        // The source sits outside the policy: reads are never restricted, only writes.
        var sourceFile = source.Combine("original.txt");
        File.WriteAllText(sourceFile, "copied");

        file.Copy(sourceFile, destination.Combine("copy.txt"));

        Assert.Equal("copied", File.ReadAllText(destination.Combine("copy.txt")));
    }

    [Fact]
    public void OpenRead_DoesNotLockTheFileAgainstOtherWriters()
    {
        using var temp = new TempDirectory();
        var target = temp.Combine("shared.txt");
        File.WriteAllText(target, "content");

        using var reader = SafeFile.OpenRead(target);

        // The game and OneDrive both write these files while we hold them open.
        using var concurrentWriter = new FileStream(
            target, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        Assert.True(concurrentWriter.CanWrite);
    }

    [Fact]
    public void ReadAllBytes_RoundTripsExactBytesIncludingCrlfAndNoBom()
    {
        using var temp = new TempDirectory();
        var target = temp.Combine("designs.txt");

        // Mirrors the real designs file: no BOM, CRLF, tab indentation.
        var expected = Encoding.UTF8.GetBytes("\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n}\r\n");
        File.WriteAllBytes(target, expected);

        Assert.Equal(expected, SafeFile.ReadAllBytes(target));
    }
}
