using Sem.Clausewitz;
using Sem.Designs;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// What to do with the empires already open when a file is connected to that holds its own.
/// </summary>
/// <remarks>
/// Connecting used to mean the file wins, which is right the first time and wrong every time after:
/// picking a file after an afternoon's work threw the afternoon away, and so did coming back to a
/// connection that had lapsed. None of these writes anything - the choice decides what is in front
/// of you, and the file at the provider is only changed by a save.
/// </remarks>
public enum CloudArrival
{
    /// <summary>The file as it stands. What was open is let go of.</summary>
    TakeTheirs,

    /// <summary>What is open, now pointed at this file, which a save would write over.</summary>
    KeepMine,

    /// <summary>Both, and where one empire is in both, the file's copy is the one kept.</summary>
    TheirsWin,

    /// <summary>Both, and where one empire is in both, the open copy is the one kept.</summary>
    MineWin,
}

/// <summary>
/// Asks what to do when a file arrives holding empires and there are already empires open.
/// </summary>
/// <param name="name">The file being opened, so the question can name it.</param>
/// <param name="holds">How many empires it holds, which is half of what the question weighs.</param>
/// <returns>What to do, or null to stop and leave everything exactly as it was.</returns>
public delegate Task<CloudArrival?> ArrivalQuestion(string name, int holds);

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

    /// <summary>
    /// The file's whole path at the provider, which is the only form that identifies it.
    /// </summary>
    /// <remarks>
    /// The name alone does not. One account backing up two machines holds two files called
    /// user_empire_designs_v3.4.txt, in folders named after each machine, and being shown the name
    /// tells somebody nothing about which of them this app is about to write.
    /// </remarks>
    public string? Where => File is { } file
        ? file.Folder is { Length: > 0 } and not "/" ? $"{file.Folder}/{file.Name}" : $"/{file.Name}"
        : null;

    /// <summary>
    /// The providers on offer, and whether each can be chosen now.
    /// </summary>
    /// <remarks>
    /// One, until there are two. Google Drive was listed here as a disabled row for a while and has
    /// been taken out: a row that cannot be pressed is a promise on screen, and the honest place for
    /// one of those is a note in the docs rather than a control somebody keeps trying.
    ///
    /// The shape stays a list, and the rule that being signed in to one rules out the others is
    /// already written below, so adding the second is adding a row rather than reworking a dialog.
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

        // Anything added here answers the same question the one row does: can it be chosen now,
        // and if not, why not - where being signed in somewhere else is one of the reasons.
        _providers = [new(_provider.Name, Ready: true, signedIn ? "Signed in" : null)];

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
    public async Task<bool> CompleteSignInAsync(string address, ArrivalQuestion? ask = null)
    {
        if (!await _auth.CompleteAsync(address).ConfigureAwait(false))
        {
            // Only worth saying where this load was meant to be the way back. An ordinary load
            // reaches here too, and has nothing to report.
            if (OneDriveAuth.IsReturn(address))
            {
                // The provider's own words where it gave any, from whichever leg refused: the
                // address carries them when the sign-in itself was turned down, and the token
                // endpoint's reply carries them when the code would not exchange. Rolling both
                // into one house sentence threw away the only part that says what to do about it,
                // and left a message that could equally mean any of five different things.
                Note = (OneDriveAuth.RefusalIn(address) ?? _auth.Refusal) is { Length: > 0 } refused
                    ? $"{_provider.Name} would not sign you in: {refused}"
                    : _auth.Trouble is { Length: > 0 } trouble
                        ? trouble
                        : $"Signing in to {_provider.Name} did not finish. Try connecting again.";

                Changed?.Invoke();
            }

            return false;
        }

        await ResumeAsync(ask).ConfigureAwait(false);
        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Whether this address is the provider handing an answer back, of either kind.
    /// </summary>
    /// <remarks>
    /// Asked apart from whether the answer was any good, because the two want different things.
    /// A return that failed still has to have the code taken out of the address and still has to
    /// put the picker up - otherwise pressing the provider again fetches another code onto a page
    /// that will not spend it either, which is a loop with no way out of it and no message.
    /// </remarks>
    public static bool IsSignInReturn(string address) => OneDriveAuth.IsReturn(address);

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
    public async Task<bool> UseAsync(CloudFile file, bool? autoSave = null, ArrivalQuestion? ask = null)
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

        // Asked before anything is touched, and only where there is a real decision to make.
        //
        // Nothing open means nothing to lose, so the file is simply taken. A file holding nothing
        // is the mirror of that and matters more: taking it would empty the list, and the three
        // answers that are not "take it" all come to the same thing, so keeping what is open is
        // the answer rather than a question with one real option. Both sides holding empires is
        // the case worth asking about, and the only one that is asked.
        var mine = _host.Current?.File;
        var arrival = CloudArrival.TakeTheirs;

        // A file that already matches what is open is not a decision, and asking about it would
        // put a question in front of every ordinary reload of a connection that is in step.
        if (mine is { Designs.Count: > 0 } && !Matches(mine, read.Contents))
        {
            if (parsed.Designs.Count == 0)
            {
                arrival = CloudArrival.KeepMine;
            }
            else if (ask is not null)
            {
                if (await ask(file.Name, parsed.Designs.Count).ConfigureAwait(false) is not { } answered)
                {
                    // Stopped rather than refused. Nothing has been changed at this point, so
                    // leaving quietly is the whole of it - no note, because nothing went wrong.
                    exchange.Dispose();

                    return false;
                }

                arrival = answered;
            }
        }

        // Only now is anything changed, so a file that would not open leaves the session alone.
        _connected?.Dispose();
        _connected = exchange;
        _router.SwitchTo(exchange);

        if (_host.Current is { } session)
        {
            switch (arrival)
            {
                // The one answer that leaves nothing owed: what is open is what the file holds.
                case CloudArrival.TakeTheirs:
                    session.Open(parsed, read.Name);
                    break;

                // Opened under the file's name so that Save goes there, and immediately owed to
                // it, because the file still holds something else until a save says otherwise.
                case CloudArrival.KeepMine:
                    session.Open(mine!, read.Name);
                    session.MarkFileUnwritten();
                    break;

                // Merge already means "the one handed in wins", so which of the two is opened
                // first is the entire difference between these. Both come out owing the file.
                case CloudArrival.TheirsWin:
                    session.Open(mine!, read.Name);
                    session.Merge(parsed);
                    break;

                default:
                    session.Open(parsed, read.Name);
                    session.Merge(mine!);
                    break;
            }
        }

        // The folder goes in too. Without it a reload knows which file to reopen and cannot say
        // where it is, so the sheet would name the file and leave the one useful half out.
        _preferences.Set(ChosenKey, $"{file.Id}|{file.Name}|{file.Folder}");
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
    /// Whether what is open would write back as exactly the bytes that were just read.
    /// </summary>
    /// <remarks>
    /// Written out and compared rather than counted or matched name by name, because the question
    /// it answers is "would saving change this file", and only the bytes answer that.
    /// </remarks>
    private static bool Matches(EmpireDesignsFile mine, byte[] theirs)
    {
        try
        {
            return mine.Save().AsSpan().SequenceEqual(theirs);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            // Unable to say they are the same, so say they are not: the cost of asking a question
            // that was not needed is a question, and the cost of skipping one that was is an
            // afternoon's work.
            return false;
        }
    }

    /// <summary>
    /// Reconnects to the file chosen last time, where there is one and the session allows.
    /// </summary>
    public async Task<bool> ResumeAsync(ArrivalQuestion? ask = null)
    {
        if (Connected || _preferences.Get(ChosenKey) is not { Length: > 0 } kept)
        {
            return false;
        }

        // Three parts, and two is tolerated: a choice remembered before the folder was kept should
        // reopen rather than be thrown away for being short.
        var parts = kept.Split('|');

        if (parts.Length < 2 || parts[0].Length == 0)
        {
            return false;
        }

        return await UseAsync(
            new CloudFile(parts[0], parts[1], parts.Length > 2 ? parts[2] : string.Empty), ask: ask)
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

        // What is open belonged to the file while the connection lasted, and belongs to nobody the
        // moment it ends. The browser keeps no copy of a file for a host that saves in place, so
        // everything done since connecting - including whichever way the arrival question was
        // answered - would go with the next reload. Written back now that the browser is holding it
        // again, and after the router has been switched, because that is what decides where it goes.
        await _host.RememberAsync().ConfigureAwait(false);

        _preferences.Set(ChosenKey, string.Empty);
        await _auth.SignOutAsync().ConfigureAwait(false);

        // The answer about writing by itself goes with the file it was about. Left set, a later
        // connection to a different file would start writing it without anybody having said so.
        _preferences.SetSyncsWithFile(false);

        Note = null;
        SessionEnded = false;

        // Rebuilt rather than left as it was. The list carries whether each provider is already
        // signed in, and having just signed out of one, a row still saying so is both wrong and
        // the thing somebody reads to decide whether pressing it will ask them for anything.
        await SignedInAsync().ConfigureAwait(false);

        Changed?.Invoke();
    }

    /// <summary>Drops the exchange, which stops it asking the provider anything further.</summary>
    public void Dispose()
    {
        _connected?.Dispose();
        _connected = null;
    }
}
