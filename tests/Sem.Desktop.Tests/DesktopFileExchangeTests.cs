using Sem.Core.Tests;
using Sem.Io;
using Sem.Ui.Services;

namespace Sem.Desktop.Tests;

/// <summary>
/// The host that writes the player's real designs file.
/// </summary>
/// <remarks>
/// <para>
/// The promise is that nothing is written over a change this app has not seen, and until now
/// nothing tested it. That is not an oversight anybody could have corrected from the old test
/// projects - this class lives in a WPF assembly a net10.0 test assembly cannot reference, which is
/// exactly how its archive came to be keeping a copy of the wrong side of every save.
/// </para>
/// <para>
/// Everything here works inside a temp directory, against a policy that allows that directory and
/// nothing else. The real designs file is never touched and could not be: the policy would refuse.
/// </para>
/// </remarks>
public sealed class DesktopFileExchangeTests
{
    private const string Name = "user_empire_designs_v3.4.txt";

    private static (DesktopFileExchange Files, string Path) In(TempDirectory temp)
    {
        var path = temp.Combine(Name);

        return (new DesktopFileExchange(new SafeFile(WritePolicy.DenyAll.Allowing(temp.Path)), path), path);
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    /// <summary>An ordinary save writes the file and says so.</summary>
    [Fact]
    public async Task ASaveOverAnUntouchedFileLands()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "what was there");
        await files.TryOpenExistingAsync();

        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync(Name, Bytes("what the app wrote")));
        Assert.Equal("what the app wrote", File.ReadAllText(path));
    }

    /// <summary>
    /// A save over something else's change is refused, and the file is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// The whole promise in one test. The ordinary way to reach it is to edit here, quit Stellaris -
    /// which rewrites this file as it exits - and press Save, which before any of this existed put
    /// the editor's copy straight over what the game had just written, without a word.
    /// </remarks>
    [Fact]
    public async Task ASaveOverSomebodyElsesChangeIsRefused()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "what the app last saw");
        await files.TryOpenExistingAsync();

        File.WriteAllText(path, "what the game wrote after that");

        Assert.Equal(SaveOutcome.Conflicted, await files.SaveAsync(Name, Bytes("the editor's copy")));
        Assert.Equal("what the game wrote after that", File.ReadAllText(path));
    }

    /// <summary>
    /// Reading it again is what lifts the refusal, which is what the conflict question relies on.
    /// </summary>
    /// <remarks>
    /// <c>DesignSync.ReconcileAsync</c> answers a refused save by reading the file and asking what
    /// to do about it, and the write that follows the answer only works because the read moved the
    /// baseline. Nothing pinned that until now, and it is not obvious from either side on its own.
    /// </remarks>
    [Fact]
    public async Task ReadingItAgainAllowsTheWriteThatWasRefused()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "first");
        await files.TryOpenExistingAsync();
        File.WriteAllText(path, "somebody else");

        Assert.Equal(SaveOutcome.Conflicted, await files.SaveAsync(Name, Bytes("mine")));

        // Which is what the reconcile does before it asks.
        await files.TryOpenExistingAsync();

        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync(Name, Bytes("mine")));
        Assert.Equal("mine", File.ReadAllText(path));
    }

    /// <summary>Never having looked at the file is not a conflict.</summary>
    /// <remarks>
    /// There is nothing this app could be overwriting unseen, so refusing would only make a first
    /// save impossible.
    /// </remarks>
    [Fact]
    public async Task NeverHavingReadItIsNotAConflict()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "something that was already here");

        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync(Name, Bytes("mine")));
    }

    /// <summary>And neither is the file having gone, since putting it back takes nothing away.</summary>
    [Fact]
    public async Task AMissingFileIsNotAConflictEither()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "here for now");
        await files.TryOpenExistingAsync();
        File.Delete(path);

        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync(Name, Bytes("back again")));
        Assert.Equal("back again", File.ReadAllText(path));
    }

    /// <summary>Reading a file that is not there says so rather than throwing.</summary>
    [Fact]
    public async Task ReadingAFileThatIsNotThereAnswersNothing()
    {
        using var temp = new TempDirectory();
        var (files, _) = In(temp);

        Assert.Null(await files.TryOpenExistingAsync());
    }

    /// <summary>A save keeps a dated copy beside the file, under the file's own name.</summary>
    [Fact]
    public async Task ASaveKeepsADatedCopyBesideTheFile()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "the previous version");
        await files.TryOpenExistingAsync();

        await files.SaveAsync(Name, Bytes("the new one"), backUp: true);

        var beside = Directory
            .GetFiles(temp.Path, "user_empire_designs_v3.4_*.txt")
            .Select(File.ReadAllText)
            .ToList();

        Assert.Contains("the previous version", beside);
    }

    /// <summary>
    /// And keeps none when the file is being written as you go.
    /// </summary>
    /// <remarks>
    /// Saving then happens several times a minute and one dated file per save would bury the folder
    /// the game keeps its saves in. This is the arrangement that made the archive matter: with the
    /// sibling off, the app's own copy is the only way back.
    /// </remarks>
    [Fact]
    public async Task WritingAsYouGoKeepsNoDatedCopy()
    {
        using var temp = new TempDirectory();
        var (files, path) = In(temp);

        File.WriteAllText(path, "the previous version");
        await files.TryOpenExistingAsync();

        await files.SaveAsync(Name, Bytes("the new one"), backUp: false);

        Assert.Empty(Directory.GetFiles(temp.Path, "user_empire_designs_v3.4_*.txt"));
    }

    /// <summary>The header calls it Save here, and means the player's own file.</summary>
    [Fact]
    public void ItSavesInPlaceAndSaysSo()
    {
        using var temp = new TempDirectory();
        var (files, _) = In(temp);

        Assert.True(files.SavesInPlace);
        Assert.Equal("Save", files.SaveVerb);
    }

    /// <summary>
    /// A shared link points at the published site rather than at this window.
    /// </summary>
    /// <remarks>
    /// The web view serves the app from an origin only this process can reach, so a link built from
    /// the window's own address opened nothing anywhere.
    /// </remarks>
    [Fact]
    public void ASharedLinkPointsAtTheSite()
    {
        using var temp = new TempDirectory();
        var (files, _) = In(temp);

        Assert.Equal("https://kezyma.github.io/Stellaris-Empire-Manager/", files.ShareBaseUri);
    }

    /// <summary>
    /// With no window to put a dialog on, exporting refuses rather than pretending.
    /// </summary>
    /// <remarks>
    /// Both of the calls that need the window - the export dialog and the clipboard - answer a
    /// refusal when there is no dispatcher, and a test runs without one. Worth pinning because a
    /// silent false here is what once made the share button look broken on the desktop while
    /// behaving perfectly in a tab.
    /// </remarks>
    [Fact]
    public async Task WithNoWindowTheExportRefusesRatherThanClaimingSuccess()
    {
        using var temp = new TempDirectory();
        var (files, _) = In(temp);

        Assert.Equal(
            SaveOutcome.Refused,
            await files.ExportAsync("empire.png", Bytes("not really a png"), ExportKind.Image));

        Assert.False(await files.CopyToClipboardAsync("anything"));
    }

    /// <summary>Whether the window should ask before closing is the editor's answer, kept here.</summary>
    [Fact]
    public async Task UnsavedWorkIsRememberedForTheWindowToAsk()
    {
        using var temp = new TempDirectory();
        var (files, _) = In(temp);

        Assert.False(files.HasUnsavedWork);

        await files.WarnBeforeLeavingAsync(true);
        Assert.True(files.HasUnsavedWork);

        await files.WarnBeforeLeavingAsync(false);
        Assert.False(files.HasUnsavedWork);
    }
}
