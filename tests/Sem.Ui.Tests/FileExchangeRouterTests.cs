using System.Reflection;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The exchange that stands in for whichever one the app is using.
/// </summary>
/// <remarks>
/// One kind of mistake matters here and it is silent. <see cref="IFileExchange"/> gives most of its
/// members a default, so a router that forgets one still compiles and still runs - it just answers
/// the default instead of asking the exchange behind it. Forget <see cref="IFileExchange.SavesInPlace"/>
/// and the header offers Export over a host that saves in place; forget
/// <see cref="IFileExchange.Watch"/> and the file is never watched, with nothing anywhere saying so.
/// </remarks>
public sealed class FileExchangeRouterTests
{
    /// <summary>An exchange whose every answer differs from the interface's default.</summary>
    private sealed class Loud : IFileExchange
    {
        public Loud(string name) => Name = name;

        public string Name { get; }

        public List<string> Called { get; } = [];

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents)
        {
            Called.Add(nameof(SaveAsync));

            return Task.FromResult(SaveOutcome.Cancelled);
        }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
        {
            Called.Add("SaveAsync3");

            return Task.FromResult(backUp ? SaveOutcome.Saved : SaveOutcome.Downloaded);
        }

        public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind)
        {
            Called.Add(nameof(ExportAsync));

            return Task.FromResult(SaveOutcome.Saved);
        }

        public IDisposable? Watch(Action onChanged)
        {
            Called.Add(nameof(Watch));

            return new Nothing();
        }

        public Task<(string Name, byte[] Contents)?> OpenAsync()
        {
            Called.Add(nameof(OpenAsync));

            return Task.FromResult<(string, byte[])?>((Name, [1]));
        }

        public Task<bool> CanOpenAsync()
        {
            Called.Add(nameof(CanOpenAsync));

            return Task.FromResult(true);
        }

        public bool SavesInPlace => true;

        public string SaveVerb => "Save";

        public Task WarnBeforeLeavingAsync(bool unsaved)
        {
            Called.Add(nameof(WarnBeforeLeavingAsync));

            return Task.CompletedTask;
        }

        public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
        {
            Called.Add(nameof(TryOpenExistingAsync));

            return Task.FromResult<(string, byte[])?>((Name, [2]));
        }

        public Task<bool> CopyToClipboardAsync(string text)
        {
            Called.Add(nameof(CopyToClipboardAsync));

            return Task.FromResult(true);
        }

        public string? ShareBaseUri => "https://" + Name;

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// Every member of the interface is implemented here rather than inherited from it.
    /// </summary>
    /// <remarks>
    /// The test that will still be doing work in a year. It does not check what any member
    /// forwards - the ones below do that - only that none is left to the interface's own default,
    /// which is what a member added to <see cref="IFileExchange"/> tomorrow would silently be.
    /// </remarks>
    [Fact]
    public void NoMemberIsLeftToTheInterfacesDefault()
    {
        var map = typeof(FileExchangeRouter).GetInterfaceMap(typeof(IFileExchange));

        var inherited = map.InterfaceMethods
            .Zip(map.TargetMethods)
            .Where(pair => pair.Second.DeclaringType != typeof(FileExchangeRouter))
            .Select(pair => pair.First.Name)
            .ToList();

        Assert.Empty(inherited);
    }

    /// <summary>And the count is what the interface actually declares, so nothing is missed.</summary>
    [Fact]
    public void TheRouterCoversTheWholeInterface()
    {
        var declared = typeof(IFileExchange)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<MethodInfo>()
            .Count();

        Assert.Equal(declared, typeof(FileExchangeRouter).GetInterfaceMap(typeof(IFileExchange)).InterfaceMethods.Length);
    }

    private static (FileExchangeRouter Router, Loud First, Loud Second) Built()
    {
        var first = new Loud("first");

        return (new FileExchangeRouter(first), first, new Loud("second"));
    }

    /// <summary>Everything reaches the exchange behind it, and nothing answers for itself.</summary>
    [Fact]
    public async Task EveryCallReachesTheExchangeBehindIt()
    {
        var (router, first, _) = Built();
        var files = (IFileExchange)router;

        Assert.Equal(SaveOutcome.Cancelled, await files.SaveAsync("f", [1]));
        Assert.Equal(SaveOutcome.Saved, await files.SaveAsync("f", [1], backUp: true));
        Assert.Equal(SaveOutcome.Downloaded, await files.SaveAsync("f", [1], backUp: false));
        Assert.Equal(SaveOutcome.Saved, await files.ExportAsync("f", [1], ExportKind.Image));
        Assert.NotNull(files.Watch(() => { }));
        Assert.Equal("first", (await files.OpenAsync())?.Name);
        Assert.True(await files.CanOpenAsync());
        Assert.True(files.SavesInPlace);
        Assert.Equal("Save", files.SaveVerb);
        await files.WarnBeforeLeavingAsync(true);
        Assert.Equal("first", (await files.TryOpenExistingAsync())?.Name);
        Assert.True(await files.CopyToClipboardAsync("x"));
        Assert.Equal("https://first", files.ShareBaseUri);

        Assert.Equal(
            [
                "SaveAsync", "SaveAsync3", "SaveAsync3", "ExportAsync", "Watch", "OpenAsync",
                "CanOpenAsync", "WarnBeforeLeavingAsync", "TryOpenExistingAsync", "CopyToClipboardAsync",
            ],
            first.Called);
    }

    /// <summary>Switching sends everything somewhere else, and says that it has.</summary>
    [Fact]
    public async Task SwitchingRedirectsEverythingAndAnnouncesIt()
    {
        var (router, first, second) = Built();
        var told = 0;
        router.Changed += () => told++;

        router.SwitchTo(second);

        Assert.Equal(1, told);
        Assert.Same(second, router.Current);
        Assert.Equal("second", (await ((IFileExchange)router).TryOpenExistingAsync())?.Name);
        Assert.Empty(first.Called);
    }

    /// <summary>Switching to the one already there is not a change, and is not announced.</summary>
    [Fact]
    public void SwitchingToTheSameOneSaysNothing()
    {
        var (router, first, _) = Built();
        var told = 0;
        router.Changed += () => told++;

        router.SwitchTo(first);

        Assert.Equal(0, told);
    }
}
