using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Keeps the one working session alive across pages, so moving between the empire list and the
/// designer does not reload the game data or lose unsaved changes.
/// </summary>
/// <remarks>
/// <c>assumeAllPacks</c> decides whether to open with every content pack enabled: true on the web,
/// where the installation the data was read from is not the player's; false on the desktop, where
/// it is.
/// </remarks>
public sealed class SessionHost(
    IGameDataSource source,
    IFileExchange files,
    IDesignStore? store = null,
    bool assumeAllPacks = false,
    Preferences? preferences = null) : IDisposable
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

    /// <summary>
    /// Raised after the file has been written, carrying the bytes that went into it.
    /// </summary>
    /// <remarks>
    /// For whoever is keeping the file and the app in step, which needs to recognise its own
    /// writing when the file system reports it a moment later - otherwise every save would be read
    /// straight back in as though somebody else had made it.
    /// </remarks>
    public event Action<byte[]>? Saved;

    /// <summary>
    /// Whether a save keeps a dated copy of the file it replaces.
    /// </summary>
    /// <remarks>
    /// The player's own answer, except while the file is being kept in step: saving then happens by
    /// itself, several times a minute, and one dated file per save would bury the folder the game
    /// keeps its saves in. The app's own archive still holds the newest twenty, which is what that
    /// is for.
    /// </remarks>
    private bool KeepsBackup => _preferences.KeepsBackup && !_preferences.SyncsWithFile;

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

            // Cleared before the attempt, not after it. TryOpenExistingAsync below records why the
            // host's own file would not open, and clearing on the way out deleted that reason one
            // line later — the desktop opened an empty session with nothing said about the file it
            // had just failed to read. Clearing here still wipes a previous attempt's message, which
            // is the only thing the old line was good for.
            LoadError = null;

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
            session.FileChanged += () => Keep(session);

            // Opening a file can change it - one written before this app derived what an empire's
            // choices force is brought up to date as it is read - and the browser's store is the
            // only copy of that repair there is. So it is kept now rather than waiting for the next
            // edit, by asking rather than by listening.
            //
            // Asking, because listening was wrong in a way that cost data. Subscribed before the
            // ladder, the handler also heard StartEmptyFile above, which is not a file arriving:
            // a store that failed to read for any reason took an empty file over the top of it, and
            // every start after that read the empty one back and kept it empty. The repair is worth
            // persisting; nothing about starting with nothing is.
            if (session.HasUnwrittenFileChanges)
            {
                Keep(session);
            }

            _session = session;
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
    /// Handed the session rather than reading the field, because it is also called directly from
    /// the startup ladder, before the field is set.
    ///
    /// Not awaited: this runs from a change notification during a render, and a save that takes a
    /// moment must not hold one up. Failures are the store's own business — nobody asked for this
    /// one, so nobody is waiting to be told. The desktop keeps nothing here, since the player's real
    /// file is the one copy and it is written when they say so.
    /// </remarks>
    private void Keep(DesignSession session)
    {
        if (!_files.SavesInPlace && session.Save() is { } bytes)
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
                    .SaveAsync(session.FileName ?? EmpireDesignsFile.FileName, contents, KeepsBackup)
                    .ConfigureAwait(false);

                // Said apart from the rest, because it is the one that is not a failure: the file
                // was not written because writing it would have thrown away a change somebody else
                // made, and trying again is the wrong thing to do about that.
                if (outcome is SaveOutcome.Conflicted)
                {
                    return "Your designs file changed somewhere else while you were editing, so "
                        + "nothing was written over it. Reload it to see what arrived.";
                }

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
            Saved?.Invoke(contents);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"Your empires could not be saved: {ex.Message}";
        }
    }

    /// <summary>
    /// Keeps the file wherever this host keeps one, after it has been handed to the player.
    /// </summary>
    /// <remarks>
    /// Export writes a file the player named and then wants to call the work saved. On the desktop
    /// that file <em>is</em> where the app keeps things, so there is nothing further to do. In a
    /// browser it is not: the browser's own copy lives in local storage, that copy is what a reload
    /// restores, and only <see cref="SaveAsync"/> was writing it. So an export marked the work saved,
    /// disabled the Save button, stopped warning on the way out — and the next reload handed back the
    /// empire as it had been before the export.
    /// </remarks>
    /// <returns>What went wrong, or null when the copy is safe.</returns>
    public async Task<string?> RememberAsync()
    {
        if (_session is not { File: not null } session)
        {
            return "There is nothing open to keep.";
        }

        if (_files.SavesInPlace)
        {
            return null;
        }

        return await _store.WriteAsync(Kept.Encode(session.Save())).ConfigureAwait(false)
            ? null
            : "Your browser would not keep the file. The copy you just saved is the only one.";
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

    /// <summary>Releases the gate that keeps two openings from racing each other.</summary>
    /// <remarks>
    /// Scoped, so one is built per page in the browser and per window on the desktop, and a
    /// SemaphoreSlim holds a wait handle from the first wait onwards.
    /// </remarks>
    public void Dispose() => _gate.Dispose();
}
