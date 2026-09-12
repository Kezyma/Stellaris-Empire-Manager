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
    private Task<IJSObjectReference>? _module;

    /// <inheritdoc />
    public async Task<string?> ReadAsync()
    {
        try
        {
            return await (await ModuleAsync().ConfigureAwait(false))
                .InvokeAsync<string?>("readStored", Key)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // Storage can be switched off or full. Losing what was kept is a shame; refusing to
            // start the app over it is worse. A runtime that has gone away is caught with it -
            // JSDisconnectedException does not derive from JSException, which is why DisposeAsync
            // below names it separately.
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
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // Storage switched off, or full, or a runtime that has gone away. The player is told,
            // since they asked for this one - and this is now the path Export reports through, so
            // anything escaping here reaches them as an unhandled failure instead of a sentence.
            return false;
        }
    }

    /// <summary>
    /// The script module, imported once however many callers ask for it at once.
    /// </summary>
    /// <remarks>
    /// The import is held as the task rather than its result. Written as
    /// <c>_module ??= await Import()</c> the check and the assignment sat either side of an await,
    /// so a second caller arriving during the import saw no module and started another - and the
    /// reference the loser assigned was overwritten and never released. Holding the task closes it
    /// without a lock: there is nothing to yield to between the test and the store.
    /// </remarks>
    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
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
                // The import never succeeded, so it left nothing to release. Reachable only now
                // that the task is what is held: a failed import used to leave the field null.
            }
        }
    }
}
