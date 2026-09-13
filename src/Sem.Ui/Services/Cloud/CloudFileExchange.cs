namespace Sem.Ui.Services.Cloud;

/// <summary>
/// The player's designs file, where the file is one they keep at a cloud provider.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the cloud feature as far as the rest of the app is concerned. Answering
/// <see cref="SavesInPlace"/> with true turns Export into Save, brings up the question before a file
/// is replaced, and - because <see cref="DesignSync"/> asks for nothing beyond that,
/// <see cref="Watch"/> and <see cref="TryOpenExistingAsync"/> - makes the file sync switch work
/// against a provider without a line of it knowing there is one.
/// </para>
/// <para>
/// Only the things that are about <em>the designs file</em> are answered here. Exporting a picture,
/// copying a link, importing from disk and warning before the tab closes are all still the
/// browser's, and are passed to it: connecting to OneDrive does not stop a download being a
/// download. That is also why this takes the browser's exchange rather than reimplementing six
/// members badly.
/// </para>
/// </remarks>
public sealed class CloudFileExchange : IFileExchange, IDisposable
{
    /// <summary>
    /// How often the file is asked whether it has changed.
    /// </summary>
    /// <remarks>
    /// A poll, because a webhook needs somewhere to be delivered and this app has no server. Fifteen
    /// seconds is a compromise between noticing an edit made on the desktop and spending somebody's
    /// data on a request that almost always answers "no": the call returns a stamp, not the file.
    /// </remarks>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly ICloudProvider _provider;
    private readonly IFileExchange _browser;

    /// <summary>Whether anybody is looking, or null where nothing can say.</summary>
    private readonly PageAttention? _attention;
    private readonly CancellationTokenSource _stopped = new();

    /// <summary>The version last read or written, which is what a write promises not to overwrite.</summary>
    private string? _baseline;

    /// <summary>
    /// The version the watch has already announced, so one change is not announced twice.
    /// </summary>
    /// <remarks>
    /// Kept apart from the baseline, because noticing a change and having dealt with one are
    /// different events and only the second is a promise. Moving the baseline on a mere look was
    /// the bug: a change that was noticed and then never answered - the read skipped, the question
    /// dismissed - left the next write licensed to go straight over it. Cleared whenever the
    /// baseline moves, since the baseline moving is what having dealt with it looks like.
    /// </remarks>
    private string? _reported;

    /// <summary>
    /// Takes a provider ready to act, the file chosen at it, and the browser underneath.
    /// </summary>
    /// <param name="provider">Where the file is kept.</param>
    /// <param name="file">Which file there.</param>
    /// <param name="browser">What answers everything that is not about the designs file.</param>
    /// <param name="version">
    /// The version already known to be there, where a previous visit read or wrote it and nothing
    /// has changed it since. Picking up from a stamp rather than from a read is what lets a reload
    /// leave an edit in progress alone: there is no reason to fetch a file that has not moved.
    /// </param>
    /// <param name="attention">
    /// Whether anybody is looking, so a tab nobody has in front of them stops asking and a tab
    /// somebody has just come back to asks at once. Null means always attended, which is what the
    /// desktop and a test get.
    /// </param>
    public CloudFileExchange(
        ICloudProvider provider,
        CloudFile file,
        IFileExchange browser,
        string? version = null,
        PageAttention? attention = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        File = file ?? throw new ArgumentNullException(nameof(file));
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _baseline = version;
        _attention = attention;
    }

    /// <summary>
    /// Raised with the version now at the provider and the bytes that go with it.
    /// </summary>
    /// <remarks>
    /// Every read and every write moves this file on, and both happen down here where the thing
    /// that remembers choices cannot see them. Saying so as it happens is what lets the next visit
    /// ask "has it changed since we last touched it" and get a true answer.
    /// </remarks>
    public event Action<string, byte[]>? Settled;

    /// <summary>The version last read or written, or null where neither has happened.</summary>
    public string? Version => _baseline;

    /// <summary>
    /// Takes this version and these bytes as what the file now holds, and says so.
    /// </summary>
    /// <remarks>
    /// The one place the baseline moves, and it moves only for a read or a write - the two things
    /// that put this app's own eyes on the contents. Anything the watch merely saw goes in
    /// <see cref="_reported"/> instead, and is forgotten here, because whatever it saw has now
    /// either been taken or been written over.
    /// </remarks>
    private void Settle(string version, byte[] contents)
    {
        _baseline = version;
        _reported = null;

        Settled?.Invoke(version, contents);
    }

    /// <summary>The file this is pointed at.</summary>
    public CloudFile File { get; }

    /// <summary>Which provider it is at, for saying so in the interface.</summary>
    public string ProviderName => _provider.Name;

