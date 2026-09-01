using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// What became of an attempt to hand the file back.
/// </summary>
/// <remarks>
/// A boolean said only whether the work was still outstanding, which is true of three of these four
/// and for entirely different reasons. The player who dismissed a dialog needs to be told nothing;
/// the player whose browser has no dialog needs to be told where their file went, and why it is not
/// where they expected.
/// </remarks>
public enum SaveOutcome
{
    /// <summary>The bytes are in a file the player named. The only outcome that settles anything.</summary>
    Saved,

    /// <summary>The player dismissed the save dialog. Nothing was written and nothing downloaded.</summary>
    Cancelled,

    /// <summary>This host has no save dialog, so a copy was downloaded instead.</summary>
    Downloaded,

    /// <summary>A dialog exists but the browser would not open it or would not write. Downloaded instead.</summary>
    Refused,
}

/// <summary>
/// Hands a finished file back to the user.
/// </summary>
/// <remarks>
/// Neither host writes the file the same way. The desktop app has the path and replaces the file in
/// place. A browser cannot reach the disk at all, so it asks for a save dialog and is handed a
/// writer for the one file the player names in it - and where there is no dialog to ask for, it
/// falls back to offering a download, which is a copy in the downloads folder and settles nothing.
/// </remarks>
public interface IFileExchange
{
    /// <summary>
    /// Gives the user a file under a suggested name, saying what became of it.
    /// </summary>
    /// <remarks>
    /// Only <see cref="SaveOutcome.Saved"/> means the bytes are in a file the player named. The
    /// other three all leave the work outstanding, and the caller needs to tell them apart: a
    /// dismissed dialog wants no response at all, and a download wants explaining.
    /// </remarks>
    Task<SaveOutcome> SaveAsync(string fileName, byte[] contents);

    /// <summary>
    /// Asks the player for a file, where the host has a way to ask.
    /// </summary>
    /// <remarks>
    /// The desktop opens the player's own designs without asking, so it has no use for this. A
    /// browser that has a file picker uses it, and one that has not returns null so the caller can
    /// fall back to the file input that is still in the page.
    /// </remarks>
    Task<(string Name, byte[] Contents)?> OpenAsync() =>
        Task.FromResult<(string, byte[])?>(null);

    /// <summary>
    /// Whether <see cref="OpenAsync"/> has anything to offer, so the caller can draw the right
    /// control before anybody presses it.
    /// </summary>
    Task<bool> CanOpenAsync() => Task.FromResult(false);

    /// <summary>
    /// Whether <see cref="SaveAsync"/> writes back over the file the session was opened from.
    /// </summary>
    /// <remarks>
    /// True on the desktop, where saving means the player's real designs file is replaced, and that
    /// is what the Save button should do. False in a browser, where the same call may reach a file
    /// and may not - the player has to be shown a dialog and may dismiss it, and there are browsers
    /// with no dialog to show. Whether one save actually landed is a different question, and
    /// <see cref="SaveAsync"/> answers that one.
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

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").ConfigureAwait(false);

    /// <inheritdoc />
    /// <summary>
    /// Offers the file back to the player, through a save dialog where the browser has one.
    /// </summary>
    /// <remarks>
    /// A dialog rather than a download, because a download lands in the downloads folder and the
    /// player then has to know that a designs file belongs somewhere else entirely. The dialog opens
    /// where they last opened one, which after a single import is their Stellaris folder.
    /// </remarks>
    public async Task<SaveOutcome> SaveAsync(string fileName, byte[] contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contents);

        var module = await ModuleAsync().ConfigureAwait(false);
        var answer = await module.InvokeAsync<string>("saveDesignsFile", fileName, contents)
            .ConfigureAwait(false);

        switch (answer)
        {
            case "saved":
                return SaveOutcome.Saved;

            // The player closed the dialog. Downloading anyway would hand them the file they had
            // just declined to be given, into a folder they did not choose.
            case "cancelled":
                return SaveOutcome.Cancelled;
        }

        // No picker in this browser, or one that would not open. A download is the older, weaker
        // answer and is still the only one some browsers have, so it stays as the fallback - but it
        // hands over a copy, which settles nothing about the file the session came from, and the
        // player is told so rather than left to find it in their downloads.
        await module.InvokeVoidAsync("saveFile", fileName, contents).ConfigureAwait(false);

        return answer == "refused" ? SaveOutcome.Refused : SaveOutcome.Downloaded;
    }

    /// <inheritdoc />
    public async Task<bool> CanOpenAsync() =>
        await (await ModuleAsync().ConfigureAwait(false))
            .InvokeAsync<bool>("canPickFiles").ConfigureAwait(false);

    /// <summary>Asks the player for a file, where the browser has a picker to ask with.</summary>
    public async Task<(string Name, byte[] Contents)?> OpenAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        var chosen = await module.InvokeAsync<PickedFile?>("openDesignsFile").ConfigureAwait(false);

        return chosen is { Name: { Length: > 0 } name, Bytes: { } bytes }
            ? (name, bytes)
            : null;
    }

    /// <summary>What the picker hands back, which is a file's name and its contents.</summary>
    private sealed record PickedFile(string Name, byte[] Bytes);

    /// <inheritdoc />
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var module = await ModuleAsync().ConfigureAwait(false);

        return await module.InvokeAsync<bool>("copyText", text).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WarnBeforeLeavingAsync(bool unsaved)
    {
        var module = await ModuleAsync().ConfigureAwait(false);

        await module.InvokeVoidAsync("warnBeforeLeaving", unsaved).ConfigureAwait(false);
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
