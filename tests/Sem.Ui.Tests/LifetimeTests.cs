using Microsoft.JSInterop;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the services hold on to, and whether they hand it back.
/// </summary>
/// <remarks>
/// Every one of these was written as <c>_module ??= await Import()</c>, which reads as though it
/// imports once and does not: the check and the assignment sit either side of an await, so a second
/// caller arriving while the first is still importing sees no module and starts another. The loser's
/// reference is then overwritten and never released - a handle held open in the browser for the rest
/// of the session, once per race.
/// </remarks>
public sealed class LifetimeTests
{
    [Fact]
    public async Task TwoCallersAtOnceImportTheScriptOnce()
    {
        var js = new CountingRuntime();
        await using var store = new BrowserDesignStore(js);

        // Both start before either finishes, which is the whole of the bug.
        var first = store.ReadAsync();
        var second = store.ReadAsync();

        js.LetImportsFinish();
        await Task.WhenAll(first, second);

        Assert.Equal(1, js.Imports);
    }

    [Fact]
    public async Task TheFileExchangeImportsOnceToo()
    {
        var js = new CountingRuntime();
        await using var files = new BrowserFileExchange(js);

        var first = files.CanOpenAsync();
        var second = files.CanOpenAsync();

        js.LetImportsFinish();
        await Task.WhenAll(first, second);

        Assert.Equal(1, js.Imports);
    }

    /// <summary>
    /// The module that was imported is the one released, and only once.
    /// </summary>
    [Fact]
    public async Task TheImportedModuleIsReleasedOnDisposal()
    {
        var js = new CountingRuntime();
        var store = new BrowserDesignStore(js);

        js.LetImportsFinish();
        _ = await store.ReadAsync();

        await store.DisposeAsync();

        Assert.Equal(1, js.Imports);
        Assert.Equal(1, js.Modules.Count(m => m.Disposals == 1));
    }

    /// <summary>
    /// An import that never succeeded leaves nothing to release, and saying so must not throw.
    /// </summary>
    /// <remarks>
    /// Only reachable now that the task is what is held rather than its result: a failed import used
    /// to leave the field null, so disposal skipped it without having to think about it.
    /// </remarks>
    [Fact]
    public async Task AFailedImportIsNotAFailureToDisposeOf()
    {
        var js = new CountingRuntime { FailImports = true };
        var store = new BrowserDesignStore(js);

        js.LetImportsFinish();
        _ = await store.ReadAsync();

        await store.DisposeAsync();
    }

    /// <summary>
    /// Both of these hold a <see cref="SemaphoreSlim"/>, which holds a wait handle from the first
    /// wait onwards, and both are registered per page in the browser and per window on the desktop.
    /// </summary>
    [Fact]
    public void TheGateHoldersCanBeDisposedOf()
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://example.invalid/") };
        using var source = new HttpGameDataSource(client);
        using var host = new SessionHost(source, new NoFileExchange());

        Assert.IsAssignableFrom<IDisposable>(source);
        Assert.IsAssignableFrom<IDisposable>(host);
    }

    /// <summary>A file exchange with nowhere to put anything, for a host that only needs one.</summary>
    private sealed class NoFileExchange : IFileExchange
    {
        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            Task.FromResult(SaveOutcome.Refused);
    }

    /// <summary>
    /// A runtime that counts imports and can be made to hold them open, so two callers really do
    /// overlap rather than running one after the other.
    /// </summary>
    private sealed class CountingRuntime : IJSRuntime
    {
        private readonly TaskCompletionSource _gate = new();

        public int Imports { get; private set; }

        public bool FailImports { get; init; }

        public List<CountingModule> Modules { get; } = [];

        public void LetImportsFinish() => _gate.TrySetResult();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier != "import")
            {
                return default!;
            }

            Imports++;
            await _gate.Task.ConfigureAwait(false);

            if (FailImports)
            {
                throw new JSException("no such module");
            }

            var module = new CountingModule();
            Modules.Add(module);

            return (TValue)(object)module;
        }
    }

    /// <summary>A module reference that remembers how often it was released.</summary>
    private sealed class CountingModule : IJSObjectReference
    {
        public int Disposals { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult<TValue>(default!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            ValueTask.FromResult<TValue>(default!);

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
