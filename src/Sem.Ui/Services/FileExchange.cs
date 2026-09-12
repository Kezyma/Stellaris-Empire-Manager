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

    /// <summary>
    /// The file had moved on since it was read, so nothing was written over it.
    /// </summary>
    /// <remarks>
    /// Only a host that can tell says this, which means one that keeps a version alongside the file
    /// and promises not to write past it - a cloud provider. A disk cannot: two programs writing the
    /// same file simply both write it, and the second wins. So this is not a failure to save, it is
    /// a refusal to overwrite somebody, and the answer to it is to look at what arrived rather than
    /// to try again.
    /// </remarks>
    Conflicted,

    /// <summary>
    /// The host has no session to write with any more, so nothing was attempted.
    /// </summary>
    /// <remarks>
    /// A sign-in that has expired or been revoked, which only a host that signs in can have. Worth
    /// its own answer because it is the one failure with an obvious remedy: nothing is wrong with
    /// the file or the work, and connecting again fixes it. Reported as an ordinary refusal it read
    /// as "your empires were not written" with no hint that a button would put it right.
    /// </remarks>
    SignedOut,
}

/// <summary>
/// A kind of file that is not the designs file, with what a save dialog needs to offer it.
/// </summary>
/// <param name="MediaType">The MIME type, which is what a browser picker asks for.</param>
/// <param name="Extension">The suffix, with its dot.</param>
/// <param name="Description">What to call this kind of file in a dialog.</param>
public readonly record struct ExportKind(string MediaType, string Extension, string Description)
{
    /// <summary>Part of a collection of empires, in the game's own format.</summary>
    public static ExportKind Designs { get; } =
        new("text/plain", ".txt", "Stellaris empire designs");

    /// <summary>A picture of an empire.</summary>
    public static ExportKind Image { get; } = new("image/png", ".png", "PNG image");
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
    /// The same save, saying whether to keep a copy of what is being replaced.
    /// </summary>
    /// <remarks>
    /// Only a host that replaces a file has anything to keep, which is why this defaults to the
    /// call above and not the other way round: a browser save writes where the player pointed it
    /// and overwrites nothing they did not name, so there is nothing there to ask about.
    /// </remarks>
    Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp) =>
        SaveAsync(fileName, contents);

    /// <summary>
    /// Hands over a file that is not the designs file, under a name and a kind of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <see cref="SaveAsync(string, byte[])"/> with a different name. On the desktop that call
    /// means "replace the player's designs file", and it means it whatever name it is given - it
    /// holds the path and writes there. So a selection of empires, or a picture of one, sent through
    /// it would have replaced a file full of hand-built empires with a fragment of itself or a PNG.
    /// </para>
    /// <para>
    /// A host with no way to offer a separate file refuses rather than falling back to the other
    /// call, for the same reason.
    /// </para>
    /// </remarks>
    Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind) =>
        Task.FromResult(SaveOutcome.Refused);

    /// <summary>
    /// Watches the file this host saves in place, and says so when anything else writes it.
    /// </summary>
    /// <param name="onChanged">
    /// Called on whichever thread the app renders on, once the writing has settled, however many
    /// times the file system actually reported it.
    /// </param>
    /// <returns>Something to dispose to stop watching, or null where there is nothing to watch.</returns>
    /// <remarks>
    /// Only the desktop has a file of its own to watch, and only the desktop has a second writer to
    /// watch for: the game writes this file every time an empire is created in it. A browser has
    /// neither, and returns null, which is how the header knows not to offer the choice at all.
    /// </remarks>
    IDisposable? Watch(Action onChanged) => null;

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
    /// Whether <see cref="SaveAsync(string, byte[])"/> writes back over the file the session was
    /// opened from.
    /// </summary>
    /// <remarks>
    /// True on the desktop, where saving means the player's real designs file is replaced, and that
    /// is what the Save button should do. False in a browser, where the same call may reach a file
    /// and may not - the player has to be shown a dialog and may dismiss it, and there are browsers
    /// with no dialog to show. Whether one save actually landed is a different question, and
    /// <see cref="SaveAsync(string, byte[])"/> answers that one.
    /// </remarks>
    bool SavesInPlace => false;

    /// <summary>
    /// What the button that hands the file over should be called on this host.
    /// </summary>
    /// <remarks>
    /// "Export" is honest in a browser, where the player is offered a file and chooses where it
    /// lands. On the desktop the same button replaces the designs file the game reads, in place, and
    /// calling that Export described the wrong action entirely - the one host where the button is
    /// destructive was the one host whose label said it was not.
    /// </remarks>
    string SaveVerb => "Export";

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
    private Task<IJSObjectReference>? _module;

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
    public async Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contents);

        var module = await ModuleAsync().ConfigureAwait(false);

        var answer = await module.InvokeAsync<string>(
            "exportFile", fileName, contents, kind.MediaType, kind.Extension, kind.Description)
            .ConfigureAwait(false);

        switch (answer)
        {
            case "saved":
                return SaveOutcome.Saved;

            case "cancelled":
                return SaveOutcome.Cancelled;
        }

        await module.InvokeVoidAsync("saveFile", fileName, contents, kind.MediaType)
            .ConfigureAwait(false);

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

    /// <summary>Hands the imported script module back, if one was ever imported.</summary>
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
