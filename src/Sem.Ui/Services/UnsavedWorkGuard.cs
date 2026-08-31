namespace Sem.Ui.Services;

/// <summary>
/// Asks before something abandons work that has not been saved.
/// </summary>
/// <remarks>
/// <para>
/// The designer knows what unsaved work looks like and owns the question to ask about it, but it is
/// no longer the only thing that can throw that work away: the header can start a new empire or open
/// a different file from any page, and both of those leave whatever was being edited behind.
/// </para>
/// <para>
/// So the question is registered here and asked from wherever it is needed. With no designer open
/// nothing is registered and everything goes ahead, which is the right answer when there is nothing
/// in hand to lose.
/// </para>
/// </remarks>
public sealed class UnsavedWorkGuard
{
    /// <summary>
    /// What to ask, and how. Set by whatever page is holding unsaved work, and cleared when it goes.
    /// </summary>
    /// <remarks>
    /// Answers true to carry on and false to stop. The page is expected to have dealt with the work
    /// itself before answering true — saved it, or put it back — so that a caller only has to know
    /// whether it may proceed.
    /// </remarks>
    public Func<Task<bool>>? Ask { get; set; }

    /// <summary>Whether to go ahead with something that would abandon the current empire.</summary>
    public Task<bool> ConfirmAsync() => Ask?.Invoke() ?? Task.FromResult(true);
}
