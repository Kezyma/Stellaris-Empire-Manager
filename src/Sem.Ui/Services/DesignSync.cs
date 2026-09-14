using Sem.Clausewitz;
using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Keeps the app and the player's designs file in step with one another.
/// </summary>
/// <remarks>
/// <para>
/// Off by default and offered only where there is a file to keep in step, which is the desktop. On,
/// the file is written whenever the list changes or an empire is saved, and read back whenever
/// something else writes it - so the game and this app can be open at once and neither is looking
/// at a stale copy. The Save button goes quiet, because there is nothing left for it to do.
/// </para>
/// <para>
/// Two things make this harder than it sounds, and both are handled here rather than by the host.
/// The first is that a write of ours is reported to us like anybody else's, so without care every
/// save would be read straight back in; the bytes that went into the file are remembered and the
/// file is compared against them. The second is that reading and writing both raise the same
/// notifications that started them, so anything this does to the session is done behind a flag that
/// stops it answering itself.
/// </para>
/// <para>
/// The one case it will not decide alone is a file that changed underneath an empire with edits
/// nobody has saved. Loading would take those edits away without asking, so it asks.
/// </para>
/// </remarks>
public sealed class DesignSync : IDisposable
{
    /// <summary>How many times a file that will not parse is read again before giving up.</summary>
    /// <remarks>
    /// A file being written is readable and half there, and the watcher deliberately reports late
    /// rather than never - so the first read after the game saves can still land mid-write.
    /// </remarks>
    private const int Attempts = 4;

    private static readonly TimeSpan Settling = TimeSpan.FromMilliseconds(250);

    private readonly SessionHost _host;
    private readonly IFileExchange _files;
    private readonly Preferences _preferences;

    /// <summary>What announces that the file being kept in step has been swapped, where one can.</summary>
    private readonly FileExchangeRouter? _router;

    private DesignSession? _session;
    private IDisposable? _watch;

    /// <summary>What the file is believed to hold, so this app's own writing is not read back.</summary>
    private byte[]? _onDisk;

    /// <summary>Set while this is the one reading or writing, so it does not answer itself.</summary>
    private bool _busy;

    /// <summary>
    /// Something arrived while that flag was up and has not been dealt with.
    /// </summary>
    /// <remarks>
    /// Without this, anything that happened during a read or a write was simply lost. A read that
    /// finds a half-written file waits and reads again, holding the flag for the best part of a
    /// second, and an empire deleted during that second would never have reached the file - the
    /// app and the file would have gone quietly out of step, which is the one thing being on is
    /// supposed to prevent.
    /// </remarks>
    private bool _missed;

    /// <summary>A file read from disk that is waiting on an answer about unsaved edits.</summary>
    private (EmpireDesignsFile File, byte[] Contents, string Name)? _waiting;

    /// <summary>Takes what it keeps in step, and where to notice that the file underneath changed.</summary>
    /// <param name="host">The session being kept in step with a file.</param>
    /// <param name="files">Where that file is, which on the web is whatever the router points at.</param>
    /// <param name="preferences">Where the answer about writing as you go is remembered.</param>
    /// <param name="router">
    /// The thing that can be repointed at a different file mid-visit, where there is one. Optional
    /// because the desktop has no such thing - its file is the one it started with - and null there
    /// simply means nothing ever announces a switch.
    /// </param>
    public DesignSync(
        SessionHost host,
        IFileExchange files,
        Preferences preferences,
        FileExchangeRouter? router = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _router = router;

        if (_router is not null)
        {
            _router.Changed += OnExchangeChanged;
        }
    }

    /// <summary>
    /// Points the watch at whatever the router now holds, and forgets the last file entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Connecting to a second file used to leave the watch on the first. <see cref="SetAsync"/>
    /// returns at once when asked to turn on something already on, which is right for a toggle and
    /// wrong as the way a new file arrives - so the old watch went on polling the file that had
    /// been left, and the new one was watched by nothing.
    /// </para>
    /// <para>
    /// The baseline goes with it, which was latent and worse: nothing cleared it on a switch, so a
    /// read of the newly connected file was compared against the bytes of the previous one.
    /// </para>
    /// </remarks>
    private void OnExchangeChanged()
    {
        Stop();

        _onDisk = null;

        if (Enabled && Available)
        {
            _watch = _files.Watch(OnFileTouched);
        }
        else
        {
            // Nothing in place to keep in step with any more - a disconnection puts the browser's
            // own exchange back, which saves nowhere in particular.
            Enabled = Enabled && Available;
        }

        Changed?.Invoke();
    }

