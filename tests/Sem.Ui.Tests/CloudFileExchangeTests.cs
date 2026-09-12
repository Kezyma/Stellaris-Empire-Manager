using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;
using Sem.Ui.Services.Cloud;

namespace Sem.Ui.Tests;

/// <summary>
/// The designs file, where the file is one the player keeps at a cloud provider.
/// </summary>
/// <remarks>
/// Two things are being checked. The first is that this behaves like a host that saves in place -
/// reads the file it was pointed at, writes it back, keeps a dated copy when asked, and refuses to
/// write over somebody else's change rather than winning the race. The second is the one that
/// justifies the shape of the whole feature: that the file sync already in the app drives this with
/// no changes at all, because it asks an exchange for only three things and this answers all three.
/// </remarks>
public sealed class CloudFileExchangeTests
{
    /// <summary>A provider holding one file, which anything else may also write.</summary>
    private sealed class Provider : ICloudProvider
    {
        private int _version;

        public Provider(params string[] empires) => Contents = FileOf(empires);

        public string Name => "Somewhere";

        public byte[] Contents { get; private set; }

        public Dictionary<string, byte[]> Beside { get; } = new(StringComparer.Ordinal);

        public int Reads { get; private set; }

        public int Stats { get; private set; }

        public int Writes { get; private set; }

        public bool Refuse { get; set; }

        public CloudFile File { get; } = new("item-1", "user_empire_designs_v3.4.txt", "/Documents");

        public string Version => _version.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static byte[] FileOf(string[] empires)
        {
            var file = EmpireDesignsFile.CreateEmpty();

            foreach (var name in empires)
            {
                file.Add(name);
            }

            return file.Save();
        }

        /// <summary>Writes the file the way the game, or another device, would.</summary>
        public void WrittenElsewhere(params string[] empires)
        {
            Contents = FileOf(empires);
            _version++;
        }

        public Task<IReadOnlyList<CloudFile>> FindAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CloudFile>>([File]);

        public Task<(byte[] Contents, CloudStamp Stamp)?> ReadAsync(string id, CancellationToken cancellationToken = default)
        {
            Reads++;

            return Task.FromResult<(byte[], CloudStamp)?>((Contents, Stamp()));
        }

        public Task<(CloudWrite Outcome, CloudStamp? Stamp)> WriteAsync(
            string id, byte[] contents, string? ifVersion, CancellationToken cancellationToken = default)
        {
            Writes++;

            if (Refuse)
            {
                return Task.FromResult<(CloudWrite, CloudStamp?)>((CloudWrite.Refused, null));
            }

            if (ifVersion is not null && !string.Equals(ifVersion, Version, StringComparison.Ordinal))
            {
                return Task.FromResult<(CloudWrite, CloudStamp?)>((CloudWrite.Conflicted, null));
            }

            Contents = contents;
            _version++;

            return Task.FromResult<(CloudWrite, CloudStamp?)>((CloudWrite.Written, Stamp()));
        }

        public Task<CloudStamp?> StatAsync(string id, CancellationToken cancellationToken = default)
        {
            Stats++;

            return Task.FromResult<CloudStamp?>(Stamp());
        }

        public Task<bool> WriteBesideAsync(
            CloudFile file, string name, byte[] contents, CancellationToken cancellationToken = default)
        {
            Beside[name] = contents;

            return Task.FromResult(true);
        }

        private CloudStamp Stamp() => new(Version, DateTimeOffset.UnixEpoch, Contents.Length);
    }

    /// <summary>A browser exchange, to prove the things that are not about the file still reach it.</summary>
    private sealed class Browser : IFileExchange
    {
        public List<string> Called { get; } = [];

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents)
        {
            Called.Add(nameof(SaveAsync));

            return Task.FromResult(SaveOutcome.Downloaded);
        }

