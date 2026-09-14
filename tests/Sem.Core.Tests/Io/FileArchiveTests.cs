using Sem.Io;

namespace Sem.Core.Tests.Io;

/// <summary>
/// The copies kept of what a save wrote over.
/// </summary>
/// <remarks>
/// <para>
/// The first test here is the one that matters, and it is the one that did not exist. The archive
/// kept a copy of the bytes being <em>written</em> rather than the bytes being <em>replaced</em> -
/// a record of what each save created, which is no way back from any of them. It held them for as
/// long as it did because the class that called it lives in a WPF assembly no test project can
/// reference, so moving the archive here was most of the fix.
/// </para>
/// <para>
/// The rest of the fix is that the archive reads the file itself. A method handed its bytes is only
/// ever as right as its caller, and a test of one would have passed throughout - which is the trap
/// this class is built to stay out of.
/// </para>
/// <para>
/// It matters most where it is the only copy. A save keeps a dated sibling beside the designs file
/// as well, except while the file is being kept in step - saving then happens several times a
/// minute and one dated file per save would bury the folder the game keeps its saves in. So with
/// that switched on, this archive is the whole of the way back.
/// </para>
/// </remarks>
public sealed class FileArchiveTests
{
    private const string Designs = "user_empire_designs_v3.4.txt";

    private static (FileArchive Archive, string Root) In(TempDirectory temp)
    {
        var root = temp.Combine("archive");

        return (new FileArchive(new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path)), root), root);
    }

    private static string[] Kept(string root) =>
        Directory.Exists(root) ? [.. Directory.GetFiles(root).Order(StringComparer.Ordinal)] : [];

    /// <summary>
    /// What is kept is what was there, and the save that follows does not change that.
    /// </summary>
    /// <remarks>
    /// Written as the sequence it guards rather than as a call and an assertion: the game writes the
    /// file, the app keeps a copy, the app replaces it. What must survive is the game's.
    /// </remarks>
    [Fact]
    public void TheCopyHoldsWhatWasReplacedRatherThanWhatReplacedIt()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);
        var designs = temp.Combine(Designs);

        File.WriteAllText(designs, "the empires the game wrote");

        archive.KeepReplaced(designs);
        File.WriteAllText(designs, "what the app saved over them");

        var kept = Assert.Single(Kept(root));
        Assert.Equal("the empires the game wrote", File.ReadAllText(kept));
    }

    /// <summary>And it is named for the file it is a copy of, and for when.</summary>
    [Fact]
    public void TheCopyIsNamedForTheFileAndTheMoment()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);
        var designs = temp.Combine(Designs);

        File.WriteAllText(designs, "held");
        archive.KeepReplaced(designs, new DateTime(2026, 9, 14, 1, 30, 5));

        Assert.Equal(
            $"20260914-013005-{Designs}",
            Path.GetFileName(Assert.Single(Kept(root))));
    }

    /// <summary>A first save, with nothing there yet, keeps nothing and says nothing.</summary>
    [Fact]
    public void ThereIsNothingToKeepBeforeTheFileExists()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);

        archive.KeepReplaced(temp.Combine(Designs));

        Assert.Empty(Kept(root));
    }

    /// <summary>Twenty of them, and the oldest goes rather than the newest.</summary>
    /// <remarks>
    /// By name rather than by creation time, because Windows hands back the creation time of a file
    /// that used to have the same name - so a folder churning through copies would prune by a clock
    /// that is not moving. The name carries the moment and sorts.
    /// </remarks>
    [Fact]
    public void OnlyTheNewestTwentyAreKept()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);
        var designs = temp.Combine(Designs);

        var start = new DateTime(2026, 9, 14, 0, 0, 0);

        for (var save = 0; save < FileArchive.Keeps + 5; save++)
        {
            File.WriteAllText(designs, $"save {save}");
            archive.KeepReplaced(designs, start.AddSeconds(save));
        }

        var kept = Kept(root);

        Assert.Equal(FileArchive.Keeps, kept.Length);

        // The five oldest went; the newest is still here.
        Assert.Equal($"save {FileArchive.Keeps + 4}", File.ReadAllText(kept[^1]));
        Assert.Equal("save 5", File.ReadAllText(kept[0]));
    }

    /// <summary>One crowded file does not push out the copies of another.</summary>
    [Fact]
    public void PruningCountsEachFileSeparately()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);
        var start = new DateTime(2026, 9, 14, 0, 0, 0);

        var notes = temp.Combine("notes.txt");
        File.WriteAllText(notes, "the only copy of this one");
        archive.KeepReplaced(notes, start);

        var designs = temp.Combine(Designs);
        File.WriteAllText(designs, "designs");

        for (var save = 0; save < FileArchive.Keeps + 5; save++)
        {
            archive.KeepReplaced(designs, start.AddSeconds(save + 1));
        }

        Assert.Equal(FileArchive.Keeps + 1, Kept(root).Length);
        Assert.Contains(Kept(root), kept => File.ReadAllText(kept) == "the only copy of this one");
    }

    /// <summary>
    /// An archive that cannot be written is not a save that cannot happen.
    /// </summary>
    /// <remarks>
    /// It is a courtesy the player did not ask for and cannot see. Letting it throw would fail the
    /// save with an error naming a folder in application data that has nothing to do with their
    /// empires - which is what a policy refusal, or a corporate rule on LocalAppData, used to do.
    /// </remarks>
    [Fact]
    public void AnArchiveThatCannotBeWrittenSaysNothingAndThrowsNothing()
    {
        using var temp = new TempDirectory();
        using var elsewhere = new TempDirectory();

        // A policy that allows the designs folder and nothing else, which is what an archive root
        // somewhere the machine will not permit amounts to.
        var archive = new FileArchive(
            new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path)),
            elsewhere.Combine("archive"));

        var designs = temp.Combine(Designs);
        File.WriteAllText(designs, "held");

        archive.KeepReplaced(designs);

        Assert.Empty(Kept(elsewhere.Combine("archive")));
    }

    /// <summary>A folder that is not there yet is made on the way.</summary>
    [Fact]
    public void TheFolderIsMadeOnTheFirstCopy()
    {
        using var temp = new TempDirectory();
        var (archive, root) = In(temp);
        var designs = temp.Combine(Designs);

        File.WriteAllText(designs, "held");
        Assert.False(Directory.Exists(root));

        archive.KeepReplaced(designs);

        Assert.True(Directory.Exists(root));
    }
}
