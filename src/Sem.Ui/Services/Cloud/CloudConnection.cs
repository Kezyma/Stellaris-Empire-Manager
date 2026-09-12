using Sem.Clausewitz;
using Sem.Designs;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// Which file at a provider the app is working on, and how it got there.
/// </summary>
/// <remarks>
/// <para>
/// The one piece that knows about all of it: signing in, choosing a file, and putting the exchange
/// that reaches it behind the router so that Save goes there instead of into a download. Everything
/// else in the cloud folder is deliberately ignorant of the others.
/// </para>
/// <para>
/// Connecting replaces what is open, so the caller asks about unsaved work first - the same
/// arrangement Import has, and for the same reason: the question belongs to whatever is holding the
/// work, and this is not it.
/// </para>
/// </remarks>
public sealed class CloudConnection : IDisposable
{
    /// <summary>
    /// Where the chosen file is remembered, as the provider's own handle and the name to show.
    /// </summary>
    /// <remarks>
    /// Two fields with a bar between them, which neither can contain: the separator is invalid in a
    /// file name at every provider and item ids are alphanumeric. The preference store splits a line
    /// on its first equals and trims, so what must not appear here is a newline, and neither can.
    /// </remarks>
    private const string ChosenKey = "cloud.file";

    private readonly ICloudProvider _provider;
    private readonly OneDriveAuth _auth;
    private readonly FileExchangeRouter _router;
    private readonly IFileExchange _browser;
    private readonly SessionHost _host;
    private readonly Preferences _preferences;
    private readonly DesignSync _sync;

    private CloudFileExchange? _connected;

    /// <summary>Takes everything the act of connecting has to touch.</summary>
    public CloudConnection(
        ICloudProvider provider,
        OneDriveAuth auth,
        FileExchangeRouter router,
        BrowserFileExchange browser,
        SessionHost host,
        Preferences preferences,
        DesignSync sync)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    /// <summary>Raised when any of the answers below changes.</summary>
    public event Action? Changed;

    /// <summary>What to call the provider on screen.</summary>
    public string ProviderName => _provider.Name;

    /// <summary>The file being worked on there, or null when none is.</summary>
    public CloudFile? File => _connected?.File;

    /// <summary>Whether a file at the provider is what Save now writes.</summary>
    public bool Connected => _connected is not null;

    /// <summary>What went wrong, where something did.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether the session ended while the app was still pointed at a file there.
    /// </summary>
    /// <remarks>
    /// Different from simply not being connected: the file is still chosen and still wanted, and
    /// one sign-in puts it back. The header says so rather than leaving a save to fail quietly.
    /// </remarks>
    public bool SessionEnded { get; private set; }

    /// <summary>Whether the last answer about writing as you go was yes, for seeding the question.</summary>
    public bool AutoSaveRemembered => _preferences.SyncsWithFile;

    /// <summary>Where the file sits, as a trail worth showing on a button.</summary>
    public string? Where => File is { } file
        ? file.Folder is { Length: > 0 } folder ? $"{folder} › {file.Name}" : file.Name
        : null;

    /// <summary>
    /// The providers on offer, and whether each can be chosen now.
    /// </summary>
    /// <remarks>
    /// One is built. The other is named anyway, because a chooser that silently offers a single
    /// option tells somebody nothing about whether the app will ever have more - and because being
    /// signed in to one has to visibly rule out the other, which needs both on screen to show.
    /// </remarks>
    public IReadOnlyList<CloudChoice> Providers => _providers;

    private CloudChoice[] _providers = [];

    /// <summary>
    /// Whether there is a session at the provider to act with, and what to offer if not.
    /// </summary>
    /// <remarks>
    /// Also rebuilds the list of providers, because what can be chosen depends on the answer:
    /// signing in to one rules the others out until it is let go of.
    /// </remarks>
    public async Task<bool> SignedInAsync()
    {
        var signedIn = await _auth.SignedInAsync().ConfigureAwait(false);

        _providers =
        [
            new(_provider.Name, Ready: true, signedIn ? "Signed in" : null),
            new(
                "Google Drive",
                Ready: false,
                signedIn ? $"Disconnect {_provider.Name} first" : "Not built yet"),
        ];

        return signedIn;
    }

    /// <summary>Where to send the browser to sign in.</summary>
    public Task<string> BeginSignInAsync() => _auth.BeginAsync();

    /// <summary>
    /// Finishes a sign-in the browser has come back from, and picks up where it left off.
    /// </summary>
    /// <remarks>
    /// The file chosen before is reconnected to without asking again, because choosing it was the
    /// answer to that question and signing in was only the way back to it.
    /// </remarks>
    public async Task<bool> CompleteSignInAsync(string address)
    {
        if (!await _auth.CompleteAsync(address).ConfigureAwait(false))
        {
            return false;
        }

        await ResumeAsync().ConfigureAwait(false);
        Changed?.Invoke();

        return true;
    }

