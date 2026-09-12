using Microsoft.JSInterop;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// Somewhere to keep something that must not outlive the tab.
/// </summary>
/// <remarks>
/// An interface for one reason: the sign-in flow below is worth testing, and a test has no
/// sessionStorage. It is deliberately not <see cref="IDesignStore"/> or <see cref="Preferences"/> -
/// both of those keep things on purpose, between visits, and this is the opposite promise.
/// </remarks>
public interface ISessionStore
{
    /// <summary>What is filed under a key, or null when nothing is.</summary>
    Task<string?> ReadAsync(string key);

    /// <summary>Files something, or forgets it when the value is null.</summary>
    Task WriteAsync(string key, string? value);
}

/// <summary>The browser's own sessionStorage, which it empties when the tab closes.</summary>
public sealed class BrowserSessionStore(IJSRuntime js) : ISessionStore, IAsyncDisposable
{
    private readonly IJSRuntime _js = js ?? throw new ArgumentNullException(nameof(js));
    private Task<IJSObjectReference>? _module;

    /// <summary>
    /// The script module, imported once however many callers ask for it at once.
    /// </summary>
    /// <remarks>
    /// The import is held as the task rather than its result, for the reason the other three
    /// services that do this record: written as <c>_module ??= await Import()</c> the check and the
    /// assignment sit either side of an await, so a second caller arriving during the import starts
    /// another, and the reference the loser assigns is overwritten and never released.
    /// </remarks>
    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").AsTask();

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            return await (await ModuleAsync().ConfigureAwait(false))
                .InvokeAsync<string?>("readSession", key).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage can be switched off. Nothing kept means signing in again, which works.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            await (await ModuleAsync().ConfigureAwait(false))
                .InvokeVoidAsync("writeSession", key, value).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // As above, and worse here: a sign-in that cannot keep its verifier will fail on the
            // way back instead. Reported there, where there is somebody waiting to be told.
        }
    }

    /// <summary>Hands the imported module back, if one was ever imported.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await (await _module.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The page went away first; there is nothing left to release.
        }
        catch (JSException)
        {
            // The import never succeeded, so it left nothing to release.
        }
    }
}

/// <summary>Keeps nothing anywhere, for a test or a host with no browser under it.</summary>
public sealed class NoSessionStore : ISessionStore
{
    private readonly Dictionary<string, string> _held = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string?> ReadAsync(string key) => Task.FromResult(_held.GetValueOrDefault(key));

    /// <inheritdoc />
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
