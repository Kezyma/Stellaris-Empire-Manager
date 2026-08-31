using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// Where the browser keeps a player's empires between visits.
/// </summary>
/// <remarks>
/// The web app has nowhere to put a file. Without this, closing the tab loses everything not
/// downloaded first, which is a poor bargain for someone who spent an hour on an empire. The
/// designs file is a few tens of kilobytes of text against a budget of several megabytes, so the
/// whole thing is kept rather than a summary of it.
/// </remarks>
public interface IDesignStore
{
    /// <summary>The designs file kept from a previous visit, if there is one.</summary>
    Task<string?> ReadAsync() => Task.FromResult<string?>(null);

    /// <summary>
    /// Keeps a designs file for next time, saying whether it got there.
    /// </summary>
    /// <remarks>
    /// The answer matters now that saving is something the player asks for: a button that says
    /// "Saved" over a store that quietly refused would be worse than one that admits it.
    /// </remarks>
    Task<bool> WriteAsync(string contents) => Task.FromResult(false);
}

/// <summary>Keeps nothing, for a host with a real file of its own.</summary>
/// <remarks>
/// The desktop app reads and writes the player's actual designs file. Keeping a second copy in the
/// embedded browser would be a second source of truth, and the first one is authoritative.
/// </remarks>
public sealed class NoDesignStore : IDesignStore;

/// <summary>Keeps the designs file in the browser's local storage.</summary>
public sealed class BrowserDesignStore(IJSRuntime js) : IDesignStore, IAsyncDisposable
{
    /// <summary>What the file is filed under, versioned so a change of shape cannot be misread.</summary>
    private const string Key = "sem.designs.v1";

    private readonly IJSRuntime _js = js ?? throw new ArgumentNullException(nameof(js));
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async Task<string?> ReadAsync()
    {
        try
        {
            return await (await ModuleAsync().ConfigureAwait(false))
                .InvokeAsync<string?>("readStored", Key)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage can be switched off or full. Losing what was kept is a shame; refusing to
            // start the app over it is worse.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> WriteAsync(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        try
        {
            await (await ModuleAsync().ConfigureAwait(false))
                .InvokeVoidAsync("writeStored", Key, contents)
                .ConfigureAwait(false);

            return true;
        }
        catch (JSException)
        {
            // Storage switched off, or full. The player is told, since they asked for this one.
            return false;
        }
    }

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // The page went away first; there is nothing left to release.
            }
        }
    }
}
