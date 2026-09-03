namespace Sem.Ui.Components;

/// <summary>
/// A part of the empire the lite editor can open an editor for.
/// </summary>
/// <remarks>
/// One member per dialog, not one per field. Several things on the card open the same editor because
/// the game treats them as one decision - the ethics, the authority and the civics are all the
/// government, and the homeworld's class, its name, its system and a nomad's arkship are all where
/// the empire starts. Splitting those would mean opening three windows to answer one question.
/// </remarks>
public enum LitePart
{
    /// <summary>The empire's own settings: adjective, ship prefix, special flags, spawn rules.</summary>
    Empire,

    /// <summary>Ethics, authority, civics, and whether the empire is nomadic.</summary>
    Government,

    /// <summary>The origin.</summary>
    Origin,

    /// <summary>The founder species: its names, its class, its name list and its story.</summary>
    Species,

    /// <summary>Its portrait, its gender, and whether the game may reuse the likeness.</summary>
    Portrait,

    /// <summary>Its traits.</summary>
    Traits,

    /// <summary>The second species some origins bring, where there is one.</summary>
    SecondSpecies,

    /// <summary>The ruler: name, class, traits, titles, appearance and biography.</summary>
    Ruler,

    /// <summary>Where the empire starts - the world, its name, the system, and a nomad's arkship.</summary>
    Homeworld,

    /// <summary>The flag: its colours, its pattern and its emblem.</summary>
    Flag,

    /// <summary>The room the ruler stands in, and the city on the world behind them.</summary>
    Room,

    /// <summary>The ships the empire flies.</summary>
    Shipset,

    /// <summary>The voice that narrates its game.</summary>
    Advisor,
}
