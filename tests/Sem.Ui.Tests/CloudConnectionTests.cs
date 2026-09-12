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

            var host = new SessionHost(new Source(), Router, preferences: Preferences);
            var sync = new DesignSync(host, Router, Preferences);

            Connection = new CloudConnection(
                Provider,
                new OneDriveAuth(new HttpClient(), Store, "client-id", "https://example.invalid/"),
                Router,
                Browser,
                host,
                Preferences,
                sync);
        }

        public Provider Provider { get; } = new();

        public Tokens Store { get; }

        public BrowserFileExchange Browser { get; }

        public FileExchangeRouter Router { get; }

        public Preferences Preferences { get; }

        public CloudConnection Connection { get; }

        public void Dispose() => Connection.Dispose();
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
