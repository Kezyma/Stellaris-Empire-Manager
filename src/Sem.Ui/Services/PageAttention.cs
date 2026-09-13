using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// Whether the app is in front of the player, and when it comes back.
/// </summary>
/// <remarks>
/// <para>
/// For anything that asks a provider something on a timer. A tab nobody is looking at should not be
/// spending somebody's data on a question whose answer nobody can see, and a tab somebody has just
/// come back to should not make them wait out the rest of an interval to find out what changed
/// while they were away. Both halves are the same fact, so they are answered in one place.
/// </para>
/// <para>
/// Browsers throttle timers in a hidden tab, and that is not a substitute for this. It reduces
/// rather than stops, the rules differ by browser and version, a frozen page can fire several
/// pending delays together on resume - and, decisively, throttling is a latency mechanism, so the
/// tab you come back to has a delayed look pending that may not arrive for a minute. That minute of
/// staleness is the thing this exists to remove.
/// </para>
/// <para>
/// A null runtime means permanently attended and never returning, which is what the desktop gets -
/// its document is always visible inside the embedded browser - and what a test gets for free.
/// </para>
/// </remarks>
public sealed class PageAttention(IJSRuntime? js = null) : IAsyncDisposable
{
    private readonly IJSRuntime? _js = js;

    private Task<IJSObjectReference>? _module;
    private DotNetObjectReference<PageAttention>? _self;

    /// <summary>Whether the app is in front of the player.</summary>
    public bool Attended { get; private set; } = true;

    /// <summary>
    /// Raised when it comes back to the front, or the network does.
    /// </summary>
    /// <remarks>
    /// Not the same event as <see cref="Attended"/> turning true, though that raises it. Regaining
    /// focus without ever having been hidden, and coming back online, are both moments when what is
    /// on screen may have gone stale, and neither says anything about visibility.
    /// </remarks>
    public event Action? Returned;

    /// <summary>Starts listening. Asked more than once, does nothing further.</summary>
    public async Task StartAsync()
    {
        if (_js is null || _module is not null)
        {
            return;
        }

        try
        {
            _self = DotNetObjectReference.Create(this);

            var module = await ModuleAsync().ConfigureAwait(false);

            // The page answers with what it can see right now, so this starts from the truth
            // rather than from an assumption that happens to be right most of the time.
            Attended = await module
                .InvokeAsync<bool>("watchAttention", _self)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // Nothing to listen with. Everything that asks carries on at its own pace, which is
            // what it did before any of this existed.
            _module = null;
        }
    }

    /// <summary>Told by the page that it became visible, or stopped being.</summary>
    [JSInvokable]
    public void Attend(bool attended)
    {
        var returning = attended && !Attended;

        Attended = attended;

        if (returning)
        {
            Returned?.Invoke();
        }
    }

    /// <summary>
    /// Told by the page that it is worth looking again, without its visibility having changed.
    /// </summary>
    /// <remarks>
    /// Window focus, coming back online, and a page restored from the back-forward cache. The
    /// desktop is the reason this is separate: inside the embedded browser the document is always
    /// visible, so it would never see a visibility change, and window activation is still a moment
    /// when the file underneath may have moved.
    /// </remarks>
    [JSInvokable]
    public void Reconnected() => Returned?.Invoke();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            _self?.Dispose();

            return;
        }

        try
        {
            var module = await _module.ConfigureAwait(false);

            await module.InvokeVoidAsync("stopWatchingAttention").ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            // The page went away first, which is the usual way this ends. There is nothing left to
            // stop listening to and nothing left to release.
        }
        finally
        {
            _self?.Dispose();
        }
    }

    /// <summary>
    /// The script module, imported once however many callers ask for it at once.
    /// </summary>
    /// <remarks>
    /// Held as the task rather than its result, for the reason the other services that do this
    /// record: written as <c>_module ??= await Import()</c> the check and the assignment sit either
    /// side of an await, so a second caller arriving during the import starts another, and the
    /// reference the loser assigns is overwritten and never released.
    /// </remarks>
    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= _js!.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").AsTask();
}