    /// <summary>What is in a folder at the provider, for finding a file by looking.</summary>
    public Task<IReadOnlyList<CloudEntry>> ListAsync(string? folderId) =>
        Asking(() => _provider.ListAsync(folderId), Array.Empty<CloudEntry>());

    /// <summary>Files at the provider whose name matches, for somebody to choose between.</summary>
    public Task<IReadOnlyList<CloudFile>> FindAsync(string query) =>
        Asking(() => _provider.FindAsync(query), Array.Empty<CloudFile>());

    /// <summary>
    /// Asks the provider something, turning the ways it can be unreachable into a note.
    /// </summary>
    /// <remarks>
    /// Nothing here is worth throwing at a dialog somebody is standing in front of: a tab that went
    /// offline, a session that quietly expired, a provider having a bad minute. Each becomes an
    /// empty answer and a line on screen.
    /// </remarks>
    private async Task<IReadOnlyList<T>> Asking<T>(
        Func<Task<IReadOnlyList<T>>> ask, IReadOnlyList<T> nothing)
    {
        try
        {
            return await ask().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Note = $"{_provider.Name} could not be asked: {ex.Message}";
            Changed?.Invoke();

            return nothing;
        }
    }

    /// <summary>
    /// Works on this file from now on, opening what it holds.
    /// </summary>
    /// <returns>True when the file was opened and Save now goes to it.</returns>
    public async Task<bool> UseAsync(CloudFile file, bool? autoSave = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        var exchange = new CloudFileExchange(_provider, file, _browser);
        var opened = await exchange.TryOpenExistingAsync().ConfigureAwait(false);

        if (opened is not { } read)
        {
            exchange.Dispose();

            // Which of the two it was decides both what to say and what the header offers, so it
            // is asked here rather than guessed from a null.
            SessionEnded = !await _provider.SignedInAsync().ConfigureAwait(false);

            // No instruction on the end of this one: where it is shown, the way out of it is a
            // button immediately after the full stop, and telling somebody to press the thing they
            // are looking at reads as a stutter.
            Note = SessionEnded
                ? $"Your {_provider.Name} sign-in has ended, so {file.Name} could not be opened."
                : $"{file.Name} could not be read from {_provider.Name}.";

            Changed?.Invoke();

            return false;
        }

        EmpireDesignsFile parsed;

        try
        {
            parsed = EmpireDesignsFile.Load(read.Contents);
        }
        catch (Exception ex) when (ex is CwSyntaxException or FormatException or InvalidOperationException)
        {
            // Read but not understood, which is a different thing from unreachable and deserves to
            // be said differently: the file is there and is not a designs file, or is damaged.
            exchange.Dispose();
            Note = $"{file.Name} is not a designs file this app can read: {ex.Message}";
            Changed?.Invoke();

            return false;
        }

        // Only now is anything changed, so a file that would not open leaves the session alone.
        _connected?.Dispose();
        _connected = exchange;
        _router.SwitchTo(exchange);
        _host.Current?.Open(parsed, read.Name);

        _preferences.Set(ChosenKey, $"{file.Id}|{file.Name}");
        Note = null;
        SessionEnded = false;

        // Said here rather than left to a second trip to the header, because choosing the file and
        // deciding whether it writes itself are one decision made in one place.
        if (autoSave is { } wanted)
        {
            _preferences.SetSyncsWithFile(wanted);
        }

        // And turned on for real, which is what makes it survive a reload: the answer is
        // remembered in preferences, but the sync itself could not be started before now because
        // there was nothing in place for it to watch.
        await _sync.SetAsync(_preferences.SyncsWithFile).ConfigureAwait(false);

        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Reconnects to the file chosen last time, where there is one and the session allows.
    /// </summary>
    public async Task<bool> ResumeAsync()
    {
        if (Connected || _preferences.Get(ChosenKey) is not { Length: > 0 } kept)
        {
            return false;
        }

        var bar = kept.IndexOf('|', StringComparison.Ordinal);

        if (bar <= 0)
        {
            return false;
        }

        return await UseAsync(new CloudFile(kept[..bar], kept[(bar + 1)..], string.Empty))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Stops working on the provider's file, and forgets the session at it.
    /// </summary>
    /// <remarks>
    /// The empires stay where they are - what was read is still open, and is now an ordinary
    /// browser session of it. Keeping the file in step is turned off first, because the thing it is
    /// watching is the exchange being taken away.
    /// </remarks>
    public async Task DisconnectAsync()
    {
        await _sync.SetAsync(false).ConfigureAwait(false);

        _router.SwitchTo(_browser);
        _connected?.Dispose();
        _connected = null;

        _preferences.Set(ChosenKey, string.Empty);
        await _auth.SignOutAsync().ConfigureAwait(false);

        // The answer about writing by itself goes with the file it was about. Left set, a later
        // connection to a different file would start writing it without anybody having said so.
        _preferences.SetSyncsWithFile(false);

        Note = null;
        SessionEnded = false;
        Changed?.Invoke();
    }

    /// <summary>Drops the exchange, which stops it asking the provider anything further.</summary>
    public void Dispose()
    {
        _connected?.Dispose();
        _connected = null;
    }
}
