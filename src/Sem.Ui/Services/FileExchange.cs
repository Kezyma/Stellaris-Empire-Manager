using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// Hands a finished file back to the user.
/// </summary>
/// <remarks>
/// The web app cannot write to disk, so the browser is asked to offer a download. The desktop app
/// replaces this with one that saves in place, which is the whole difference between the two as
/// far as the designer is concerned.
/// </remarks>
public interface IFileExchange
{
    /// <summary>Gives the user a file under a suggested name.</summary>
    Task SaveAsync(string fileName, byte[] contents);

    /// <summary>
    /// Whether <see cref="SaveAsync"/> writes back over the file the session was opened from.
    /// </summary>
    /// <remarks>
    /// True on the desktop, where saving means the player's real designs file is replaced, and that
    /// is what the Save button should do. False in a browser, where the same call hands over a
    /// download instead — which is a thing to offer, but not a thing to do every time somebody
    /// presses Save.
    /// </remarks>
    bool SavesInPlace => false;

    /// <summary>
    /// What to call the button that hands the file over, which is a different act on each host: the
    /// desktop writes the player's own file, and a browser can only offer a copy to download.
    /// </summary>
    string SaveVerb => SavesInPlace ? "Save file" : "Download";

    /// <summary>
    /// Asks the host to warn before the page is closed with work not yet saved.
    /// </summary>
    /// <remarks>
    /// Only a browser can do anything here, and even then only by asking: the wording of the warning
    /// belongs to the browser, and it will not show one at all unless the page has been interacted
    /// with. Moving between the app's own pages is caught in the app instead, where a proper
    /// question can be asked.
    /// </remarks>
    Task WarnBeforeLeavingAsync(bool unsaved) => Task.CompletedTask;

    /// <summary>
    /// The file this host already knows about, if any.
    /// </summary>
    /// <remarks>
    /// The desktop app knows where the player's designs live and opens them straight away. A
    /// browser cannot know, and must wait to be handed one.
    /// </remarks>
    Task<(string Name, byte[] Contents)?> TryOpenExistingAsync() =>
        Task.FromResult<(string, byte[])?>(null);

    /// <summary>
    /// Puts text on the clipboard, saying whether it got there.
    /// </summary>
    /// <remarks>
    /// A browser may refuse — the clipboard needs a secure context and a recent gesture — and the
    /// answer decides whether the button claims success.
    /// </remarks>
    Task<bool> CopyToClipboardAsync(string text) => Task.FromResult(false);

    /// <summary>
    /// The address a shared link should be built against, or null to use the app's own.
    /// </summary>
    /// <remarks>
    /// In a browser the app's own address is the one to share, and this stays null. A desktop window
    /// has no address anyone else can open — the web view serves the app from an origin of its own —
    /// so a link built from it was a link to nowhere, handed over as though it worked.
    /// </remarks>
    string? ShareBaseUri => null;
}

/// <summary>Offers the file as a browser download.</summary>
public sealed class BrowserFileExchange(IJSRuntime js) : IFileExchange, IAsyncDisposable
{
    private readonly IJSRuntime _js = js ?? throw new ArgumentNullException(nameof(js));
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async Task SaveAsync(string fileName, byte[] contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contents);

        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").ConfigureAwait(false);

        await _module.InvokeVoidAsync("saveFile", fileName, contents).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").ConfigureAwait(false);

        return await _module.InvokeAsync<bool>("copyText", text).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WarnBeforeLeavingAsync(bool unsaved)
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").ConfigureAwait(false);

        await _module.InvokeVoidAsync("warnBeforeLeaving", unsaved).ConfigureAwait(false);
    }

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
