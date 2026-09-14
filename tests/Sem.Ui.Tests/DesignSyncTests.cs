using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Keeping the app and the player's designs file in step with one another.
/// </summary>
/// <remarks>
/// Everything here is a way the arrangement can eat somebody's work, which is why it is worth the
/// tests. A write that is read straight back in undoes the edit that caused it; a file loaded over
/// an empire being edited takes that edit with it; and a save that keeps a dated copy several times
/// a minute fills the folder the game keeps its saves in.
/// </remarks>
public sealed class DesignSyncTests
{
    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        },
        new Dictionary<string, string>(),
        "assets");

    private sealed class Source : IGameDataSource
    {
        public Task<Sem.Ui.Services.GameData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Data());

        public Task<IReadOnlyList<PortraitOutfit>> LoadWardrobeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortraitOutfit>>([]);

    /// <summary>
    /// No wiki data here. The wiki fetches its own files and nothing in these tests opens a page
    /// that wants one, so answering "this host publishes none" is the whole of what is needed.
    /// </summary>
    /// <typeparam name="TPack">What the file would hold.</typeparam>
    /// <param name="domain">Which file.</param>
    /// <param name="shape">How to read it.</param>
    /// <param name="cancellationToken">Abandons the fetch.</param>
    /// <returns>Nothing.</returns>
    public Task<TPack?> LoadWikiPackAsync<TPack>(
        string domain,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TPack> shape,
        CancellationToken cancellationToken = default)
        where TPack : class, IWikiPack => Task.FromResult<TPack?>(null);

    }

    /// <summary>A desktop: one file, in place, which anything else may also write.</summary>
    private sealed class Disk : IFileExchange
    {
        public Disk(params string[] empires)
        {
            var file = EmpireDesignsFile.CreateEmpty();

            foreach (var name in empires)
            {
                file.Add(name);
            }

            Contents = file.Save();
        }

        public byte[] Contents { get; private set; }

        /// <summary>
        /// What the file held when this app last read or wrote it, as the real host keeps it.
        /// </summary>
        /// <remarks>
        /// Here rather than left out, because the refusal it produces is the thing several of
        /// these tests are about. A double that always says yes cannot tell a sync that handles a
        /// refusal from one that has never met one.
        /// </remarks>
        private byte[]? _baseline;

        public int Writes { get; private set; }

        public int Reads { get; private set; }

        public bool? LastBackUp { get; private set; }

        public bool Watching { get; private set; }

        /// <summary>Whether reading throws, the way a file something else is writing does.</summary>
        public bool Unreadable { get; set; }

        public bool SavesInPlace => true;

        /// <summary>Writes given bytes the way something outside this app would.</summary>
        /// <remarks>
        /// For the case where the file moved and came to hold what is already open. Built from the
        /// session rather than from names, because what that case turns on is the bytes matching
        /// exactly and names only nearly guarantee it.
        /// </remarks>
        public void WrittenElsewhere(byte[] contents) => Contents = contents;

        /// <summary>Writes the file the way something outside this app would.</summary>
        public void WrittenElsewhere(params string[] empires)
        {
            var file = EmpireDesignsFile.CreateEmpty();

            foreach (var name in empires)
            {
                file.Add(name);
            }

            Contents = file.Save();
        }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            SaveAsync(fileName, contents, backUp: true);

        /// <summary>Something to do in the middle of a write, so the busy window can be tested.</summary>
        public Action? DuringSave { get; set; }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
        {
            // Nothing is written over a change this app has not seen, which is what the desktop
            // promises and what makes a conflict reachable from a test at all.
            if (_baseline is { } seen && !seen.AsSpan().SequenceEqual(Contents))
            {
                return Task.FromResult(SaveOutcome.Conflicted);
            }

            Contents = contents;
            _baseline = contents;
            LastBackUp = backUp;
            Writes++;

            var interrupt = DuringSave;
            DuringSave = null;
            interrupt?.Invoke();

            return Task.FromResult(SaveOutcome.Saved);
        }

        /// <summary>Something to do in the middle of a read, so the busy window can be tested.</summary>
        public Action? DuringRead { get; set; }

        public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
        {
            Reads++;

            var interrupt = DuringRead;
            DuringRead = null;
            interrupt?.Invoke();

            if (Unreadable)
            {
                throw new IOException("held open by something else");
            }

            _baseline = Contents;

            return Task.FromResult<(string, byte[])?>((EmpireDesignsFile.FileName, Contents));
        }

        public IDisposable? Watch(Action onChanged)
        {
            Watching = true;

            return new Stop(this);
        }

        private sealed class Stop(Disk disk) : IDisposable
        {
            public void Dispose() => disk.Watching = false;
        }
    }

    private static async Task<(DesignSync Sync, SessionHost Host, DesignSession Session)> OpenAsync(Disk disk)
    {
        var preferences = new Preferences();
        var host = new SessionHost(new Source(), disk, store: null, assumeAllPacks: true, preferences);
        var session = await host.GetAsync() ?? throw new InvalidOperationException("no session");
        var sync = new DesignSync(host, disk, preferences);

        await sync.AttachAsync(session);

        return (sync, host, session);
    }

    private static IReadOnlyList<string> Names(EmpireDesignsFile file) =>
        [.. file.Designs.Select(d => d.Key)];

    private static IReadOnlyList<string> Names(DesignSession session) => Names(session.File!);

    /// <summary>A browser has no file of its own, so it is never offered the choice.</summary>
    [Fact]
    public async Task AHostWithNoFileOfItsOwnDoesNotOfferThis()
    {
        var preferences = new Preferences();
        var browser = new NoFile();
        var host = new SessionHost(new Source(), browser, store: null, assumeAllPacks: true, preferences);

        await host.GetAsync();

        var sync = new DesignSync(host, browser, preferences);

        Assert.False(sync.Available);

        await sync.SetAsync(true);

        Assert.False(sync.Enabled);
    }

    private sealed class NoFile : IFileExchange
    {
        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            Task.FromResult(SaveOutcome.Saved);
    }

    /// <summary>Off, the list is the app's own business until somebody presses Save.</summary>
    [Fact]
    public async Task NothingIsWrittenWhileItIsOff()
    {
        var disk = new Disk("First");
        var (_, _, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Second"));

        Assert.Equal(0, disk.Writes);
    }

    /// <summary>On, the list changing is enough.</summary>
    [Fact]
    public async Task AddingAnEmpireWritesTheFile()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.EditFile(file => file.Add("Second"));

        Assert.Equal(["First", "Second"], Names(EmpireDesignsFile.Load(disk.Contents)));
        Assert.True(disk.Watching);
    }

    /// <summary>And no dated copy for any of them, several times a minute.</summary>
    [Fact]
    public async Task SyncingKeepsNoDatedBackups()
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.EditFile(file => file.Add("Second"));

        Assert.False(disk.LastBackUp);

        // Including the save the editor asks for itself, which is the same file being written.
        await host.SaveAsync();

        Assert.False(disk.LastBackUp);
    }

    /// <summary>
    /// The app's own writing comes back as a change like anybody else's, and is not taken.
    /// </summary>
    /// <remarks>
    /// Reading it back in would be harmless only if nothing had happened since. It never is: the
    /// write is reported late, and by then there is usually an edit in hand that the reload would
    /// throw away.
    /// </remarks>
    [Fact]
    public async Task TheAppsOwnWritingIsNotReadBackIn()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.EditFile(file => file.Add("Second"));

        var held = session.File!.Designs[0];

        await sync.RefreshAsync();

        // The same objects, so nothing was opened over the top of them.
        Assert.Same(held, session.File!.Designs[0]);
        Assert.Equal(["First", "Second"], Names(session));
    }

    /// <summary>
    /// A change made while a write is already going out is not lost.
    /// </summary>
    /// <remarks>
    /// Writing and reading both hold a flag that stops this answering its own announcements, and
    /// anything arriving while it is held used to be dropped on the floor. Nothing said so: the app
    /// showed the empire and the file did not have it, and it stayed that way until something else
    /// happened to be written. The window is milliseconds for a write and most of a second for a
    /// read that finds the file half written, which is the very moment the game is saving.
    /// </remarks>
    [Fact]
    public async Task AChangeMadeDuringAWriteStillReachesTheFile()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);

        disk.DuringSave = () => session.EditFile(file => file.Add("Third"));
        session.EditFile(file => file.Add("Second"));

        Assert.Equal(["First", "Second", "Third"], Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>A change made in the game arrives without being asked for.</summary>
    [Fact]
    public async Task AChangeMadeOutsideIsLoaded()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        disk.WrittenElsewhere("First", "Built in the game");

        await sync.RefreshAsync();

        Assert.Equal(["First", "Built in the game"], Names(session));
    }

    /// <summary>
    /// Unless it would take away an edit nobody has saved, which is asked about instead.
    /// </summary>
    [Fact]
    public async Task AnUnsavedEditIsAskedAboutRatherThanReplaced()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("Built in the game");
        await sync.RefreshAsync();

        Assert.True(sync.Asking);
        Assert.Equal(["First"], Names(session));
        Assert.Equal("Renamed, not saved", session.Current!.Name.Key);
    }

    /// <summary>Keeping them leaves the app as it was, and stops asking.</summary>
    [Fact]
    public async Task DecliningKeepsTheEdit()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("Built in the game");
        await sync.RefreshAsync();
        await sync.Decline();

        Assert.False(sync.Asking);
        Assert.Equal(["First"], Names(session));
        Assert.Equal("Renamed, not saved", session.Current!.Name.Key);

        // Keeping the edit means the file is now the thing being written over, so it is written:
        // the two disagreed, and the sync is what settles a disagreement in the app's favour.
        Assert.Equal(["First"], Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>
    /// Merging keeps both sides, and puts the result back where the two disagreed.
    /// </summary>
    /// <remarks>
    /// The answer that did not exist here. A file written by the game while somebody was editing
    /// left two choices, both of which threw one side's work away - and the game writing that file
    /// as it exits is the commonest way this question ever gets asked.
    /// </remarks>
    [Theory]
    [InlineData(Arrival.TheirsWin)]
    [InlineData(Arrival.MineWin)]
    public async Task MergingKeepsBothSidesOfAChangeUnderneath(Arrival answer)
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("First", "Built in the game");
        await sync.RefreshAsync();

        Assert.True(sync.Asking);

        await sync.ResolveAsync(answer);

        Assert.False(sync.Asking);
        Assert.Equal(["First", "Built in the game"], Names(session));

        // And the file holds what was decided, rather than either half of it.
        Assert.Equal(
            ["First", "Built in the game"],
            Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>
    /// Whatever the answer, the same change is not asked about twice.
    /// </summary>
    /// <remarks>
    /// Every answer moves the baseline to what the file held when it was asked. Without that, the
    /// next look at an unchanged file finds it still differing from a stale baseline and asks
    /// again - which is a dialog somebody cannot get rid of by answering it.
    /// </remarks>
    [Theory]
    [InlineData(Arrival.TheirsWin)]
    [InlineData(Arrival.MineWin)]
    [InlineData(Arrival.KeepMine)]
    [InlineData(Arrival.TakeTheirs)]
    public async Task AnAnsweredChangeIsNotAskedAboutAgain(Arrival answer)
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("First", "Built in the game");
        await sync.RefreshAsync();
        await sync.ResolveAsync(answer);

        Assert.False(sync.Asking);

        await sync.RefreshAsync();

        Assert.False(sync.Asking);
    }

    /// <summary>And taking the file is the other answer, edit and all.</summary>
    [Fact]
    public async Task AcceptingTakesTheFile()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("Built in the game");
        await sync.RefreshAsync();
        await sync.AcceptAsync();

        Assert.False(sync.Asking);
        Assert.Equal(["Built in the game"], Names(session));
    }

    /// <summary>
    /// Turning it on with work in hand writes that work out rather than reading over it.
    /// </summary>
    [Fact]
    public async Task TurningItOnWithWorkInHandWritesIt()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Added while it was off"));

        await sync.SetAsync(true);

        Assert.False(sync.Asking);
        Assert.Equal(["First", "Added while it was off"], Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>
    /// And where the file moved as well, it asks rather than picking a side.
    /// </summary>
    /// <remarks>
    /// Both sides hold something the other has not seen, so there is no answer that is not a
    /// choice. The host refuses the write for exactly that reason - it will not go over a change
    /// nobody has looked at - and this used to be a double that said yes to everything, so the
    /// test asserted the loss it was written to prevent.
    /// </remarks>
    [Fact]
    public async Task TurningItOnWhenBothSidesHaveMovedAsks()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Added while it was off"));
        disk.WrittenElsewhere("Something else entirely");

        await sync.SetAsync(true);

        Assert.True(sync.Asking);
        Assert.Equal(["Something else entirely"], Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>And with nothing in hand, takes whatever the file has come to hold.</summary>
    [Fact]
    public async Task TurningItOnWithNothingInHandTakesTheFile()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        disk.WrittenElsewhere("First", "Built in the game while this was shut");

        await sync.SetAsync(true);

        Assert.Equal(["First", "Built in the game while this was shut"], Names(session));
    }

    /// <summary>
    /// A save refused because the file moved reads it and asks, with writing as you go switched off.
    /// </summary>
    /// <remarks>
    /// The case the old sentence could not be followed. It said to reload the file, and the reload
    /// it meant is the one this class does only when it is switched on - so with it off there was
    /// no button anywhere that did what the message asked for, and pressing Save again only asked
    /// the same refusal a second time.
    /// </remarks>
    [Fact]
    public async Task ARefusedSaveAsksEvenWithWritingAsYouGoOff()
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Mine"));
        disk.WrittenElsewhere("First", "Theirs");

        var trouble = await host.SaveAsync();

        Assert.Null(trouble);
        Assert.True(host.Deferred);
        Assert.False(sync.Enabled);
        Assert.True(sync.Asking);
        Assert.Equal(2, sync.Arrived?.Holds);
    }

    /// <summary>
    /// And every answer to it finishes the save, whichever side the answer keeps.
    /// </summary>
    /// <remarks>
    /// One assertion says it for all four: what is open and what is in the file agree. That is what
    /// a finished save means, and it is true by a different route each time - two of the answers
    /// write the result out, one takes a file that already holds it, and the fourth writes the side
    /// that was refused in the first place.
    /// </remarks>
    [Theory]
    [InlineData(Arrival.KeepMine)]
    [InlineData(Arrival.TakeTheirs)]
    [InlineData(Arrival.MineWin)]
    [InlineData(Arrival.TheirsWin)]
    public async Task EveryAnswerToARefusedSaveFinishesIt(Arrival answer)
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Mine"));
        disk.WrittenElsewhere("First", "Theirs");

        await host.SaveAsync();
        await sync.ResolveAsync(answer);

        Assert.False(sync.Asking);
        Assert.Equal(Names(session), Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>And each answer keeps what it says it keeps.</summary>
    [Theory]
    [InlineData(Arrival.KeepMine, new[] { "First", "Mine" })]
    [InlineData(Arrival.TakeTheirs, new[] { "First", "Theirs" })]
    [InlineData(Arrival.MineWin, new[] { "First", "Mine", "Theirs" })]
    [InlineData(Arrival.TheirsWin, new[] { "First", "Mine", "Theirs" })]
    public async Task AnAnswerToARefusedSaveKeepsWhatItNames(Arrival answer, string[] expected)
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Mine"));
        disk.WrittenElsewhere("First", "Theirs");

        await host.SaveAsync();
        await sync.ResolveAsync(answer);

        Assert.Equal(expected, Names(session));
    }

    /// <summary>
    /// A refusal over a file that turns out to hold what is open settles itself.
    /// </summary>
    /// <remarks>
    /// The refusal is about a baseline rather than about the contents: something wrote the file,
    /// and what it wrote happens to be what is already here. Asking four questions about a file
    /// nobody disagrees with is how a question gets dismissed unread.
    /// </remarks>
    [Fact]
    public async Task ARefusalOverAFileThatAgreesAsksNothing()
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Mine"));
        disk.WrittenElsewhere(session.Save());

        var trouble = await host.SaveAsync();

        Assert.Null(trouble);
        Assert.False(sync.Asking);
        Assert.Equal(["First", "Mine"], Names(session));
    }

    /// <summary>
    /// A file that cannot be read at all leaves the refusal to be explained in words.
    /// </summary>
    /// <remarks>
    /// The one outcome where there is nothing to ask about: something refused the write and nothing
    /// here can say what over. Reported rather than swallowed, and the work stays in hand.
    /// </remarks>
    [Fact]
    public async Task ARefusalOverAFileThatWillNotReadIsReported()
    {
        var disk = new Disk("First");
        var (sync, host, session) = await OpenAsync(disk);

        session.EditFile(file => file.Add("Mine"));
        disk.WrittenElsewhere("First", "Theirs");

        // After the open, which has to succeed for there to be a session to save.
        disk.Unreadable = true;

        var trouble = await host.SaveAsync();

        Assert.NotNull(trouble);
        Assert.Contains("changed somewhere else", trouble, StringComparison.Ordinal);
        Assert.False(host.Deferred);
        Assert.False(sync.Asking);
    }

    /// <summary>
    /// An answer pressed while something else is reading still reaches the file.
    /// </summary>
    /// <remarks>
    /// The write that follows an answer used to be dropped outright when a read or a write was in
    /// flight - and dropped for good, because the baseline had already moved to what arrived, so
    /// every comparison afterwards found the two sides in step. The two event handlers have always
    /// recorded the miss instead; this caller did not. Reachable against a provider, whose reads are
    /// HTTP round-trips rather than milliseconds.
    /// </remarks>
    [Fact]
    public async Task AnAnswerGivenDuringAReadIsNotLost()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        disk.WrittenElsewhere("Built in the game");
        await sync.RefreshAsync();

        Assert.True(sync.Asking);

        // Answered from inside the next read, which is exactly the window that used to swallow it.
        Task? answered = null;
        disk.DuringRead = () => answered = sync.ResolveAsync(Arrival.KeepMine);

        await sync.RefreshAsync();
        await answered!;

        Assert.False(sync.Asking);

        // Keeping your own means your own reach the file, whenever the answer happened to land.
        // Dropped, the file would still be holding what the game put there.
        Assert.Equal(["First"], Names(EmpireDesignsFile.Load(disk.Contents)));
        Assert.Equal(Names(session), Names(EmpireDesignsFile.Load(disk.Contents)));
    }

    /// <summary>Turning it off stops the watching, and leaves the file where it stands.</summary>
    [Fact]
    public async Task TurningItOffStopsWatchingAndWriting()
    {
        var disk = new Disk("First");
        var (sync, _, session) = await OpenAsync(disk);

        await sync.SetAsync(true);
        await sync.SetAsync(false);

        var writes = disk.Writes;
        session.EditFile(file => file.Add("Second"));

        Assert.False(sync.Enabled);
        Assert.Equal(writes, disk.Writes);
        Assert.False(disk.Watching);
    }
}
