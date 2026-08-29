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

    /// <summary>How saving is described to the user, which differs between the two hosts.</summary>
    string SaveVerb { get; }

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
}

/// <summary>Offers the file as a browser download.</summary>
public sealed class BrowserFileExchange(IJSRuntime js) : IFileExchange, IAsyncDisposable
{
    private readonly IJSRuntime _js = js ?? throw new ArgumentNullException(nameof(js));
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public string SaveVerb => "Download";

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
