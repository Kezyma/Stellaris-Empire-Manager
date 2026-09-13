using System.Text;
using Microsoft.JSInterop;
using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;
using Sem.Ui.Services.Cloud;

namespace Sem.Ui.Tests;

/// <summary>
/// Connecting the app to a file kept at a provider, and letting go of it again.
/// </summary>
/// <remarks>
/// <see cref="CloudFileExchangeTests"/> covers what happens to the bytes. This covers the decisions
/// made around them, which is where the workflow lives: what a failure leaves behind, what survives
/// a reload, and what disconnecting is obliged to forget. Each is a way somebody ends up pointed at
/// a file they did not choose, or writing to one they thought they had let go of.
/// </remarks>
public sealed class CloudConnectionTests
{
    /// <summary>A provider holding one file, which can be made to fail in each of its ways.</summary>
    private sealed class Provider : ICloudProvider
    {
        private byte[] _contents = FileOf("First", "Second");

        /// <summary>Puts a chosen set of empires in the file, for the merges to be read off.</summary>
        public void Holds(params string[] empires) => _contents = FileOf(empires);

        /// <summary>What the file holds, for a test that has to hand the same bytes back.</summary>
        public byte[] Contents => _contents;

        public string Name => "Somewhere";

        /// <summary>Set when the file cannot be fetched at all.</summary>
        public bool Refuse { get; set; }

        /// <summary>Set when the session has ended, which is a different failure from refusing.</summary>
        public bool SignedOut { get; set; }

        public Task<bool> SignedInAsync() => Task.FromResult(!SignedOut);

        /// <summary>Leaves something in the file that is not a designs file.</summary>
        public void HoldsSomethingElse() => _contents = Encoding.UTF8.GetBytes("}}} not a designs file");

        private static byte[] FileOf(params string[] empires)
        {
            var file = EmpireDesignsFile.CreateEmpty();

            foreach (var name in empires)
            {
                file.Add(name);
            }

            return file.Save();
        }

