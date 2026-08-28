namespace Sem.Ui.Services;

/// <summary>
/// Keeps the one working session alive across pages, so moving between the empire list and the
/// designer does not reload the game data or lose unsaved changes.
/// </summary>
public sealed class SessionHost(IGameDataSource source)
{
    private readonly IGameDataSource _source = source ?? throw new ArgumentNullException(nameof(source));
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
            session.StartEmptyFile();

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
}