        public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind)
        {
            Called.Add(nameof(ExportAsync));

            return Task.FromResult(SaveOutcome.Saved);
        }

        public Task<bool> CopyToClipboardAsync(string text)
        {
            Called.Add(nameof(CopyToClipboardAsync));

            return Task.FromResult(true);
        }

        public Task<bool> CanOpenAsync()
        {
            Called.Add(nameof(CanOpenAsync));

            return Task.FromResult(true);
        }
    }

    private static IReadOnlyList<string> Names(byte[] file) =>
        [.. EmpireDesignsFile.Load(file).Designs.Select(d => d.Key)];

    /// <summary>It reads the file it was pointed at, under that file's own name.</summary>
    [Fact]
    public async Task TheChosenFileIsWhatOpens()
    {
        var provider = new Provider("First", "Second");
        var files = new CloudFileExchange(provider, provider.File, new Browser());

        var opened = await files.TryOpenExistingAsync();

        Assert.Equal("user_empire_designs_v3.4.txt", opened?.Name);
        Assert.Equal(["First", "Second"], Names(opened!.Value.Contents));
        Assert.True(files.SavesInPlace);
        Assert.Equal("Save", files.SaveVerb);
    }

    /// <summary>And writes back to it.</summary>
    [Fact]
    public async Task SavingReplacesTheFileAtTheProvider()
    {
        var provider = new Provider("First");
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        await files.TryOpenExistingAsync();

        var changed = EmpireDesignsFile.CreateEmpty();
        changed.Add("First");
        changed.Add("Second");

        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync("ignored", changed.Save(), backUp: false));
        Assert.Equal(["First", "Second"], Names(provider.Contents));
        Assert.Empty(provider.Beside);
    }

    /// <summary>The dated copy is of what is being replaced, not of what replaces it.</summary>
    [Fact]
    public async Task TheBackupHoldsTheFileAsItWasBeforeTheSave()
    {
        var provider = new Provider("Before");
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        await files.TryOpenExistingAsync();

        var changed = EmpireDesignsFile.CreateEmpty();
        changed.Add("After");

        await files.SaveAsync("ignored", changed.Save(), backUp: true);

        var backup = Assert.Single(provider.Beside);

        Assert.Equal(["Before"], Names(backup.Value));
        Assert.Equal(["After"], Names(provider.Contents));

        // Beside the file and named after it, so the two sort together wherever the player looks.
        Assert.Matches(@"^user_empire_designs_v3\.4_\d{6}_\d{6}\.txt$", backup.Key);
    }

    /// <summary>
    /// The dated copy is named by the same rule the desktop uses beside the real file.
    /// </summary>
    /// <remarks>
    /// Through reflection because the rule is internal and deserves to stay that way - the same
    /// reason and the same approach as the store's encoding in <c>SessionHostTests</c>. It is worth
    /// pinning exactly rather than by shape, because the half of it that can be wrong is invisible
    /// in a shape: hh is the twelve-hour clock, and would name the morning's copy and the
    /// afternoon's identically.
    ///
    /// Its twin is <c>SafeFile.DatedBackupPath</c>, tested in Sem.Core.Tests. The two are written
    /// out separately because Sem.Ui may not reach Sem.Io, so if one moves the other has to.
    /// </remarks>
    [Fact]
    public void TheBackupIsNamedByTheSameRuleTheDesktopUses()
    {
        static string Named(string file, DateTime moment) =>
            (string)typeof(CloudFileExchange)
                .GetMethod("DatedName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [file, moment])!;

        Assert.Equal(
            "user_empire_designs_v3.4_260828_140603.txt",
            Named("user_empire_designs_v3.4.txt", new DateTime(2026, 8, 28, 14, 6, 3)));

        Assert.Equal("a_260828_093000.txt", Named("a.txt", new DateTime(2026, 8, 28, 9, 30, 0)));
        Assert.Equal("a_260828_213000.txt", Named("a.txt", new DateTime(2026, 8, 28, 21, 30, 0)));
    }

    /// <summary>
    /// A file that moved on since it was read is not written over.
    /// </summary>
    /// <remarks>
    /// The case a disk cannot have and a provider can. Two devices editing the same designs file is
    /// the ordinary way to use this feature, so losing the race quietly would be losing an evening.
    /// </remarks>
    [Fact]
    public async Task AFileThatChangedUnderneathIsNotOverwritten()
    {
        var provider = new Provider("First");
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        await files.TryOpenExistingAsync();

        provider.WrittenElsewhere("Built somewhere else");

        var mine = EmpireDesignsFile.CreateEmpty();
        mine.Add("Mine");

        Assert.Equal(SaveOutcome.Conflicted, await files.SaveAsync("ignored", mine.Save(), backUp: false));
        Assert.Equal(["Built somewhere else"], Names(provider.Contents));
    }

    /// <summary>A provider that will not is reported as that, not as a conflict.</summary>
    [Fact]
    public async Task AProviderThatRefusesIsReportedAsRefusing()
    {
        var provider = new Provider("First") { Refuse = true };
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        await files.TryOpenExistingAsync();

        Assert.Equal(SaveOutcome.Refused, await files.SaveAsync("ignored", [1], backUp: false));
    }

    /// <summary>
    /// Everything that is not about the designs file is still the browser's.
    /// </summary>
    /// <remarks>
    /// Connecting to a provider does not stop a picture being a download or a link being a link.
    /// </remarks>
    [Fact]
    public async Task TheBrowserStillDoesTheThingsThatAreNotAboutTheFile()
    {
        var provider = new Provider("First");
        var browser = new Browser();
        var files = (IFileExchange)new CloudFileExchange(provider, provider.File, browser);

        await files.ExportAsync("card.png", [1], ExportKind.Image);
        await files.CopyToClipboardAsync("a link");
        await files.CanOpenAsync();

        Assert.Equal(["ExportAsync", "CopyToClipboardAsync", "CanOpenAsync"], browser.Called);
        Assert.Equal(0, provider.Writes);
    }

    /// <summary>Nothing is asked of the provider once this is disposed.</summary>
    [Fact]
    public void DisposingStopsTheWatching()
    {
        var provider = new Provider("First");
        var files = new CloudFileExchange(provider, provider.File, new Browser());

        var watch = files.Watch(() => { });
        Assert.NotNull(watch);

        files.Dispose();
        watch.Dispose();

        Assert.Equal(0, provider.Writes);
    }

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

    /// <summary>
    /// The sync the app already has drives a provider with no changes to it at all.
    /// </summary>
    /// <remarks>
    /// The test the design rests on. DesignSync was written against a file on a disk and asks an
    /// exchange for three things - whether it saves in place, what the file holds, and to be told
    /// when it changes. If a cloud exchange answers those, every behaviour built on top comes free:
    /// writing when the list changes, not reading its own writing back, and asking rather than
    /// discarding when both sides have moved.
    /// </remarks>
    [Fact]
    public async Task TheFileSyncDrivesAProviderUnchanged()
    {
        var provider = new Provider("First");
        var preferences = new Preferences();
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        var host = new SessionHost(new Source(), files, store: null, assumeAllPacks: true, preferences);
        var session = await host.GetAsync() ?? throw new InvalidOperationException("no session");

        var sync = new DesignSync(host, files, preferences);
        await sync.AttachAsync(session);
        await sync.SetAsync(true);

        Assert.True(sync.Available);
        Assert.True(sync.Enabled);

        // Opening read the provider's file rather than starting empty.
        Assert.Equal(["First"], [.. session.File!.Designs.Select(d => d.Key)]);

        // And a change to the list reaches the provider without anybody pressing Save.
        session.EditFile(file => file.Add("Second"));

        Assert.Equal(["First", "Second"], Names(provider.Contents));
    }

    /// <summary>And it asks rather than discarding when both sides have moved.</summary>
    [Fact]
    public async Task AnUnsavedEditAgainstAChangedFileIsAskedAbout()
    {
        var provider = new Provider("First");
        var preferences = new Preferences();
        var files = new CloudFileExchange(provider, provider.File, new Browser());
        var host = new SessionHost(new Source(), files, store: null, assumeAllPacks: true, preferences);
        var session = await host.GetAsync() ?? throw new InvalidOperationException("no session");

        var sync = new DesignSync(host, files, preferences);
        await sync.AttachAsync(session);
        await sync.SetAsync(true);

        session.Select(session.File!.Designs[0]);
        session.Edit(design => design.Name.Key = "Renamed, not saved");

        provider.WrittenElsewhere("Built somewhere else");
        await sync.RefreshAsync();

        Assert.True(sync.Asking);
        Assert.Equal(["First"], [.. session.File!.Designs.Select(d => d.Key)]);
    }
}
