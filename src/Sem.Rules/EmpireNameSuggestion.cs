namespace Sem.Rules;

/// <summary>
/// One name the game could give an empire, and how it is put together.
/// </summary>
/// <remarks>
/// Both halves matter. <see cref="Text"/> is what a player reads and picks from a list; the format
/// and its parts are how the game itself stores such a name — <c>name={ key="AofB" variables={ … } }</c>
/// — so a name chosen here can be written back the way the game wrote it, and shown in whatever
/// language the file is later opened in. A name with no format is a plain string and is stored as one.
/// </remarks>
/// <param name="Text">The finished name.</param>
/// <param name="FormatKey">The localisation format it is built from, where it has one.</param>
/// <param name="Parts">What fills that format's blanks, in order.</param>
public sealed record EmpireNameSuggestion(string Text, string? FormatKey, IReadOnlyList<string> Parts);

/// <summary>
/// The things an empire's name can be built out of that are not words from a list.
/// </summary>
/// <remarks>
/// The game's templates make exactly three scripted calls, and every one of them names something the
/// design already holds. They arrive here as finished text rather than as keys because working out
/// what a species is called is the localiser's business, and the rules have no localiser.
/// </remarks>
public sealed class EmpireNameSources
{
    /// <summary>The species adjective, as in "Rethellian".</summary>
    public string? SpeciesAdjective { get; init; }

    /// <summary>The homeworld's name.</summary>
    public string? PlanetName { get; init; }

    /// <summary>The starting system's name.</summary>
    public string? SystemName { get; init; }

    /// <summary>
    /// What a scripted call answers, or nothing when the design has not been given it yet.
    /// </summary>
    /// <remarks>
    /// A call nobody can answer takes its whole shape out of the running, which is the right
    /// outcome: "Empire of " with the system unnamed is not a name.
    /// </remarks>
    public string? Resolve(string call) => call switch
    {
        "This.GetSpeciesAdj" => SpeciesAdjective,
        "This.GetCapitalPlanetNameOrRandom" => PlanetName,
        "This.GetCapitalSystemNameOrRandom" => SystemName,
        _ => null,
    };
}
