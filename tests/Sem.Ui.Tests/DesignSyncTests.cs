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

        public int Writes { get; private set; }

        public int Reads { get; private set; }

        public bool? LastBackUp { get; private set; }

        public bool Watching { get; private set; }

        public bool SavesInPlace => true;

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

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
        {
            Contents = contents;
            LastBackUp = backUp;
            Writes++;

            return Task.FromResult(SaveOutcome.Saved);
        }

        public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
        {
            Reads++;

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
        sync.Decline();

        Assert.False(sync.Asking);
        Assert.Equal(["First"], Names(session));
        Assert.Equal("Renamed, not saved", session.Current!.Name.Key);
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
        disk.WrittenElsewhere("Something else entirely");

        await sync.SetAsync(true);

        Assert.Equal(["First", "Added while it was off"], Names(EmpireDesignsFile.Load(disk.Contents)));
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