    /// <inheritdoc />
    /// <remarks>
    /// True, and it is the load-bearing answer in this class. The file being replaced is one the
    /// player already had, at an address they chose, exactly as on the desktop.
    /// </remarks>
    public bool SavesInPlace => true;

    /// <inheritdoc />
    public string SaveVerb => "Save";

    /// <inheritdoc />
    public async Task<(string Name, byte[] Contents)?> TryOpenExistingAsync()
    {
        var found = await _provider.ReadAsync(File.Id, _stopped.Token).ConfigureAwait(false);

        if (found is not { } read)
        {
            return null;
        }

        Settle(read.Stamp.Version, read.Contents);

        return (File.Name, read.Contents);
    }

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
        SaveAsync(fileName, contents, backUp: true);

    /// <inheritdoc />
    /// <remarks>
    /// The dated copy goes first. A backup written after a successful save is a backup of the thing
    /// that just replaced what it was supposed to be a copy of, and a backup that fails must not
    /// stop the save the player asked for - so it is attempted, and its answer is not consulted.
    /// </remarks>
    public async Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (backUp && await Kept().ConfigureAwait(false) is { } previous)
        {
            await _provider
                .WriteBesideAsync(File, DatedName(File.Name, DateTime.Now), previous, _stopped.Token)
                .ConfigureAwait(false);
        }

        var (outcome, stamp) = await _provider
            .WriteAsync(File.Id, contents, _baseline, _stopped.Token)
            .ConfigureAwait(false);

        if (outcome is CloudWrite.Written)
        {
            // What was written is now what is there, so the next visit has both halves of the
            // answer without fetching anything.
            if ((stamp?.Version ?? _baseline) is { Length: > 0 } settled)
            {
                Settle(settled, contents);
            }

            return SaveOutcome.Saved;
        }

        if (outcome is CloudWrite.Conflicted)
        {
            return SaveOutcome.Conflicted;
        }

