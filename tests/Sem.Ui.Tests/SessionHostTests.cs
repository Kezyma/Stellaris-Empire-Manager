using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the host owes the player once a file has been handed over, and what it owes them when one
/// cannot be opened at all.
/// </summary>
/// <remarks>
/// Both of these were real losses rather than hypotheticals. Export handed the player a file, called
/// the work saved and never wrote the browser's own copy, so the next reload gave back the empire as
/// it had been before the export. And a designs file that would not parse recorded why and then had
/// the reason deleted on the way out, so the desktop opened empty with nothing said.
/// </remarks>
public sealed class SessionHostTests
{
    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },

            // One class that insists on a trait, so a file can be stale in the way opening one
            // repairs: a design of this class that does not carry trait_lithoid gains it as it is
            // read. See DerivedTraitTests for the repair itself.
            SpeciesClasses = [new SpeciesClassDefinition("LITH", "BIOLOGICAL") { ForcedTrait = "trait_lithoid" }],
            Traits = [new TraitDefinition("trait_lithoid", TraitKind.Species)],
        },
        new Dictionary<string, string>(),
        "assets");

    /// <summary>A file holding one empire that does not yet carry what its class forces.</summary>
    /// <summary>The file a stored value holds.</summary>
    /// <remarks>
    /// Through reflection because <c>Kept</c> is internal to the app and deserves to stay that way:
    /// how a browser's store wraps a file is nobody else's business, including these tests'. What
    /// they are entitled to is the file back, which is what this is.
    /// </remarks>
    private static byte[] Decoded(string? kept)
    {
        ArgumentNullException.ThrowIfNull(kept);

        return typeof(SessionHost).Assembly
            .GetType("Sem.Ui.Services.Kept")!
            .GetMethod("TryDecode")!
            .Invoke(null, [kept]) as byte[]
            ?? throw new InvalidOperationException("the store did not hold bytes");
    }

    private static string Stale()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        file.Add("Old").Species.Class = "LITH";

        return System.Text.Encoding.UTF8.GetString(file.Save());
    }

    private sealed class Source : IGameDataSource
    {
        public Task<Sem.Ui.Services.GameData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Data());

        public Task<IReadOnlyList<PortraitOutfit>> LoadWardrobeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortraitOutfit>>([]);
    }

    /// <summary>A browser: nothing of its own to open, and a store that keeps what it is given.</summary>
    private sealed class Browser : IFileExchange
    {
        public string? Kept { get; private set; }

        public int Writes { get; private set; }

        public bool Refuse { get; init; }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            Task.FromResult(SaveOutcome.Saved);

        public sealed class Store(Browser owner) : IDesignStore
        {
            public Task<string?> ReadAsync() => Task.FromResult<string?>(null);

            public Task<bool> WriteAsync(string contents)
            {
                if (owner.Refuse)
                {
                    return Task.FromResult(false);
                }

                owner.Kept = contents;
                owner.Writes++;
                return Task.FromResult(true);
            }
        }
    }

    /// <summary>The desktop: it has the player's real file and writes that instead of a store.</summary>
    private sealed class Desktop : IFileExchange
    {
        public bool SavesInPlace => true;

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            Task.FromResult(SaveOutcome.Saved);
    }

    /// <summary>A host whose own file is unreadable, which is what a truncated designs file looks like.</summary>
    private sealed class Unreadable : IFileExchange
    {
        public bool SavesInPlace => true;

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            Task.FromResult(SaveOutcome.Saved);

        public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync() =>
            throw new IOException("the file is in pieces");
    }

    /// <summary>A store that already holds a file, and says whether anything wrote over it.</summary>
    private sealed class Holding(string contents) : IDesignStore
    {
        public string Contents { get; private set; } = contents;

        public int Writes { get; private set; }

        public Task<string?> ReadAsync() => Task.FromResult<string?>(Contents);

        public Task<bool> WriteAsync(string written)
        {
            Contents = written;
            Writes++;
            return Task.FromResult(true);
        }
    }

    /// <summary>A store holding nothing, which is what a browser that has not been used yet is.</summary>
    private sealed class Empty : IDesignStore
    {
        public int Writes { get; private set; }

        public Task<string?> ReadAsync() => Task.FromResult<string?>(null);

        public Task<bool> WriteAsync(string contents)
        {
            Writes++;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Starting with nothing does not write nothing over the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The empty start is not a file arriving, and it used to be heard by the keeper along with
    /// everything else: the store was handed an empty file the first time the app ran, and every
    /// start afterwards read that back and kept it empty. A store saying it holds nothing is to be
    /// left holding nothing until the player puts something in it.
    /// </para>
    /// <para>
    /// Opened by hand, because the helper adds an empire afterwards - which is a change, and is
    /// supposed to be written.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task StartingEmptyKeepsNothing()
    {
        var store = new Empty();
        var host = new SessionHost(new Source(), new Browser(), store);

        Assert.NotNull(await host.GetAsync());
        Assert.Equal(0, store.Writes);
    }

    /// <summary>
    /// And a file that was brought up to date as it opened is kept, without waiting for an edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A design that does not carry the traits its own choices force gains them as the file is
    /// read. The browser's store is the only copy of that, so leaving it for the next edit left the
    /// repair correct on screen and undone on disk, to be done again on every load.
    /// </para>
    /// <para>
    /// Opened by hand rather than through <see cref="OpenAsync"/>, because the opening itself is
    /// what is being watched: that helper adds an empire and marks the file saved afterwards, which
    /// writes a second time and clears the very flag this is about.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARepairedFileIsKeptAsItOpens()
    {
        // Kept as plain text, which the older store shape used and the host still reads.
        var store = new Holding(Stale());
        var host = new SessionHost(new Source(), new Browser(), store);

        var session = await host.GetAsync();

        Assert.NotNull(session);
        Assert.True(session.HasUnwrittenFileChanges, "opening it should have brought the file up to date");
        Assert.Equal(1, store.Writes);

        // And what was kept is the repair, not merely something that differs from what was there.
        var kept = EmpireDesignsFile.Load(Decoded(store.Contents));

        Assert.Contains("trait_lithoid", kept.Designs[0].Species.Traits);
    }

    private static async Task<(SessionHost Host, DesignSession Session)> OpenAsync(IFileExchange files, IDesignStore? store)
    {
        var host = new SessionHost(new Source(), files, store);
        var session = await host.GetAsync()
            ?? throw new InvalidOperationException(host.LoadError ?? "the session did not open");

        session.CreateEmpire(file => file.Add("Mine"));
        session.MarkSaved();

        return (host, session);
    }

    /// <summary>
    /// The export bug, in one assertion: handing the player a file has to leave the browser holding
    /// the same thing, because the browser's copy is what a reload restores.
    /// </summary>
    [Fact]
    public async Task KeepingAfterAnExportWritesTheBrowsersOwnCopy()
    {
        var files = new Browser();
        var (host, session) = await OpenAsync(files, new Browser.Store(files));

        session.Edit(design => design.Authority = "auth_democratic");

        Assert.Null(await host.RememberAsync());
        Assert.NotNull(files.Kept);

        // What was kept has to be the edit, not the empire as it was before it.
        var restored = EmpireDesignsFile.Load(Decoded(files.Kept));

        Assert.Equal("auth_democratic", restored.Designs.Single().Authority);
    }

    /// <summary>
    /// The desktop wrote the player's real file a moment earlier, and that is the copy. Asked to keep
    /// one it says yes without a store, rather than reporting a failure it has no reason to have.
    /// </summary>
    [Fact]
    public async Task TheDesktopHasNothingFurtherToKeep()
    {
        var (host, _) = await OpenAsync(new Desktop(), store: null);

        Assert.Null(await host.RememberAsync());
    }

    /// <summary>
    /// A store that refuses has to say so, or the caller marks the work saved over a copy that was
    /// never written — which is the whole bug, one layer down.
    /// </summary>
    [Fact]
    public async Task AStoreThatRefusesIsReported()
    {
        var files = new Browser { Refuse = true };
        var (host, session) = await OpenAsync(files, new Browser.Store(files));

        session.Edit(design => design.Authority = "auth_democratic");

        Assert.NotNull(await host.RememberAsync());
    }

    /// <summary>
    /// Why the file would not open has to survive the rest of opening the session. It was recorded
    /// and then cleared one line later, so the desktop showed an empty list and said nothing at all.
    /// </summary>
    [Fact]
    public async Task AFileThatWouldNotOpenSaysWhy()
    {
        var host = new SessionHost(new Source(), new Unreadable());

        Assert.NotNull(await host.GetAsync());
        Assert.NotNull(host.LoadError);
        Assert.Contains("in pieces", host.LoadError, StringComparison.Ordinal);
    }
}
