using Microsoft.JSInterop;

namespace Sem.Ui.Services;

/// <summary>
/// Small choices about how the designer is arranged, remembered between visits.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IDesignStore"/>, and kept by both hosts rather than one. The desktop
/// refuses to keep a copy of the designs file because the player's own file is the authoritative
/// one and a second copy would be a rival to it; which way round a picker is drawn is nobody's
/// second copy of anything, and someone who prefers a list on the web prefers one on the desktop.
/// </para>
/// <para>
/// Read once at startup and held, so a component can ask during a render. Writing goes to the
/// browser without being waited on: a preference that did not reach the store is a preference lost
/// at the end of the visit, which is not worth making anybody wait for.
/// </para>
/// </remarks>
public sealed class Preferences(IJSRuntime? js = null) : IAsyncDisposable
{
    /// <summary>What they are filed under, versioned so a change of shape cannot be misread.</summary>
    private const string StorageKey = "sem.prefs.v1";

    /// <summary>How a picker's setting is named, and the two answers it can have.</summary>
    private const string PickerPrefix = "picker.";
    private const string ListView = "list";
    private const string GridView = "grid";

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    private Task<IJSObjectReference>? _module;

    /// <summary>Reads what was kept, if anything was and if there is anywhere to read it from.</summary>
    public async Task LoadAsync()
    {
        if (js is null)
        {
            return;
        }

        try
        {
            var kept = await (await ModuleAsync().ConfigureAwait(false))
                .InvokeAsync<string?>("readStored", StorageKey)
                .ConfigureAwait(false);

            Parse(kept);
        }
        catch (JSException)
        {
            // Storage can be switched off. Starting with the defaults is the whole cost.
        }
    }

    /// <summary>What is remembered for a setting, or null when nothing is.</summary>
    public string? Get(string key) => _values.GetValueOrDefault(key);

    /// <summary>
    /// Whether a picker is drawn as a list of names rather than as a grid of symbols.
    /// </summary>
    /// <remarks>
    /// The list is what an unanswered question means: eighty-one civics as unlabelled icons is a
    /// memory test, so the grid is the thing you ask for. Each picker keeps its own answer, since
    /// origins carry a picture worth seeing and civics do not.
    /// </remarks>
    public bool PickerIsList(string picker) => Get(PickerPrefix + picker) is not GridView;

    /// <summary>Remembers how a picker is drawn.</summary>
    public void SetPickerView(string picker, bool list) =>
        Set(PickerPrefix + picker, list ? ListView : GridView);

    /// <summary>Remembers a setting, for this visit and for the next one.</summary>
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        if (_values.TryGetValue(key, out var already) && string.Equals(already, value, StringComparison.Ordinal))
        {
            return;
        }

        _values[key] = value;
        _ = SaveAsync();
    }

    /// <summary>
    /// One <c>name=value</c> line per setting.
    /// </summary>
    /// <remarks>
    /// Both halves are the application's own short words — the name of a picker, the name of a way
    /// of drawing it — never anything the player typed, so there is nothing here to escape. A line
    /// that does not look like one is dropped rather than guessed at, which is what makes a
    /// half-written store harmless.
    /// </remarks>
    private void Parse(string? kept)
    {
        if (kept is not { Length: > 0 })
        {
            return;
        }

        foreach (var line in kept.Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0)
            {
                _values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }
    }

    private async Task SaveAsync()
    {
        if (js is null)
        {
            return;
        }

        try
        {
            await (await ModuleAsync().ConfigureAwait(false))
                .InvokeVoidAsync("writeStored", StorageKey, string.Join('\n', _values.Select(v => $"{v.Key}={v.Value}")))
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage switched off, or full. Nobody is waiting to be told.
        }
        catch (JSDisconnectedException)
        {
            // The page went away between the choice and the writing of it.
        }
    }

    /// <summary>
    /// The script module, imported once however many callers ask for it at once.
    /// </summary>
    /// <remarks>
    /// The import is held as the task rather than its result. Written as
    /// <c>_module ??= await Import()</c> the check and the assignment sat either side of an await,
    /// so a second caller arriving during the import saw no module and started another - and the
    /// reference the loser assigned was overwritten and never released. Holding the task closes it
    /// without a lock: there is nothing to yield to between the test and the store.
    /// </remarks>
    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= js!.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sem.Ui/sem.js").AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
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
                // The import never succeeded, so it left nothing to release. Reachable only now
                // that the task is what is held: a failed import used to leave the field null.
            }
        }
    }
}
