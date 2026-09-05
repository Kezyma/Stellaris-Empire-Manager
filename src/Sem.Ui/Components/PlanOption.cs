using Sem.GameData;

namespace Sem.Ui.Components;

/// <summary>
/// One thing a plan may name, ready to be drawn.
/// </summary>
/// <remarks>
/// Flattened out of whatever it came from, so the picker that draws it does not have to know whether
/// it is looking at an ascension perk or a tradition tree. The two differ in where they are read
/// from and in nothing a reader can see.
/// </remarks>
/// <param name="Key">What the game calls it.</param>
/// <param name="Name">What the player calls it.</param>
/// <param name="Lore">The game's own description, as markup.</param>
/// <param name="Icon">Where its picture was written, if it has one.</param>
/// <param name="Effects">What it does, where the game says.</param>
/// <param name="Enabled">Whether it may be taken.</param>
/// <param name="Reasons">Why not, in the game's own words, when it may not.</param>
/// <param name="Unavailable">
/// Whether the objection is a standing one rather than the plan merely being full. Only these sink
/// to the bottom of the list: burying what a reader is choosing between is exactly backwards while
/// they are deciding what to give up.
/// </param>
public sealed record PlanOption(
    string Key,
    string Name,
    string? Lore,
    string? Icon,
    EffectSet? Effects,
    bool Enabled,
    IReadOnlyList<string> Reasons,
    bool Unavailable = false);