        // Asked only once something has gone wrong, and only to say which of two things it was: a
        // session that has ended is the player's to fix in one click, and everything else is not.
        return await _provider.SignedInAsync().ConfigureAwait(false)
            ? SaveOutcome.Refused
            : SaveOutcome.SignedOut;
    }

    /// <summary>What the file holds now, for the dated copy to be a copy of.</summary>
    private async Task<byte[]?> Kept()
    {
        var found = await _provider.ReadAsync(File.Id, _stopped.Token).ConfigureAwait(false);

        return found?.Contents;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Asking on a timer, and only for the stamp. A version that differs from the one last read or
    /// written is somebody else's write - this app's own are recorded as they happen, so its own
    /// saving does not read itself back.
    /// </remarks>
    public IDisposable? Watch(Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        return new CloudWatch(this, onChanged, _attention, _stopped.Token);
    }

    /// <summary>
    /// Asks the provider what the file is now, and says so where it is not what was expected.
    /// </summary>
    /// <remarks>
    /// The look itself, with no gate of its own - whoever calls it has already decided it is worth
    /// taking. Kept apart from the loop so that everything about it can be tested by calling it,
    /// rather than by waiting for a timer.
    /// </remarks>
    private async Task<bool> LookedAsync(CancellationToken cancellationToken)
    {
        if (await _provider.StatAsync(File.Id, cancellationToken).ConfigureAwait(false) is not { } now)
        {
            return false;
        }

        // In step. Anything the watch said before has been dealt with by whatever moved the
        // baseline, so it is free to say it again should the file move away and back.
        if (string.Equals(now.Version, _baseline, StringComparison.Ordinal))
        {
            _reported = null;

            return false;
        }

        if (string.Equals(now.Version, _reported, StringComparison.Ordinal))
        {
            return false;
        }

        // Recorded before telling anyone, so a reader that takes a moment cannot be told twice
        // about the same version by the next look coming round underneath it. The baseline is
        // deliberately left where it is: nothing has read this yet, and nothing may write over it.
        _reported = now.Version;

        return true;
    }

    /// <summary>
    /// One file being watched at a provider, and everything that decides when to ask about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handle that stops when it is disposed, which the last one did not: it returned the linked
    /// cancellation source, and disposing one of those does not cancel it. So turning the sync off
    /// left a request going out every fifteen seconds for as long as the tab was open.
    /// </para>
    /// <para>
    /// The tab's attention is answered here rather than anywhere else. Deciding when to ask a
    /// provider something is this class's whole job, and nothing upstream should have to learn what
    /// a hidden tab is to get the benefit of one.
    /// </para>
    /// </remarks>
    public sealed class CloudWatch : IDisposable
    {
        /// <summary>
        /// How soon after one look another is worth taking.
        /// </summary>
        /// <remarks>
        /// One floor guarding three things: an alt-tab storm, a frozen page resuming and firing
        /// several pending delays at once, and focus arriving a moment after visibility.
        /// </remarks>
        private static readonly TimeSpan Soonest = TimeSpan.FromSeconds(5);

        private readonly CloudFileExchange _file;
        private readonly Action _onChanged;
        private readonly PageAttention? _attention;

        private CancellationTokenSource? _stopped;

        /// <summary>When the last look was taken, or null before any has been.</summary>
        /// <remarks>
        /// Nullable rather than a sentinel. A "long ago" value of long.MinValue looks harmless and
        /// is not: the subtraction below overflows against it and comes out negative, so the floor
        /// reads as "asked a moment ago" and the very first return is the one it suppresses.
        /// </remarks>
        private long? _asked;

        /// <summary>Starts watching, and goes on until it is disposed or the exchange is.</summary>
        /// <param name="file">The file to ask about.</param>
        /// <param name="onChanged">Told when the file is not what this app last read or wrote.</param>
        /// <param name="attention">Whether anybody is looking, or null for always.</param>
        /// <param name="until">Cancelled when the exchange itself goes.</param>
        public CloudWatch(
            CloudFileExchange file, Action onChanged, PageAttention? attention, CancellationToken until)
        {
            _file = file;
            _onChanged = onChanged;
            _attention = attention;
            _stopped = CancellationTokenSource.CreateLinkedTokenSource(until);

            if (_attention is not null)
            {
                _attention.Returned += OnReturned;
            }

            _ = LoopAsync(_stopped.Token);
        }

        /// <summary>One look now, whatever the tab is doing.</summary>
        public async Task CheckAsync()
        {
            if (_stopped is not { } stopped || stopped.IsCancellationRequested)
            {
                return;
            }

            _asked = Environment.TickCount64;

            bool changed;

            try
            {
                changed = await _file.LookedAsync(stopped.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
                when (ex is OperationCanceledException or HttpRequestException or InvalidOperationException)
            {
                // A provider that cannot be reached this minute is not a reason to stop watching,
                // and nobody asked for this request - the next look tries again.
                return;
            }

            // Outside the guard above on purpose. What the callback does is not this class's to
            // swallow, and a failure in it faulting a loop nobody is awaiting would be a fault
            // nobody ever hears about.
            if (changed)
            {
                _onChanged();
            }
        }

        /// <summary>A look, unless the tab is away or one was taken a moment ago.</summary>
        public Task TickAsync() =>
            _attention is { Attended: false } || Recently() ? Task.CompletedTask : CheckAsync();

        /// <summary>Stops the asking, for good, and lets go of the page.</summary>
        public void Dispose()
        {
            // Exchanged, so a second dispose does not cancel a source that has already gone.
            if (Interlocked.Exchange(ref _stopped, null) is not { } stopped)
            {
                return;
            }

            if (_attention is not null)
            {
                _attention.Returned -= OnReturned;
            }

            stopped.Cancel();
            stopped.Dispose();
        }

        private bool Recently() =>
            _asked is { } last && Environment.TickCount64 - last < (long)Soonest.TotalMilliseconds;

        private void OnReturned()
        {
            if (!Recently())
            {
                _ = CheckAsync();
            }
        }

        private async Task LoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await TickAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The name for a dated copy, matching what the desktop writes beside the real file.
    /// </summary>
    /// <remarks>
    /// The same rule as <c>SafeFile.DatedBackupPath</c>, written out again because that lives in
    /// Sem.Io and this assembly may not reach it - Sem.Ui has to stay browser-safe. If one of them
    /// changes the other has to, and the tests either side both pin the shape so that a change to
    /// one goes red on its own.
    ///
    /// HH rather than hh, for the reason given there: hh is the twelve-hour clock and would put the
    /// morning's copy and the afternoon's at the same name.
    /// </remarks>
    internal static string DatedName(string fileName, DateTime moment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        return $"{stem}_{moment:yyMMdd_HHmmss}{extension}";
    }

    /// <inheritdoc />
    public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind) =>
        _browser.ExportAsync(fileName, contents, kind);

    /// <inheritdoc />
    public Task<(string Name, byte[] Contents)?> OpenAsync() => _browser.OpenAsync();

    /// <inheritdoc />
    public Task<bool> CanOpenAsync() => _browser.CanOpenAsync();

    /// <inheritdoc />
    public Task WarnBeforeLeavingAsync(bool unsaved) => _browser.WarnBeforeLeavingAsync(unsaved);

    /// <inheritdoc />
    public Task<bool> CopyToClipboardAsync(string text) => _browser.CopyToClipboardAsync(text);

    /// <inheritdoc />
    public string? ShareBaseUri => _browser.ShareBaseUri;

    /// <summary>Stops the polling, and anything else still in flight against the provider.</summary>
    public void Dispose()
    {
        _stopped.Cancel();
        _stopped.Dispose();
    }
}
