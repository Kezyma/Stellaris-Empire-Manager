using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// Writes what the address bar says, without going anywhere.
/// </summary>
/// <remarks>
/// <para>
/// Three places in this app put something in the bar purely so the page can be sent to somebody: a
/// wiki entry that has been pressed, the empire the editor is holding, and the clearing of that
/// address when the editor closes. None of them is a navigation. Nothing routes, no page changes,
/// and the component doing it already knows what it means to show.
/// </para>
/// <para>
/// They cannot use <c>NavigationManager.NavigateTo</c> for it. Blazor's internal navigation sets a
/// "scroll to the top after the next render" flag whenever the <em>path</em> differs from the
/// current one, and reads that flag at the end of every applied render batch. Passing
/// <c>replace: true</c> suppresses the history entry, not the scroll - so pressing a card two
/// thirds of the way down a page threw the reader back to the top of it, and opening an empire
/// threw the list back to the top of itself. Only query strings and fragments escape the check, and
/// the addresses here are paths.
/// </para>
/// <para>
/// A service rather than three components each importing the script, which is how the two other
/// small browser errands in this app are arranged - <see cref="Preferences"/> and
/// <c>PageAttention</c> are the same shape, for the same reason: one import, one thing to release,
/// and no component obliged to become <see cref="IAsyncDisposable"/> to reach a one-line function.
/// </para>
/// <para>
/// What this does <em>not</em> do is tell <c>NavigationManager</c>. Its <c>Uri</c> goes stale, which
/// is the deliberate trade: the address is for the reader and for whoever they send it to, and the
/// components that care about the route read it from their own route parameter instead. A back or
/// forward gesture still routes normally, because the browser raises <c>popstate</c> from the real
/// location either way.
/// </para>
/// </remarks>
/// <param name="navigation">What the app's own base address is, to resolve a route against.</param>
/// <param name="js">The browser, or null on a host that has none.</param>
public sealed class AddressBar(NavigationManager navigation, IJSRuntime? js = null) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    /// <summary>
    /// Puts an address in the bar, replacing the entry that is there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replacing rather than pushing, in every one of the three cases. Reading down a list and
    /// pressing four things is one visit, not four, and a Back button that walks a reader back
    /// through their own browsing of one page is a Back button that does not go back.
    /// </para>
    /// <para>
    /// Made absolute first, which <c>NavigateTo</c> did for itself. <c>replaceState</c> resolves a
    /// relative address against the document rather than against the app, and reads the empty
    /// string as "leave it exactly as it is" - so clearing the bar by asking for the base address
    /// did nothing at all, and the empire stayed in it after the editor had closed. Going through
    /// the base also keeps this right under the site's own sub-path on Pages.
    /// </para>
    /// </remarks>
    /// <param name="address">Where the bar should read, relative to the app's base.</param>
    /// <returns>A task that completes once it has been written.</returns>
    public async Task ShowAsync(string address)
    {
        if (js is null)
        {
            return;
        }

        try
        {
            await (await ModuleAsync().ConfigureAwait(false))
                .InvokeVoidAsync("setAddress", navigation.ToAbsoluteUri(address).ToString())
                .ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The page went away mid-press. The address is the least of it.
        }
        catch (JSException)
        {
            // The script would not import. Everything the address was for - sharing, reloading -
            // is already unavailable, and the page itself is fine, so there is nothing to report.
        }
    }

    /// <summary>
    /// The script, imported once.
    /// </summary>
    /// <remarks>
    /// The task is held rather than its result, for the reason <see cref="Preferences"/> gives:
    /// written as <c>_module ??= await …</c>, two callers can both find it null and both import.
    /// </remarks>
    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= js!.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").AsTask();

    /// <summary>Hands the imported script module back, if one was ever imported.</summary>
    /// <returns>A task that completes once it has been released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await (await _module.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The page went away first; there is nothing left to release.
        }
        catch (JSException)
        {
            // The import never succeeded, so it left nothing to release.
        }
    }
}
