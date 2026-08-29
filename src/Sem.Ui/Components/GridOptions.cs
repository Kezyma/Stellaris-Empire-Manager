using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Services;

namespace Sem.Ui.Components;

/// <summary>
/// Turns what the rules say about a set of choices into what a grid needs to draw them.
/// </summary>
/// <remarks>
/// Every section of the designer asks the same question of a different list — what is it called,
/// what does it look like, what does it do, may I have it and why not — so the assembling is done
/// once here rather than in each section.
/// </remarks>
public static class GridOptions
{
    /// <summary>Builds the grid entries for a list of choices.</summary>
    /// <param name="session">The session, for names, icons and explanations.</param>
    /// <param name="states">What the rules say about each choice.</param>
    /// <param name="describe">Where to find a choice's picture and its effects.</param>
    /// <param name="isSelected">Whether the empire has it.</param>
    /// <param name="isFixed">
    /// Whether something else has already decided it, such as a trait an origin forces. Those are
    /// shown as chosen but cannot be unchosen.
    /// </param>
    public static IReadOnlyList<OptionGrid.GridOption> Build(
        DesignSession session,
        IEnumerable<OptionState> states,
        Func<string, (string? Icon, EffectSet? Effects)> describe,
        Func<string, bool> isSelected,
        Func<string, bool>? isFixed = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(describe);
        ArgumentNullException.ThrowIfNull(isSelected);

        return
        [
            .. states.Where(s => s.Visible).Select(state =>
            {
                var (icon, effects) = describe(state.Key);
                var locked = isFixed?.Invoke(state.Key) ?? false;

                return new OptionGrid.GridOption(state.Key, session.Localizer.Text(state.Key))
                {
                    Description = session.Localizer.Html($"{state.Key}_desc", string.Empty),
                    Effects = effects,
                    Icon = icon,
                    Cost = state.Cost == 0 ? null : state.Cost,
                    Selected = isSelected(state.Key),
                    Enabled = state.Enabled && !locked,
                    Reasons = Reasons(session, state, locked),
                };
            })
        ];
    }

    /// <summary>
    /// Why a choice is unavailable, in the game's own words where it has any.
    /// </summary>
    private static IReadOnlyList<string> Reasons(DesignSession session, OptionState state, bool locked)
    {
        var reasons = session.Reasons.Describe(state.Reasons).ToList();

        if (locked)
        {
            reasons.Insert(0, "Fixed by another choice.");
        }

        if (state.RequiredDlc is { Length: > 0 } dlc)
        {
            reasons.Add($"Requires {dlc}.");
        }

        return reasons;
    }
}
