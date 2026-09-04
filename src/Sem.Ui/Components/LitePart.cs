namespace Sem.Ui.Components;

/// <summary>
/// A part of the empire the lite editor can open an editor for.
/// </summary>
/// <remarks>
/// <para>
/// One member per editor, and an editor covers exactly what you pressed. Pressing the ethics opens
/// the ethics and not the authority beside them; pressing the species' name opens its names and not
/// its portrait and traits as well.
/// </para>
/// <para>
/// The exceptions are the three places the game itself treats several fields as one decision: the
/// homeworld's class, its name, its system and a nomad's arkship are all where the empire starts,
/// and the room and the city are one picture. Splitting those would mean opening three windows to
/// answer one question.
/// </para>
/// </remarks>
public enum LitePart
{
    /// <summary>What the empire is called: its name, its adjective and its ship prefix.</summary>
    Naming,

    /// <summary>What it believes.</summary>
    Ethics,

    /// <summary>How it is run, and whether it stays still to do it.</summary>
    Authority,

    /// <summary>What it does with the room its ethics and authority leave.</summary>
    Civics,

    /// <summary>All three at once, for the government name they add up to.</summary>
    Government,

    /// <summary>The origin.</summary>
    Origin,

    /// <summary>The founder species' names, its name list and its story.</summary>
    Species,

    /// <summary>Its portrait, its class, its gender, and whether the game may reuse the likeness.</summary>
    Portrait,

    /// <summary>Its traits.</summary>
    Traits,

    /// <summary>The second species some origins bring, where there is one.</summary>
    SecondSpecies,

    /// <summary>Who the ruler is: name, titles, gender, class and biography.</summary>
    Ruler,

    /// <summary>How the ruler looks.</summary>
    RulerAppearance,

    /// <summary>What the ruler is good at.</summary>
    RulerTraits,

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