        public Task<IReadOnlyList<CloudEntry>> ListAsync(
            string? folderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CloudEntry>>([]);

        public Task<IReadOnlyList<CloudFile>> FindAsync(
            string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CloudFile>>([]);

        public Task<(byte[] Contents, CloudStamp Stamp)?> ReadAsync(
            string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Refuse || SignedOut
                ? null
                : ((byte[], CloudStamp)?)(_contents, new CloudStamp("1", DateTimeOffset.UnixEpoch, _contents.Length)));

        public Task<(CloudWrite Outcome, CloudStamp? Stamp)> WriteAsync(
            string id, byte[] contents, string? ifVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult<(CloudWrite, CloudStamp?)>(
                (CloudWrite.Written, new CloudStamp("2", DateTimeOffset.UnixEpoch, contents.Length)));

        public Task<CloudStamp?> StatAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CloudStamp?>(new CloudStamp("1", DateTimeOffset.UnixEpoch, _contents.Length));

        public Task<bool> WriteBesideAsync(
            CloudFile file, string name, byte[] contents, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    /// <summary>Remembers a token without a browser to keep it in.</summary>
    private sealed class Tokens : ITokenStore
    {
        private readonly Dictionary<string, string> _held = new(StringComparer.Ordinal);

        public Tokens(bool signedIn)
        {
            if (signedIn)
            {
                _held["sem.cloud.refresh"] = "a-refresh-token";
            }
        }

        public bool Holds(string key) => _held.ContainsKey(key);

        public Task<string?> ReadAsync(string key) => Task.FromResult(_held.GetValueOrDefault(key));

        public Task WriteAsync(string key, string? value)
        {
            if (value is null)
            {
                _held.Remove(key);
            }
            else
            {
                _held[key] = value;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A runtime that fails if anything reaches it.
    /// </summary>
    /// <remarks>
    /// The browser exchange is only ever the thing to fall back to here, so it should be held and
    /// not called. Throwing says so rather than letting a stray call pass unnoticed.
    /// </remarks>
    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException($"nothing should have called {identifier}");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException($"nothing should have called {identifier}");
    }

    /// <summary>The browser's own copy, so that what it is handed can be read back.</summary>
    private sealed class Store : IDesignStore
    {
        public string? Kept { get; private set; }

        public Task<string?> ReadAsync() => Task.FromResult(Kept);

        public Task<bool> WriteAsync(string contents)
        {
            Kept = contents;

            return Task.FromResult(true);
        }
    }

    private sealed class Source : IGameDataSource
    {
        public Task<Sem.Ui.Services.GameData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                },
                new Dictionary<string, string>(),
                "assets"));

        public Task<IReadOnlyList<PortraitOutfit>> LoadWardrobeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortraitOutfit>>([]);
    }

    /// <summary>Everything a connection needs, wired the way the app wires it.</summary>
    private sealed class Rig : IDisposable
    {
        public Rig(bool signedIn = true)
        {
            Store = new Tokens(signedIn);
            Browser = new BrowserFileExchange(new NoRuntime());
            Router = new FileExchangeRouter(Browser);
            Preferences = new Preferences();

            Host = new SessionHost(new Source(), Router, Kept, preferences: Preferences);

            var sync = new DesignSync(Host, Router, Preferences);

            Connection = new CloudConnection(
                Provider,
                new OneDriveAuth(new HttpClient(), Store, "client-id", "https://example.invalid/"),
                Router,
                Browser,
                Host,
                Preferences,
                sync);
        }

        public Provider Provider { get; } = new();

        public Store Kept { get; } = new();

        public Tokens Store { get; }

        public BrowserFileExchange Browser { get; }

        public FileExchangeRouter Router { get; }

        public Preferences Preferences { get; }

        public SessionHost Host { get; }

        public CloudConnection Connection { get; }

        /// <summary>Opens a session holding these empires, so there is something to lose.</summary>
        public async Task<DesignSession> OpenAsync(params string[] mine)
        {
            var session = await Host.GetAsync() ?? throw new InvalidOperationException("no session");

            if (mine.Length > 0)
            {
                var file = EmpireDesignsFile.CreateEmpty();

                foreach (var name in mine)
                {
                    file.Add(name);
                }

                session.Open(file, "mine.txt");
            }

            return session;
        }

        public void Dispose()
        {
            Connection.Dispose();
            Host.Dispose();
        }
    }

    private static CloudFile TheFile(string folder = "/KEZYMA-DT/Documents/Paradox Interactive/Stellaris") =>
        new("item-1", "user_empire_designs_v3.4.txt", folder);

    /// <summary>
    /// The path is what is shown, because the name on its own does not say which file it is.
    /// </summary>
    /// <remarks>
    /// One account backing up two machines holds two files with this name, in folders named after
    /// each machine. Being shown the name tells somebody nothing about which is about to be written.
    /// </remarks>
    [Theory]
    [InlineData("/Documents", "/Documents/user_empire_designs_v3.4.txt")]
    [InlineData("/", "/user_empire_designs_v3.4.txt")]
    [InlineData("", "/user_empire_designs_v3.4.txt")]
    public async Task TheWholePathIsWhatNamesTheFile(string folder, string expected)
    {
        using var rig = new Rig();

        Assert.Null(rig.Connection.Where);
        Assert.True(await rig.Connection.UseAsync(TheFile(folder)));
        Assert.Equal(expected, rig.Connection.Where);
    }

    /// <summary>A file that is not a designs file leaves everything as it was.</summary>
    /// <remarks>
    /// The failure that is easiest to get wrong: the file was reachable, so the session is fine and
    /// the sign-in is fine, and the only thing wrong is the choice. Switching the router before
    /// finding out would point Save at it anyway.
    /// </remarks>
    [Fact]
    public async Task AFileThatIsNotADesignsFileChangesNothing()
    {
        using var rig = new Rig();
        rig.Provider.HoldsSomethingElse();

        Assert.False(await rig.Connection.UseAsync(TheFile()));
        Assert.False(rig.Connection.Connected);
        Assert.False(rig.Connection.SessionEnded);
        Assert.False(rig.Router.SavesInPlace);
        Assert.Null(rig.Preferences.Get("cloud.file"));
        Assert.Contains("not a designs file", rig.Connection.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// A file that cannot be fetched is told apart from a sign-in that has ended.
    /// </summary>
    /// <remarks>
    /// The difference is the whole point of <see cref="CloudConnection.SessionEnded"/>: one of them
    /// has an obvious remedy and a button to offer, and the other does not.
    /// </remarks>
    [Fact]
    public async Task AFileThatCannotBeReadIsToldApartFromAnEndedSession()
    {
        using var refused = new Rig();
        refused.Provider.Refuse = true;

        Assert.False(await refused.Connection.UseAsync(TheFile()));
        Assert.False(refused.Connection.SessionEnded);
        Assert.Contains("could not be read", refused.Connection.Note, StringComparison.Ordinal);

        using var ended = new Rig();
        ended.Provider.SignedOut = true;

        Assert.False(await ended.Connection.UseAsync(TheFile()));
        Assert.True(ended.Connection.SessionEnded);
        Assert.Contains("sign-in has ended", ended.Connection.Note, StringComparison.Ordinal);
    }

    /// <summary>The file chosen last time is opened again without being asked for.</summary>
    [Fact]
    public async Task TheFileChosenBeforeIsOpenedAgain()
    {
        using var first = new Rig();
        Assert.True(await first.Connection.UseAsync(TheFile()));

        var kept = first.Preferences.Get("cloud.file");

        using var next = new Rig();
        next.Preferences.Set("cloud.file", kept!);

        Assert.True(await next.Connection.ResumeAsync());
        Assert.Equal("/KEZYMA-DT/Documents/Paradox Interactive/Stellaris/user_empire_designs_v3.4.txt",
            next.Connection.Where);
    }

    /// <summary>
    /// A choice made before the folder was kept still opens, rather than being thrown away.
    /// </summary>
    /// <remarks>
    /// Two fields was the earlier shape. Somebody who connected then reloads into this build with
    /// two fields in their preferences, and losing their file over it would be a poor greeting.
    /// </remarks>
    [Fact]
    public async Task AChoiceMadeBeforeTheFolderWasKeptStillOpens()
    {
        using var rig = new Rig();
        rig.Preferences.Set("cloud.file", "item-1|user_empire_designs_v3.4.txt");

        Assert.True(await rig.Connection.ResumeAsync());
        Assert.Equal("/user_empire_designs_v3.4.txt", rig.Connection.Where);
    }

    /// <summary>Nothing remembered, or nothing usable, is nothing to go back to.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("item-1")]
    [InlineData("|no-id")]
    public async Task ThereIsNothingToGoBackTo(string kept)
    {
        using var rig = new Rig();
        rig.Preferences.Set("cloud.file", kept);

        Assert.False(await rig.Connection.ResumeAsync());
        Assert.False(rig.Connection.Connected);
    }

    /// <summary>Choosing the file is also when writing as you go is decided.</summary>
    /// <remarks>
    /// Both halves matter. Answering yes has to survive the reload that reconnects, and answering
    /// no has to be honoured, because the file being written is the player's real one.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChoosingTheFileIsWhenWritingAsYouGoIsDecided(bool wanted)
    {
        using var rig = new Rig();

        Assert.True(await rig.Connection.UseAsync(TheFile(), wanted));
        Assert.Equal(wanted, rig.Preferences.SyncsWithFile);
        Assert.Equal(wanted, rig.Connection.AutoSaveRemembered);
    }

    /// <summary>
    /// Disconnecting forgets the file, the session, and the answer about writing by itself.
    /// </summary>
    /// <remarks>
    /// The last of those is the one worth a test. Left set, a later connection to a different file
    /// would start writing it as you go without anybody having said so.
    /// </remarks>
    [Fact]
    public async Task DisconnectingForgetsTheFileAndTheAnswerAboutWriting()
    {
        using var rig = new Rig();
        Assert.True(await rig.Connection.UseAsync(TheFile(), autoSave: true));

        await rig.Connection.DisconnectAsync();

        Assert.False(rig.Connection.Connected);
        Assert.Null(rig.Connection.Where);
        Assert.False(rig.Preferences.SyncsWithFile);
        Assert.Equal(string.Empty, rig.Preferences.Get("cloud.file"));
        Assert.False(rig.Store.Holds("sem.cloud.refresh"));
        Assert.False(rig.Router.SavesInPlace);
    }

    private static IReadOnlyList<string> Names(DesignSession session) =>
        [.. session.File!.Designs.Select(d => d.Key)];

    /// <summary>
    /// What the browser's own copy holds.
    /// </summary>
    /// <remarks>
    /// The store takes text, so a file goes into it base64 behind a prefix that cannot begin a
    /// designs file. Unpicked here rather than through the app's own encoder, because a test that
    /// used the same code to write and to read would pass whatever that code happened to do.
    /// </remarks>
    private static IReadOnlyList<string> Names(Store store)
    {
        Assert.NotNull(store.Kept);
        Assert.StartsWith("{sem/b64}", store.Kept, StringComparison.Ordinal);

        var bytes = Convert.FromBase64String(store.Kept!["{sem/b64}".Length..]);

        return [.. EmpireDesignsFile.Load(bytes).Designs.Select(d => d.Key)];
    }

    /// <summary>
    /// Each answer keeps exactly what it says it keeps.
    /// </summary>
    /// <remarks>
    /// The whole point of asking. Connecting used to mean the file wins, so choosing one after an
    /// afternoon's work threw the afternoon away with no question and no way back.
    /// </remarks>
    [Theory]
    [InlineData(CloudArrival.TakeTheirs, new[] { "Shared", "Theirs" })]
    [InlineData(CloudArrival.KeepMine, new[] { "Mine", "Shared" })]
    [InlineData(CloudArrival.TheirsWin, new[] { "Mine", "Shared", "Theirs" })]
    [InlineData(CloudArrival.MineWin, new[] { "Shared", "Theirs", "Mine" })]
    public async Task EachAnswerKeepsWhatItSaysItKeeps(CloudArrival answer, string[] expected)
    {
        using var rig = new Rig();
        rig.Provider.Holds("Shared", "Theirs");

        var session = await rig.OpenAsync("Mine", "Shared");

        Assert.True(await rig.Connection.UseAsync(
            TheFile(), autoSave: false, (_, _) => Task.FromResult<CloudArrival?>(answer)));

        Assert.Equal(expected, Names(session));

        // Whatever was asked for is now the file's name, because that is where a save goes.
        Assert.Equal("user_empire_designs_v3.4.txt", session.FileName);
    }

    /// <summary>
    /// Anything but taking the file outright leaves the file owed something.
    /// </summary>
    /// <remarks>
    /// The flag is what lights Save and what draws "not yet written back". Without it the three
    /// answers that keep any of your own work would look settled while the file still held
    /// something else, which is the quiet version of losing it.
    /// </remarks>
    [Theory]
    [InlineData(CloudArrival.TakeTheirs, false)]
    [InlineData(CloudArrival.KeepMine, true)]
    [InlineData(CloudArrival.TheirsWin, true)]
    [InlineData(CloudArrival.MineWin, true)]
    public async Task OnlyTakingTheFileLeavesNothingOwedToIt(CloudArrival answer, bool owed)
    {
        using var rig = new Rig();
        rig.Provider.Holds("Shared", "Theirs");

        var session = await rig.OpenAsync("Mine", "Shared");

        Assert.True(await rig.Connection.UseAsync(
            TheFile(), autoSave: false, (_, _) => Task.FromResult<CloudArrival?>(answer)));

        Assert.Equal(owed, session.HasUnwrittenFileChanges);
    }

    /// <summary>Stopping at the question leaves everything exactly as it was.</summary>
    [Fact]
    public async Task StoppingAtTheQuestionChangesNothing()
    {
        using var rig = new Rig();
        rig.Provider.Holds("Theirs");

        var session = await rig.OpenAsync("Mine");

        Assert.False(await rig.Connection.UseAsync(
            TheFile(), autoSave: false, (_, _) => Task.FromResult<CloudArrival?>(null)));

        Assert.Equal(["Mine"], Names(session));
        Assert.False(rig.Connection.Connected);
        Assert.False(rig.Router.SavesInPlace);
        Assert.Null(rig.Preferences.Get("cloud.file"));

        // Stopped, not failed. A note here would be the app reporting a decision back as a problem.
        Assert.Null(rig.Connection.Note);
    }

    /// <summary>
    /// The question is asked only where there is really a decision to make.
    /// </summary>
    /// <remarks>
    /// Three of these would be a question with one useful answer, and the last would be a question
    /// on every ordinary reload of a connection that is already in step - which is how a safeguard
    /// turns into something people click past without reading.
    /// </remarks>
    [Fact]
    public async Task NothingWorthAskingAboutIsNotAsked()
    {
        // Nothing open: there is nothing that taking the file could cost.
        using var empty = new Rig();
        empty.Provider.Holds("Theirs");
        var emptySession = await empty.OpenAsync();
        var asked = false;

        Assert.True(await empty.Connection.UseAsync(TheFile(), autoSave: false, Counting()));
        Assert.False(asked);
        Assert.Equal(["Theirs"], Names(emptySession));

        // A file holding nothing: taking it would empty the list, and the other answers all agree.
        using var hollow = new Rig();
        hollow.Provider.Holds();
        var hollowSession = await hollow.OpenAsync("Mine");
        asked = false;

        Assert.True(await hollow.Connection.UseAsync(TheFile(), autoSave: false, Counting()));
        Assert.False(asked);
        Assert.Equal(["Mine"], Names(hollowSession));
        Assert.True(hollowSession.HasUnwrittenFileChanges);

        // And a file that already says what is open says nothing new.
        using var same = new Rig();
        same.Provider.Holds("Mine", "Shared");
        var sameSession = await same.OpenAsync();
        asked = false;

        Assert.True(await same.Connection.UseAsync(TheFile(), autoSave: false, Counting()));
        Assert.False(asked);

        // Opened once, so what is in hand is now exactly the file - and reconnecting to it asks
        // nothing, which is the case that would otherwise nag on every reload.
        Assert.True(await same.Connection.UseAsync(TheFile(), autoSave: false, Counting()));
        Assert.False(asked);

        ArrivalQuestion Counting() => (_, _) => { asked = true; return Task.FromResult<CloudArrival?>(CloudArrival.TakeTheirs); };
    }

    /// <summary>
    /// An answer from the provider is recognised as one whether it was yes or no.
    /// </summary>
    /// <remarks>
    /// What the loop was made of. A refusal that is not recognised as a return stays in the address
    /// and puts nothing on screen, so pressing the provider again fetches another answer onto a page
    /// that will not read that one either.
    /// </remarks>
    [Theory]
    [InlineData("https://example.invalid/?code=abc&state=xyz", true)]
    [InlineData("https://example.invalid/?error=access_denied", true)]
    [InlineData("https://example.invalid/?error=access_denied&error_description=no", true)]
    [InlineData("https://example.invalid/", false)]
    [InlineData("https://example.invalid/?something=else", false)]
    public void AnAnswerFromTheProviderIsRecognisedEitherWay(string address, bool expected) =>
        Assert.Equal(expected, CloudConnection.IsSignInReturn(address));

    /// <summary>
    /// A sign-in that did not finish says so, rather than leaving the page looking untouched.
    /// </summary>
    [Fact]
    public async Task ASignInThatDidNotFinishSaysSo()
    {
        using var rig = new Rig();

        // A return carrying a code this tab never asked for, which is dropped rather than spent.
        Assert.False(await rig.Connection.CompleteSignInAsync("https://example.invalid/?code=abc&state=nope"));
        Assert.NotNull(rig.Connection.Note);
        Assert.Contains("did not finish", rig.Connection.Note, StringComparison.Ordinal);

        // An ordinary load is not a failed anything, and has nothing to report.
        using var plain = new Rig();

        Assert.False(await plain.Connection.CompleteSignInAsync("https://example.invalid/"));
        Assert.Null(plain.Connection.Note);
    }

    /// <summary>
    /// Disconnecting hands what is open back to the browser rather than dropping it.
    /// </summary>
    /// <remarks>
    /// Nothing mirrors a file to the browser's own copy while a host that saves in place is
    /// connected, which is right - the file is the copy that counts. The moment that ends, though,
    /// nothing is holding the work at all, and everything done since connecting was going with the
    /// next reload: the merge that had just been chosen included.
    /// </remarks>
    [Fact]
    public async Task DisconnectingHandsTheWorkBackToTheBrowser()
    {
        using var rig = new Rig();
        rig.Provider.Holds("Shared", "Theirs");

        var session = await rig.OpenAsync("Mine", "Shared");

        Assert.True(await rig.Connection.UseAsync(
            TheFile(), autoSave: false, (_, _) => Task.FromResult<CloudArrival?>(CloudArrival.MineWin)));

        // The browser's copy is whatever it was before connecting: nothing mirrors to it while a
        // host that saves in place is the one being saved to, so the merge has not reached it.
        Assert.Equal(["Mine", "Shared"], Names(rig.Kept));

        await rig.Connection.DisconnectAsync();

        // And now it holds what the merge produced, so a reload finds it rather than the file
        // that was open before any of this.
        Assert.Equal(["Shared", "Theirs", "Mine"], Names(rig.Kept));
        Assert.Equal(["Shared", "Theirs", "Mine"], Names(session));
    }

    /// <summary>Disconnecting leaves the row saying what is true of it afterwards.</summary>
    [Fact]
    public async Task DisconnectingStopsTheRowSayingSignedIn()
    {
        using var rig = new Rig();

        Assert.True(await rig.Connection.SignedInAsync());
        Assert.Equal("Signed in", Assert.Single(rig.Connection.Providers).Why);

        Assert.True(await rig.Connection.UseAsync(TheFile()));
        await rig.Connection.DisconnectAsync();

        Assert.Null(Assert.Single(rig.Connection.Providers).Why);
    }

    /// <summary>The row says whether there is already a session at that provider.</summary>
    [Fact]
    public async Task TheRowSaysWhetherThereIsASessionAtThatProvider()
    {
        using var stranger = new Rig(signedIn: false);

        Assert.False(await stranger.Connection.SignedInAsync());
        var row = Assert.Single(stranger.Connection.Providers);
        Assert.Equal("Somewhere", row.Name);
        Assert.True(row.Ready);
        Assert.Null(row.Why);

        using var known = new Rig();

        Assert.True(await known.Connection.SignedInAsync());
        Assert.Equal("Signed in", Assert.Single(known.Connection.Providers).Why);
    }
}
