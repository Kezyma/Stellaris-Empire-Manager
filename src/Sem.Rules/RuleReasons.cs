namespace Sem.Rules;

/// <summary>
/// Why the rules blocked something, for the cases the game's own script does not explain.
/// </summary>
/// <remarks>
/// Where the game supplies a localisation key with its condition, that is used instead: the player
/// gets the game's own wording. These cover what is left, chiefly the species trait constraints,
/// which the files express as plain lists with no accompanying text. The interface turns them into
/// sentences, so the wording lives where it can be translated rather than in the rules.
/// </remarks>
public static class RuleReasons
{
    /// <summary>Separates a reason from the thing it refers to.</summary>
    public const char Separator = ':';

    /// <summary>
    /// The species' archetype cannot take this trait. Followed by the archetypes it allows.
    /// </summary>
    public const string WrongArchetype = "sem.trait.archetype";

    /// <summary>
    /// The species' class cannot take this trait. Followed by the classes it allows.
    /// </summary>
    public const string WrongSpeciesClass = "sem.trait.species_class";

    /// <summary>The trait needs a different homeworld. Followed by the classes it allows.</summary>
    public const string WrongPlanetClass = "sem.trait.planet_class";

    /// <summary>The trait needs a different origin.</summary>
    public const string WrongOrigin = "sem.trait.origin";

    /// <summary>The chosen origin rules the trait out.</summary>
    public const string ForbiddenByOrigin = "sem.trait.forbidden_origin";

    /// <summary>The trait needs different ethics.</summary>
    public const string WrongEthics = "sem.trait.ethics";

    /// <summary>The empire's ethics rule the trait out.</summary>
    public const string ForbiddenByEthics = "sem.trait.forbidden_ethics";

    /// <summary>The trait needs a civic the empire does not have.</summary>
    public const string WrongCivics = "sem.trait.civics";

    /// <summary>Another trait already taken excludes this one. Followed by that trait's key.</summary>
    public const string Opposite = "sem.trait.opposite";

    /// <summary>There are not enough trait points left.</summary>
    public const string NotEnoughPoints = "sem.trait.points";

    /// <summary>The species already has as many traits as it may.</summary>
    public const string NoPicksLeft = "sem.trait.picks";

    /// <summary>The empire already has as many civics as it may.</summary>
    /// <remarks>
    /// Separate from <see cref="NoPicksLeft"/> because it is a different sentence to a reader, and
    /// because a civic is not spent from a pool of points the way a trait is: there are two slots,
    /// and a third choice used to take the place of the first without saying so.
    /// </remarks>
    public const string NoCivicSlotsLeft = "sem.civic.picks";

    /// <summary>An origin is already chosen, and an empire has only the one.</summary>
    public const string OriginAlreadyChosen = "sem.origin.chosen";

    /// <summary>The ruler already has their trait, and a ruler has only the one.</summary>
    public const string RulerTraitTaken = "sem.ruler.trait";

    /// <summary>
    /// The trait belongs to another kind of leader. Followed by the classes that may hold it.
    /// </summary>
    public const string WrongLeaderClass = "sem.ruler.class";

    /// <summary>A content pack is needed. Followed by its name.</summary>
    public const string MissingDlc = "sem.dlc";

    /// <summary>There are not enough ethics points left.</summary>
    public const string NotEnoughEthicsPoints = "sem.ethic.points";

    /// <summary>An ethic from the same opposing group is already taken. Followed by its key.</summary>
    public const string EthicGroupTaken = "sem.ethic.group";

    /// <summary>Gestalt consciousness cannot share an empire with any other ethic.</summary>
    public const string GestaltExclusive = "sem.ethic.gestalt";

    /// <summary>
    /// Whether a reason is about having run out of room, rather than about what the empire is.
    /// </summary>
    /// <remarks>
    /// The distinction the interface is built on. What the game forbids an empire is a standing
    /// objection and reads as one; a full slate is undone by letting something go, and saying so in
    /// the same grey as the other would tell a player their whole list had gone unavailable at the
    /// moment they were deciding what to release. These are also the ones already counted out on
    /// the bars above the list, which is why the trait picker declines to say them twice.
    /// </remarks>
    public static bool IsBudget(string reason) =>
        Split(reason).Kind is NotEnoughPoints
            or NoPicksLeft
            or NotEnoughEthicsPoints
            or NoCivicSlotsLeft
            or OriginAlreadyChosen
            or RulerTraitTaken;

    /// <summary>Builds a reason that refers to something, such as the trait that excludes it.</summary>
    public static string For(string reason, string subject) => $"{reason}{Separator}{subject}";

    /// <summary>Splits a reason into its kind and what it refers to.</summary>
    public static (string Kind, string? Subject) Split(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var index = reason.IndexOf(Separator, StringComparison.Ordinal);
        return index < 0 ? (reason, null) : (reason[..index], reason[(index + 1)..]);
    }
}
