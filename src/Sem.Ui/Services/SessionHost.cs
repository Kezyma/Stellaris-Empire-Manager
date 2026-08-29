using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Keeps the one working session alive across pages, so moving between the empire list and the
/// designer does not reload the game data or lose unsaved changes.
/// </summary>
/// <param name="assumeAllPacks">
/// Whether to open with every content pack enabled. True on the web, where the installation the
/// data was read from is not the player's; false on the desktop, where it is.
/// </param>
public sealed class SessionHost(
    IGameDataSource source,
    IFileExchange files,
    IDesignStore? store = null,
    bool assumeAllPacks = false)
{
    private readonly IGameDataSource _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly IFileExchange _files = files ?? throw new ArgumentNullException(nameof(files));
    private readonly IDesignStore _store = store ?? new NoDesignStore();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DesignSession? _session;

    /// <summary>The session, once it has been opened.</summary>
    public DesignSession? Current => _session;

    /// <summary>What went wrong loading the game data, if anything did.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Opens the session, loading the game data the first time it is asked for.</summary>
    public async Task<DesignSession?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null)
        {
            return _session;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_session is not null)
            {
                return _session;
            }

            var data = await _source.LoadAsync(cancellationToken).ConfigureAwait(false);
            var session = new DesignSession(data, assumeAllPacks);

            // The desktop app knows where the player's designs are and opens them. A browser has to
            // be handed one — but it may have been handed one before, so what it kept is opened
            // rather than starting empty and losing an evening's work to a closed tab.
            if (await TryOpenExistingAsync().ConfigureAwait(false) is { } existing)
            {
                session.Load(existing.Contents, existing.Name);
            }
            else if (await _store.ReadAsync().ConfigureAwait(false) is { Length: > 0 } kept)
            {
                session.LoadText(kept, EmpireDesignsFile.FileName);
            }
            else
            {
                session.StartEmptyFile();
            }

            // Kept from here on, so that what is in the tab and what is in the browser's store stay
            // the same thing. Opening a file replaces the store, which is what opening a file means.
            session.Changed += Keep;

            _session = session;
            LoadError = null;
            return _session;
        }
        catch (Exception ex)
        {
            // Usually the extracted data missing from the site, which has a specific fix. Anything
            // else is caught too: a designer that says what went wrong beats one that spins.
            LoadError = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Writes the whole designs file back to the store whenever anything changes.
    /// </summary>
    /// <remarks>
    /// Not awaited: this runs from a change notification during a render, and a save that takes a
    /// moment must not hold one up. Failures are the store's own business and it swallows them.
    /// </remarks>
    private void Keep()
    {
        if (_session?.Save() is { } bytes)
        {
            _ = _store.WriteAsync(System.Text.Encoding.UTF8.GetString(bytes));
        }
    }

    /// <summary>
    /// Opens the host's existing file when it has one. A file that cannot be read leaves the
    /// session empty with the reason recorded, rather than stopping the app from starting.
    /// </summary>
    private async Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
    {
        try
        {
            return await _files.TryOpenExistingAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Clausewitz.CwSyntaxException)
        {
            LoadError = $"Your empire designs file could not be read: {ex.Message}";
            return null;
        }
    }
}
