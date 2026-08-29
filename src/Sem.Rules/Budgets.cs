namespace Sem.Rules;

/// <summary>Points spent against points available.</summary>
/// <param name="Spent">How much the current selections cost.</param>
/// <param name="Available">How much there is to spend.</param>
public readonly record struct Budget(int Spent, int Available)
{
    /// <summary>How much is left. Negative when overspent.</summary>
    public int Remaining => Available - Spent;

    /// <summary>Whether the selections cost more than is available.</summary>
    public bool IsOverspent => Spent > Available;
}

/// <summary>
/// What a species may spend on traits.
/// </summary>
/// <remarks>
/// Two limits apply at once: points, which drawbacks give back, and the number of picks. Traits
/// costing nothing are free of the pick limit as well, which is how a species class's own trait
/// does not eat into the allowance.
/// </remarks>
/// <param name="Points">Trait points spent against those available.</param>
/// <param name="Picks">Traits taken against the number allowed.</param>
public readonly record struct TraitBudget(Budget Points, Budget Picks)
{
    /// <summary>Whether either limit has been exceeded.</summary>
    public bool IsExceeded => Points.IsOverspent || Picks.IsOverspent;
}

/// <summary>Where in a design a problem was found.</summary>
public enum ValidationArea
{
    /// <summary>The founder species' class, portrait or name.</summary>
    Species,

    /// <summary>The founder species' traits.</summary>
    Traits,

    /// <summary>The empire's ethics.</summary>
    Ethics,

    /// <summary>The empire's authority.</summary>
    Authority,

    /// <summary>The empire's civics.</summary>
    Civics,

    /// <summary>The empire's origin.</summary>
    Origin,

    /// <summary>The homeworld and starting system.</summary>
    Homeworld,

    /// <summary>The second species some origins require.</summary>
    SecondarySpecies,

    /// <summary>The starting ruler.</summary>
    Ruler,
}

/// <summary>How much a validation problem matters.</summary>
public enum ValidationSeverity
{
    /// <summary>The game would not accept this. The design cannot be used as it stands.</summary>
    Error,

    /// <summary>
    /// The game accepts it but something is not what it appears. Chiefly a field the chosen origin
    /// overrides anyway, which the game silently ignores at game start rather than rejecting.
    /// </summary>
    Warning,
}

/// <summary>Something about a design worth telling the player.</summary>
/// <param name="Area">Which part of the design the problem is in.</param>
/// <param name="Key">The offending selection, when there is one.</param>
/// <param name="Message">A plain description of the problem.</param>
/// <param name="Reasons">
/// Localisation keys carrying the game's own explanation, when its script provided one.
/// </param>
/// <param name="Severity">Whether this blocks the design or merely deserves mentioning.</param>
public sealed record ValidationProblem(
    ValidationArea Area,
    string? Key,
    string Message,
    IReadOnlyList<string> Reasons,
    ValidationSeverity Severity = ValidationSeverity.Error)
{
    public override string ToString() => Key is null ? $"{Area}: {Message}" : $"{Area} ({Key}): {Message}";
}

/// <summary>Everything wrong with a design, or nothing.</summary>
public sealed record ValidationReport(IReadOnlyList<ValidationProblem> Problems)
{
    /// <summary>Whether the game would accept the design. Warnings do not make it invalid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>The problems that would stop the design being used.</summary>
    public IReadOnlyList<ValidationProblem> Errors =>
        [.. Problems.Where(p => p.Severity == ValidationSeverity.Error)];

    /// <summary>The problems worth mentioning that do not block the design.</summary>
    public IReadOnlyList<ValidationProblem> Warnings =>
        [.. Problems.Where(p => p.Severity == ValidationSeverity.Warning)];

    public override string ToString() => Problems.Count == 0
        ? "valid"
        : string.Join("; ", Problems.Select(p => p.ToString()));
}

/// <summary>
/// One option the designer can offer, with everything the interface needs to present it.
/// </summary>
/// <param name="Key">The option's key.</param>
/// <param name="Visible">
/// Whether to show it at all. The game hides options that make no sense for the empire so far,
/// rather than listing every civic in the game as unavailable.
/// </param>
/// <param name="Enabled">Whether it can be chosen right now.</param>
/// <param name="Reasons">Localisation keys explaining why it cannot, when it cannot.</param>
/// <param name="Cost">What it costs, for options that cost something.</param>
/// <param name="RequiredDlc">The content pack it needs, when the player does not own it.</param>
public sealed record OptionState(
    string Key,
    bool Visible,
    bool Enabled,
    IReadOnlyList<string> Reasons,
    int Cost = 0,
    string? RequiredDlc = null)
{
    /// <summary>An option that can be chosen.</summary>
    public static OptionState Available(string key, int cost = 0) => new(key, true, true, [], cost);
}

/// <summary>One tab of the portrait picker, holding the portraits it shows.</summary>
/// <param name="Key">The category's key.</param>
/// <param name="NameKey">Localisation key for the tab's label.</param>
/// <param name="Portraits">
/// The portraits, in the game's own order. That order is deliberate and must not be sorted.
/// </param>
public sealed record PortraitGroup(string Key, string NameKey, IReadOnlyList<OptionState> Portraits);
