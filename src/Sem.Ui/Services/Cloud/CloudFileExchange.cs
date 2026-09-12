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
    private readonly CancellationTokenSource _stopped = new();

    /// <summary>The version last read or written, which is what a write promises not to overwrite.</summary>
    private string? _version;

    /// <summary>Takes a provider ready to act, the file chosen at it, and the browser underneath.</summary>
    public CloudFileExchange(ICloudProvider provider, CloudFile file, IFileExchange browser)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        File = file ?? throw new ArgumentNullException(nameof(file));
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
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

        _version = read.Stamp.Version;

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
            .WriteAsync(File.Id, contents, _version, _stopped.Token)
            .ConfigureAwait(false);

        if (outcome is CloudWrite.Written)
        {
            _version = stamp?.Version ?? _version;

            return SaveOutcome.Saved;
        }

        return outcome is CloudWrite.Conflicted ? SaveOutcome.Conflicted : SaveOutcome.Refused;
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

        var stopped = CancellationTokenSource.CreateLinkedTokenSource(_stopped.Token);

        _ = PollAsync(onChanged, stopped.Token);

        return stopped;
    }

    private async Task PollAsync(Action onChanged, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);

                if (await _provider.StatAsync(File.Id, cancellationToken).ConfigureAwait(false) is not { } now)
                {
                    continue;
                }

                if (_version is not null && string.Equals(now.Version, _version, StringComparison.Ordinal))
                {
                    continue;
                }

                // Recorded before telling anyone, so a reader that takes a moment cannot be told
                // twice about the same version by the next tick coming round underneath it.
                _version = now.Version;
                onChanged();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                // A provider that cannot be reached this minute is not a reason to stop watching,
                // and nobody asked for this request - the next tick tries again.
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
