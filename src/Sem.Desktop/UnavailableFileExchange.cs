using Sem.Ui.Services;

namespace Sem.Desktop;

/// <summary>
/// Stands in when no designs file could be found, so the designer still runs and the failure is a
/// clear message rather than a crash on the first attempt to save.
/// </summary>
public sealed class UnavailableFileExchange : IFileExchange
{
    /// <inheritdoc />
    public bool SavesInPlace => true;

    /// <inheritdoc />
    public string SaveVerb => "Save";

    /// <summary>Whether the editor is holding work nobody has saved.</summary>
    /// <remarks>
    /// Kept here as well as on the real exchange, because the window asks whichever one is in place
    /// before it closes. It used to ask only the real one by type, so a player who had installed
    /// Stellaris but never launched it - no designs folder, so this stand-in is what is registered -
    /// could build an empire and close the window on it without being asked anything. They can still
    /// reach that work through Export, which makes it real rather than theoretical.
    /// </remarks>
    public bool HasUnsavedWork { get; private set; }

    /// <inheritdoc />
    public Task WarnBeforeLeavingAsync(bool unsaved)
    {
        HasUnsavedWork = unsaved;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Where a shared link has to point, which is a question about the host and not about the file.
    /// </summary>
    /// <remarks>
    /// Inherited from the interface, this answered null, and the share button then built a link
    /// against the web view's own origin - an address only this process can reach. The designs file
    /// being missing has nothing to do with it, so the answer is the real one's.
    /// </remarks>
    public string? ShareBaseUri => DesktopFileExchange.PublishedSite;

    /// <summary>
    /// The clipboard works whether or not a designs file was found, and inheriting the interface's
    /// "did not work" made the share button report a failure it had not had.
    /// </summary>
    public Task<bool> CopyToClipboardAsync(string text) => DesktopFileExchange.CopyAsync(text);

    /// <summary>
    /// Handing over a separate file works whether or not a designs file was found, since it writes
    /// wherever the player says rather than back over anything.
    /// </summary>
    public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind) =>
        DesktopFileExchange.ExportFileAsync(fileName, contents, kind);

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
        throw new InvalidOperationException(
            "The Stellaris game data folder could not be found, so there is nowhere to save. " +
            "Run Stellaris once to create it.");
}
