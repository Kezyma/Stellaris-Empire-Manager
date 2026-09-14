namespace Sem.Ui.Components;

/// <summary>
/// What a cycling toggle says while it is on one of its three answers.
/// </summary>
/// <remarks>
/// Both together because they have to agree. The word on the button says what is being shown and
/// the tip says what pressing it would do, and a control that names one of those in the label and
/// the other in the tip is a control that has to be worked out rather than read.
/// </remarks>
/// <param name="Name">The word on the button, which names what is showing.</param>
/// <param name="Told">The tip and the spoken label, which say what pressing it would do.</param>
public sealed record ToggleWording(string Name, string Told);
