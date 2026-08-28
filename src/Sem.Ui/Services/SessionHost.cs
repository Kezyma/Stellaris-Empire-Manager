namespace Sem.Ui.Services;

/// <summary>
/// Keeps the one working session alive across pages, so moving between the empire list and the
/// designer does not reload the game data or lose unsaved changes.
/// </summary>
public sealed class SessionHost(IGameDataSource source, IFileExchange files)
{
    private readonly IGameDataSource _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly IFileExchange _files = files ?? throw new ArgumentNullException(nameof(files));
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
            var session = new DesignSession(data);

            // The desktop app knows where the player's designs are and opens them; a browser has
            // to be handed one, so it starts empty.
            if (await TryOpenExistingAsync().ConfigureAwait(false) is { } existing)
            {
                session.Load(existing.Contents, existing.Name);
            }
            else
            {
                session.StartEmptyFile();
            }

            _session = session;
            LoadError = null;
            return _session;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // Almost always the extracted data missing from the site, which has a specific fix.
            LoadError = ex.Message;
            return null;
        }
        finally
        {
            _gate.Release();
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
