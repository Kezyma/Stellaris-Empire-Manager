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
    bool assumeAllPacks = false,
    Preferences? preferences = null)
{
    private readonly IGameDataSource _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly IFileExchange _files = files ?? throw new ArgumentNullException(nameof(files));
    private readonly IDesignStore _store = store ?? new NoDesignStore();
    private readonly Preferences _preferences = preferences ?? new Preferences();
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

            // Before the session exists, so the first render of a picker is already the way the
            // player left it rather than the default flipping into it a moment later.
            await _preferences.LoadAsync().ConfigureAwait(false);

            var session = new DesignSession(data, assumeAllPacks, _preferences);

            // The desktop app knows where the player's designs are and opens them. A browser has to
            // be handed one — but it may have been handed one before, so what it kept is opened
            // rather than starting empty and losing an evening's work to a closed tab.
            if (await TryOpenExistingAsync().ConfigureAwait(false) is { } existing)
            {
                session.Load(existing.Contents, existing.Name);
            }
            else if (await _store.ReadAsync().ConfigureAwait(false) is { Length: > 0 } kept)
            {
                if (Kept.TryDecode(kept) is { } bytes)
                {
                    session.Load(bytes, EmpireDesignsFile.FileName);
                }
                else
                {
                    // Kept before the store held bytes. Read as text, and written back as bytes the
                    // next time the list changes.
                    session.LoadText(kept, EmpireDesignsFile.FileName);
                }
            }
            else
            {
                session.StartEmptyFile();
            }

            // The list keeps itself: an empire added, duplicated or deleted is a decision already
            // taken, and there is no Save button in front of it. Editing one is the thing that
            // waits, and that goes through SaveAsync below.
            session.FileChanged += Keep;

            _session = session;
            LoadError = null;
            return _session;
        }
        catch (OperationCanceledException)
        {
            // Whoever asked has gone. Reported as a failure this looked like the data was broken,
            // when nothing was wrong except that the page moved on.
            throw;
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
    /// Keeps the file in the browser's store when the list of empires changes.
    /// </summary>
    /// <remarks>
    /// Not awaited: this runs from a change notification during a render, and a save that takes a
    /// moment must not hold one up. Failures are the store's own business — nobody asked for this
    /// one, so nobody is waiting to be told. The desktop keeps nothing here, since the player's real
    /// file is the one copy and it is written when they say so.
    /// </remarks>
    private void Keep()
    {
        if (!_files.SavesInPlace && _session?.Save() is { } bytes)
        {
            _ = _store.WriteAsync(Kept.Encode(bytes));
        }
    }

    /// <summary>
    /// Saves the empire being edited, and returns what went wrong if anything did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where it goes is the difference between the two hosts and nothing else the designer sees. The
    /// desktop app has the player's real file and writes it back; a browser has nowhere to write, so
    /// it keeps the file in local storage and hands over a download separately. Either way the whole
    /// file is written, because a file is what both of them store — it is the <em>deciding</em> that
    /// is per empire, not the writing.
    /// </para>
    /// <para>
    /// Awaited, and a store that refused is reported rather than shrugged at: the player pressed a
    /// button, and a button that says "Saved" over a store that said no would be worse than useless.
    /// </para>
    /// </remarks>
    public async Task<string?> SaveAsync()
    {
        if (_session is not { File: not null } session)
        {
            return "There is nothing open to save.";
        }

        try
        {
            var contents = session.Save();

            if (_files.SavesInPlace)
            {
                // The desktop saves or throws, so this can only be Saved today. Checked anyway: a
                // host that could answer otherwise must not have its "no" reported as a success.
                var outcome = await _files
                    .SaveAsync(session.FileName ?? EmpireDesignsFile.FileName, contents)
                    .ConfigureAwait(false);

                if (outcome is not SaveOutcome.Saved)
                {
                    return "Your empires were not written to their file.";
                }
            }
            else if (!await _store.WriteAsync(Kept.Encode(contents)).ConfigureAwait(false))
            {
                return "Your browser would not keep the file. Download it to be sure of it.";
            }

            session.MarkSaved();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"Your empires could not be saved: {ex.Message}";
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
