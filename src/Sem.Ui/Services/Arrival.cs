namespace Sem.Ui.Services;

/// <summary>
/// What to do with the empires already open when a file arrives holding its own.
/// </summary>
/// <remarks>
/// <para>
/// One vocabulary for both of the ways a file arrives: imported from disk, or connected to at a
/// cloud provider. They are the same question - these empires, those empires, what survives - and
/// answering it two different ways in two places is how somebody learns that Import's "merge" and
/// the cloud's "merge" are not quite the same thing.
/// </para>
/// <para>
/// None of these writes anything anywhere. Each decides what is in front of you; a file on disk is
/// only written by an export, and a file at a provider only by a save.
/// </para>
/// <para>
/// Whichever is chosen, the order of what is already open survives and anything new is appended.
/// The answer decides which copy of an empire held by both sides is kept, and never where anything
/// ends up, so the list does not rearrange itself as a side effect of the choice.
/// </para>
/// </remarks>
public enum Arrival
{
    /// <summary>The arriving file as it stands. What was open is let go of.</summary>
    TakeTheirs,

    /// <summary>
    /// What is open, unchanged.
    /// </summary>
    /// <remarks>
    /// Only worth offering where keeping it still leaves something changed - connecting points
    /// Save at the file, so keeping your own empires is a decision with a consequence. An import
    /// has no such consequence, so there it would be a second way of saying cancel.
    /// </remarks>
    KeepMine,

    /// <summary>Both, and where one empire is in both, the arriving copy is the one kept.</summary>
    TheirsWin,

    /// <summary>Both, and where one empire is in both, the open copy is the one kept.</summary>
    MineWin,
}

/// <summary>
/// Asks what to do when a file arrives holding empires and there are already empires open.
/// </summary>
/// <param name="name">The file arriving, so the question can name it.</param>
/// <param name="holds">How many empires it holds, which is half of what the question weighs.</param>
/// <returns>What to do, or null to stop and leave everything exactly as it was.</returns>
public delegate Task<Arrival?> ArrivalQuestion(string name, int holds);
