using Microsoft.JSInterop;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the app remembers between visits, and what happens when it cannot.
/// </summary>
/// <remarks>
/// None of it matters enough to interrupt somebody over: a browser with storage switched off should
/// start on the defaults and say nothing. What must not happen is a failure here being reported as
/// something else, which is what the missing catch below produced - a page closing mid-read came
/// out as a message about the designs file being unreadable.
/// </remarks>
public sealed class PreferencesTests
{
    /// <summary>With nowhere to read from, it starts on the defaults and says nothing.</summary>
    [Fact]
    public async Task WithNoPageAtAllItLoadsNothingAndComplainsAboutNothing()
    {
        var preferences = new Preferences();

        await preferences.LoadAsync();

        Assert.Null(preferences.Get("anything"));
    }

    /// <summary>
    /// Storage being switched off costs the defaults and nothing else.
    /// </summary>
    [Fact]
    public async Task StorageBeingRefusedLeavesTheDefaults()
    {
        var preferences = new Preferences(new Throwing(() => new JSException("storage is off")));

        await preferences.LoadAsync();

        Assert.Null(preferences.Get("save.sync"));
    }

    /// <summary>
    /// And the page going away mid-read is the same kind of nothing.
    /// </summary>
    /// <remarks>
    /// This one escaped. <c>SaveAsync</c> has always caught it and this had not, so it went up into
    /// the session host's own catch and was reported to the player as their designs file being
    /// unreadable - a sentence about the one thing that was fine.
    /// </remarks>
    [Fact]
    public async Task ThePageGoingAwayMidReadIsNotAnErrorAboutSomethingElse()
    {
        var preferences = new Preferences(
            new Throwing(() => new JSDisconnectedException("the circuit is closed")));

        // The assertion is that this returns rather than throwing into whoever called it.
        await preferences.LoadAsync();

        Assert.Null(preferences.Get("save.sync"));
    }

    /// <summary>A runtime whose module throws whatever the test asks for.</summary>
    private sealed class Throwing(Func<Exception> thrown) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            identifier == "import"
                ? ValueTask.FromResult((TValue)(object)new ThrowingModule(thrown))
                : ValueTask.FromResult<TValue>(default!);
    }

    /// <summary>A module that refuses everything asked of it.</summary>
    private sealed class ThrowingModule(Func<Exception> thrown) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw thrown();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