    /// <summary>Raised when anything here that a header would draw has changed.</summary>
    public event Action? Changed;

    /// <summary>Whether this host has a file of its own to keep in step at all.</summary>
    public bool Available => _files.SavesInPlace;

    /// <summary>Whether it is being kept in step.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Whether a file is waiting on an answer about the edits it would replace.</summary>
    public bool Asking => _waiting is not null;

    /// <summary>What went wrong, where something did and nobody asked for it.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Takes the session to keep in step, and picks up where the last visit left off.
    /// </summary>
    public async Task AttachAsync(DesignSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_session is not null)
        {
            return;
        }

        _session = session;
        session.FileChanged += OnListChanged;
        _host.Saved += OnSaved;

        // What the host does about a write it was not allowed to make. It is this class rather
        // than the host that can read the file and ask, so the host holds the question and this
        // one answers it.
        _host.Reconcile = ReconcileAsync;

        if (Available && _preferences.SyncsWithFile)
        {
            await SetAsync(true).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Turns it on or off, and remembers which.
    /// </summary>
    /// <remarks>
    /// Turning it on has to settle a disagreement that may already exist, and the rule is that
    /// whichever side has something the other has not seen wins. Work the player has done and not
    /// written is theirs and goes to the file; a session with nothing outstanding takes whatever the
    /// file holds, which is how an empire built in the game while this was off arrives.
    /// </remarks>
    public Task SetAsync(bool on) =>
        !Available || on == Enabled ? Task.CompletedTask : SettleAsync(on, holds: null);

    /// <summary>
    /// Takes the file now in place as the one to keep in step, and what it was just read as.
    /// </summary>
    /// <param name="on">Whether writing as you go is wanted, which the answer outlives the file.</param>
    /// <param name="holds">What that file was found to hold, where the caller has just read it.</param>
    /// <remarks>
    /// Apart from <see cref="SetAsync"/> because they answer different questions. That one is the
    /// player changing their mind, and doing nothing when asked to turn on something already on is
    /// exactly right for it. This one is the file changing underneath an answer already given, and
    /// there "it was already on" is not a reason to leave the watch pointed at the file they left.
    /// </remarks>
    public Task RepointAsync(bool on, byte[]? holds) =>
        !Available ? Task.CompletedTask : SettleAsync(on, holds);

    /// <summary>Puts the answer, the watch and the baseline in step with one another.</summary>
    private async Task SettleAsync(bool on, byte[]? holds)
    {
        Enabled = on;
        _preferences.SetSyncsWithFile(on);

        if (!on)
        {
            Stop();
            Changed?.Invoke();
            return;
        }

        // Stopped first, so repointing at a second file does not leave the first one watched.
        Stop();
        _watch = _files.Watch(OnFileTouched);

        // What the caller has just read is what the file holds, and saying so here is what stops
        // the first look after connecting reporting the file to itself as somebody else's change.
        if (holds is not null)
        {
            _onDisk = holds;
        }

        if (_session is { File: not null, HasUnwrittenFileChanges: false, IsModified: false })
        {
            await LoadFromDiskAsync().ConfigureAwait(false);
        }
        else
        {
            await WriteAsync().ConfigureAwait(false);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Reads the file again, for a view that is being shown afresh.
    /// </summary>
    /// <remarks>
    /// Belt as well as braces. The watcher is the thing that notices a change, and a watcher can
    /// miss one - a file written over a network share, a machine asleep between the write and the
    /// looking. Coming back to the list is a natural moment to check, and costs one read.
    /// </remarks>
    public Task RefreshAsync() =>
        Enabled ? LoadFromDiskAsync() : Task.CompletedTask;

    /// <summary>
    /// Reads the file that would not be written over, and asks what to do about what is in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer to a refused save. A host that keeps a file refuses to write over a change it
    /// has not seen, which is right, and on its own leaves nowhere to go: what it said was to
    /// reload the file, and with this switched off there is no reload -
    /// <see cref="LoadFromDiskAsync"/> returns at its first guard. So the file is read here
    /// instead, directly rather than through that gated path, and handed to the same question the
    /// watcher asks, whose answers write.
    /// </para>
    /// <para>
    /// Reading is also what lifts the refusal. Both hosts move the baseline they measure a write
    /// against whenever they read, because a look is exactly what that baseline records - so by
    /// the time an answer is pressed the write is allowed again, and the write that follows the
    /// answer is the save that was asked for.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Whether the refusal has been taken in hand. False means the file could not be read at all,
    /// and whoever asked still owes the player a sentence about it.
    /// </returns>
    public async Task<bool> ReconcileAsync()
    {
        if (!Available || _session is not { } session)
        {
            return false;
        }

        // Already asked, about the same file. A second question is not a second chance to answer.
        if (Asking)
        {
            return true;
        }

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                // Nothing there to have been protecting. Whatever the write was refused over is
                // gone, and there is nothing here to ask about.
                if (await Read().ConfigureAwait(false) is not { } existing)
                {
                    return false;
                }

                if (Parse(existing.Contents) is not { } file)
                {
                    if (attempt < Attempts)
                    {
                        // Being written this moment, most likely - a refused save and a file in
                        // the middle of being replaced are the same event seen from two sides.
                        await Task.Delay(Settling).ConfigureAwait(false);
                        continue;
                    }

                    return false;
                }

                _waiting = (file, existing.Contents, existing.Name);

                // A file that holds what is already open is not a question. The refusal was about
                // a baseline rather than about the contents, and taking a copy of what you have
                // costs nothing, settles the flag, and leaves the Save button with nothing to do.
                if (Same(existing.Contents, session.Save()))
                {
                    await ResolveAsync(Arrival.TakeTheirs).ConfigureAwait(false);

                    return true;
                }

                Changed?.Invoke();

                return true;
            }
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException
                or CwSyntaxException or InvalidOperationException)
        {
            // Said by whoever asked, in the words of the button they pressed. Nothing is recorded
            // here, because a note under a sentence saying the same thing is one to dismiss twice.
            return false;
        }
    }

    /// <summary>Takes the file that was waiting, losing the edits it replaces.</summary>
    public async Task AcceptAsync() => await ResolveAsync(Arrival.TakeTheirs).ConfigureAwait(false);

    /// <summary>
    /// Settles the question the watcher asked, whichever way it was answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every answer moves the baseline to what the file now holds, including the ones that keep
    /// your own empires. That is what stops the same change being asked about again on the next
    /// touch of the file, and it is also what makes each answer mean one thing and go on meaning
    /// it: having said you are keeping yours, the next write puts yours over what arrived, on
    /// purpose rather than by accident.
    /// </para>
    /// <para>
    /// And whatever comes out of it is written back where it differs from the file. Not only with
    /// the sync on, which is what this used to say: <see cref="ReconcileAsync"/> asks the same
    /// question about a refused save, and with writing as you go switched off the answer is the
    /// only thing that will ever write.
    /// </para>
    /// </remarks>
    public async Task ResolveAsync(Arrival answer)
    {
        if (_waiting is not { } held)
        {
            return;
        }

        _waiting = null;

        var owed = answer switch
        {
            Arrival.TakeTheirs => Apply(held.File, held.Contents, held.Name),
            Arrival.KeepMine => Settled(held.Contents),
            _ => Fold(held.File, held.Contents, replacingMatches: answer is Arrival.TheirsWin),
        };

        if (owed)
        {
            // Recorded rather than dropped when something else holds the flag. WriteAsync turns
            // away while a read or a write is in flight, and this was the one caller that did not
            // say so - the two event handlers have said it since the flag existed. An answer
            // pressed during a poll was therefore lost, and lost silently and for good: the
            // baseline had already moved to what arrived, so every comparison afterwards found the
            // two sides in step and nothing ever wrote. Most reachable against a provider, where
            // the flag is held across an HTTP read rather than across a few hundred milliseconds.
            if (_busy)
            {
                _missed = true;
            }
            else
            {
                await WriteAsync().ConfigureAwait(false);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Keeps what is open, and takes the arriving file as the thing written over.</summary>
    private bool Settled(byte[] contents)
    {
        _onDisk = contents;
        Note = null;

        return _session is { } session && !Same(session.Save(), contents);
    }

    /// <summary>Folds the arriving file into what is open, the way the answer said.</summary>
    private bool Fold(EmpireDesignsFile file, byte[] contents, bool replacingMatches)
    {
        if (_session is not { } session)
        {
            return false;
        }

        // Held down for the same reason Apply holds it: merging announces itself, and the
        // announcement is the thing this class answers by writing.
        var already = _busy;
        var owed = _missed;
        _busy = true;

        try
        {
            session.Merge(file, replacingMatches);

            return Settled(contents);
        }
        finally
        {
            _busy = already;
            _missed = owed;
        }
    }

    /// <summary>
    /// Keeps the edits, and takes the arriving file as the thing they will be written over.
    /// </summary>
    /// <remarks>
    /// Which means the app and the file now disagree and the next write settles it in the app's
    /// favour - deliberately, because that is what keeping the edits means. There used to be no
    /// answer that kept both; there are two now, and this is the one that keeps neither of theirs.
    /// </remarks>
    public Task Decline() => ResolveAsync(Arrival.KeepMine);

    /// <summary>
    /// What the file that changed holds, for the question to weigh against what is open.
    /// </summary>
    public (string Name, int Holds)? Arrived =>
        _waiting is { } held ? (held.Name, held.File.Designs.Count) : null;

    /// <summary>Stops watching and forgets what was waiting, leaving the file exactly as it is.</summary>
    public void Dispose()
    {
        Stop();

        if (_session is not null)
        {
            _session.FileChanged -= OnListChanged;
        }

        if (_router is not null)
        {
            _router.Changed -= OnExchangeChanged;
        }

        _host.Saved -= OnSaved;

        if (_host.Reconcile == ReconcileAsync)
        {
            _host.Reconcile = null;
        }
    }

    private void Stop()
    {
        _watch?.Dispose();
        _watch = null;
        _waiting = null;
    }

    private void OnSaved(byte[] contents) => _onDisk = contents;

    /// <summary>The list gained, lost or moved an empire, so the file is owed the change.</summary>
    private void OnListChanged()
    {
        if (!Enabled)
        {
            return;
        }

        if (_busy)
        {
            _missed = true;
            return;
        }

        _ = WriteAsync();
    }

    private void OnFileTouched()
    {
        if (_busy)
        {
            _missed = true;
            return;
        }

        _ = LoadFromDiskAsync();
    }

    /// <summary>
    /// Settles whatever was put off, for a caller that knows only that something happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read first, then write if what is held still differs from what the file holds. Which way
    /// round matters: the file is read to find out whether it actually moved, and only what the app
    /// is still holding afterwards is worth writing.
    /// </para>
    /// <para>
    /// By comparing the bytes rather than by asking the session what it owes. It cannot be asked:
    /// a write settles the session's "unwritten" flag for the state at the moment it began, and a
    /// change made during that write leaves the flag saying everything is written when the newest
    /// empire has never reached the file. The comparison cannot be lied to.
    /// </para>
    /// </remarks>
    private async Task SettleAsync()
    {
        await LoadFromDiskAsync().ConfigureAwait(false);

        // A file waiting on an answer is not one to write over while the question is on screen.
        if (Asking || _session is not { File: not null } session)
        {
            return;
        }

        if (_onDisk is null || !Same(session.Save(), _onDisk))
        {
            await WriteAsync().ConfigureAwait(false);
        }
    }

    private async Task WriteAsync()
    {
        if (_busy || _session is not { File: not null })
        {
            return;
        }

        _busy = true;

        try
        {
            Note = await _host.SaveAsync().ConfigureAwait(false);
        }
        finally
        {
            _busy = false;
            Catch();
            Changed?.Invoke();
        }
    }

    /// <summary>Deals with whatever arrived while the flag was up, now that it is down again.</summary>
    private void Catch()
    {
        if (!_missed)
        {
            return;
        }

        _missed = false;
        _ = SettleAsync();
    }

    /// <summary>
    /// Reads the file and takes it, unless it is this app's own writing coming back.
    /// </summary>
    private async Task LoadFromDiskAsync()
    {
        if (_busy || !Enabled || _session is not { } session)
        {
            return;
        }

        _busy = true;
        var owed = false;

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                var found = await Read().ConfigureAwait(false);

                // No file, or one that is gone for the moment while it is being replaced. Nothing
                // to take, and nothing wrong either.
                if (found is not { } existing)
                {
                    return;
                }

                // The baseline is what was last written or read; before either, what is in hand -
                // which at that point is what was opened, and so is the same thing.
                if (Same(existing.Contents, _onDisk ?? session.Save()))
                {
                    // Reading it at all settles any earlier complaint that it could not be read,
                    // which would otherwise sit in the header for the rest of the session.
                    Note = null;
                    return;
                }

                if (Parse(existing.Contents) is not { } file)
                {
                    if (attempt < Attempts)
                    {
                        // Still being written. The watcher waits for the writing to settle and can
                        // still be early, so this waits again rather than calling the file broken.
                        await Task.Delay(Settling).ConfigureAwait(false);
                        continue;
                    }

                    Note = "Your empire designs file changed, but could not be read. "
                        + "It may still be being written.";
                    return;
                }

                if (session.IsModified)
                {
                    _waiting = (file, existing.Contents, existing.Name);
                    return;
                }

                owed = Apply(file, existing.Contents, existing.Name);
                return;
            }
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException
                or CwSyntaxException or InvalidOperationException)
        {
            // Reported rather than thrown, because nobody asked for this read: it happens because
            // the file moved, and the page it would have been thrown from is one somebody is using.
            Note = $"Your empire designs file changed, but could not be read: {ex.Message}";
        }
        finally
        {
            _busy = false;

            // Once the flag is down, so the write is not turned away as this method answering
            // itself - which is exactly what the flag is there to prevent.
            if (owed)
            {
                _missed = false;
                _ = WriteAsync();
            }
            else
            {
                Catch();
            }

            Changed?.Invoke();
        }
    }

    private async Task<(string Name, byte[] Contents)?> Read()
    {
        try
        {
            return await _files.TryOpenExistingAsync().ConfigureAwait(false);
        }
        catch (CwSyntaxException)
        {
            // A host that reads bytes cannot raise this; one that parses on the way out can. Nothing
            // is taken this time, and the next change - or the next visit to the list - tries again.
            return null;
        }
    }

    private static EmpireDesignsFile? Parse(byte[] contents)
    {
        try
        {
            return EmpireDesignsFile.Load(contents);
        }
        catch (Exception ex) when (ex is CwSyntaxException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Puts the file in front of the player, keeping them on the empire they were looking at.
    /// </summary>
    /// <remarks>
    /// Opening a file selects its first empire, which for an import is right and here is not: this
    /// is the same file arriving with somebody else's change in it, and being thrown back to the top
    /// of the list every time the game saves would make the two unusable together. So the empire
    /// that was open is found again by key, where the change left it there.
    /// </remarks>
    /// <returns>Whether the file is owed a write back, which opening it can decide.</returns>
    private bool Apply(EmpireDesignsFile file, byte[] contents, string name)
    {
        if (_session is not { } session)
        {
            return false;
        }

        // Held down across the whole of this, because opening a file announces itself and the
        // announcement is the one this class answers by writing. What was owed beforehand is put
        // back afterwards, so the open's own announcement is not counted as a debt of its own.
        var already = _busy;
        var owed = _missed;
        _busy = true;

        try
        {
            var editing = session.Current?.Key;

            session.Open(file, name);
            _onDisk = contents;
            Note = null;

            // Taking the file is an answer to any question about the file, however the taking was
            // arrived at. Without this a question raised over a refused write, and then settled by
            // an ordinary load a moment later, stayed on screen with nothing behind it.
            _waiting = null;

            if (editing is { Length: > 0 } && session.File?.Find(editing) is { } same)
            {
                session.Select(same);
            }

            // A file written before this app derived what an empire's choices force is repaired as
            // it is read, and the repair is real - so it goes back to the file rather than waiting
            // for the next edit to carry it there.
            return session.HasUnwrittenFileChanges;
        }
        finally
        {
            _busy = already;
            _missed = owed;
        }
    }

    private static bool Same(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
}
