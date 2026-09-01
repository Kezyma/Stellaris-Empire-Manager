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
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
        throw new InvalidOperationException(
            "The Stellaris game data folder could not be found, so there is nowhere to save. " +
            "Run Stellaris once to create it.");
}
