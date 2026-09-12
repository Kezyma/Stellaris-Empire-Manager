using Microsoft.JSInterop;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// Where a session with a cloud provider is kept.
/// </summary>
/// <remarks>
/// An interface for one reason: the sign-in flow is worth testing, and a test has no browser
/// storage. Kept apart from <see cref="Preferences"/> even though both end up in the same place,
/// because one holds answers about how the app is arranged and this holds a credential, and the
/// two should be findable separately when something has to be cleared.
/// </remarks>
public interface ITokenStore
{
    /// <summary>What is filed under a key, or null when nothing is.</summary>
    Task<string?> ReadAsync(string key);

    /// <summary>Files something, or forgets it when the value is null.</summary>
    Task WriteAsync(string key, string? value);
}

/// <summary>
/// The browser's own localStorage, so a sign-in outlives the tab it was made in.
/// </summary>
/// <remarks>
/// sessionStorage was the first answer and is the safer one: the browser empties it by itself, so
/// nothing is left on the machine once somebody has finished. It was changed because the cost
/// landed on every visit. A new tab, or a browser reopened, meant signing in again to reach a file
/// the app was supposed to already be connected to - and being asked to prove yourself to
/// something that ought to remember you is what gets a feature switched off.
///
/// So the trade is written down rather than hidden. The refresh token now sits on the machine
/// until it is disconnected or site data is cleared, which means anything able to run script on
/// this origin can read it. That is what the Content-Security-Policy and the escaping tests are
/// for, and why disconnecting removes it before anything else.
/// </remarks>
public sealed class BrowserTokenStore(IJSRuntime js) : ITokenStore, IAsyncDisposable
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
                .InvokeAsync<string?>("readStored", key).ConfigureAwait(false);
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
                .InvokeVoidAsync("writeStored", key, value).ConfigureAwait(false);
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

/// <summary>Keeps it in memory only, for a test or a host with no browser under it.</summary>
public sealed class NoTokenStore : ITokenStore
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
