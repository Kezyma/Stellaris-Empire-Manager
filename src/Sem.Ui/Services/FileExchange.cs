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
