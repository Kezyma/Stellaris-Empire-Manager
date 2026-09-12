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

    private DesignSession? _session;
    private IDisposable? _watch;

    /// <summary>What the file is believed to hold, so this app's own writing is not read back.</summary>
    private byte[]? _onDisk;

    /// <summary>Set while this is the one reading or writing, so it does not answer itself.</summary>
    private bool _busy;

    /// <summary>A file read from disk that is waiting on an answer about unsaved edits.</summary>
    private (EmpireDesignsFile File, byte[] Contents, string Name)? _waiting;

    /// <summary>Takes the session's owner, the file, and where the answer is remembered.</summary>
    public DesignSync(SessionHost host, IFileExchange files, Preferences preferences)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
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
    public async Task SetAsync(bool on)
    {
        if (!Available || on == Enabled)
        {
            return;
        }

        Enabled = on;
        _preferences.SetSyncsWithFile(on);

        if (!on)
        {
            Stop();
            Changed?.Invoke();
            return;
        }

        _watch = _files.Watch(OnFileTouched);

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

    /// <summary>Takes the file that was waiting, losing the edits it replaces.</summary>
    public async Task AcceptAsync()
    {
        if (_waiting is not { } held)
        {
            return;
        }

        _waiting = null;

        if (Apply(held.File, held.Contents, held.Name))
        {
            await WriteAsync().ConfigureAwait(false);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Keeps the edits and drops the file that was waiting.
    /// </summary>
    /// <remarks>
    /// Which means the app and the file now disagree, and the next save settles it in the app's
    /// favour. That is what keeping the edits means, and there is no third answer that keeps both.
    /// </remarks>
    public void Decline()
    {
        _waiting = null;
        Changed?.Invoke();
    }

    /// <summary>Stops watching and forgets what was waiting, leaving the file exactly as it is.</summary>
    public void Dispose()
    {
        Stop();

        if (_session is not null)
        {
            _session.FileChanged -= OnListChanged;
        }

        _host.Saved -= OnSaved;
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
        if (Enabled && !_busy)
        {
            _ = WriteAsync();
        }
    }

    private void OnFileTouched() => _ = LoadFromDiskAsync();

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
            Changed?.Invoke();
        }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Note = $"Your empire designs file changed, but could not be read: {ex.Message}";
        }
        finally
        {
            _busy = false;

            // Once the flag is down, so the write is not turned away as this method answering
            // itself - which is exactly what the flag is there to prevent.
            if (owed)
            {
                _ = WriteAsync();
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
            // Read as bytes here, so this is the host objecting to the file rather than the parse
            // below. Treated the same way: it may simply be half written.
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
        // announcement is the one this class answers by writing.
        var already = _busy;
        _busy = true;

        try
        {
            var editing = session.Current?.Key;

            session.Open(file, name);
            _onDisk = contents;
            Note = null;

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
        }
    }

    private static bool Same(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
}
