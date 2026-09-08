namespace Sem.Ui.Components;

/// <summary>Which part of a plan an editor is for.</summary>
/// <remarks>
/// The traditions and the perks share one, because they share an order: a tree is opened, finished,
/// and the perk it earns is spent. The civics are a reform of the government rather than a step
/// along that path, so they have an editor to themselves.
/// </remarks>
public enum PlanPart
{
    /// <summary>The tradition trees, and the editor opens on them.</summary>
    Traditions,

    /// <summary>The same editor, opened on the ascension perks.</summary>
    Perks,

    /// <summary>The civics a government reform would leave behind, on their own.</summary>
    Civics,
}
