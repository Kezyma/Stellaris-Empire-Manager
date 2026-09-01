using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// Turns the reasons the rules give into sentences a player can act on.
/// </summary>
/// <remarks>
/// Where the game's own script explains a condition, that explanation is used, so the player reads
/// the same words the game would show. The rest are the cases the game states as bare lists with
/// no accompanying text, chiefly the species trait constraints, and those are phrased here.
/// </remarks>
public sealed class ReasonWriter(Localizer localizer)
{
    private readonly Localizer _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

    /// <summary>Writes out a set of reasons, without repeating any.</summary>
    public IReadOnlyList<string> Describe(IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        return [.. reasons.Select(Describe).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Writes out one reason.</summary>
    public string Describe(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var (kind, subject) = RuleReasons.Split(reason);

        return kind switch
        {
            RuleReasons.WrongArchetype => Requires(subject, "Not for this kind of species"),
            RuleReasons.WrongSpeciesClass => Requires(subject, "Not for this species class"),
            RuleReasons.WrongPlanetClass => $"Needs a {Names(subject)} homeworld",
            RuleReasons.WrongOrigin => $"Only with {Names(subject)}",
            RuleReasons.ForbiddenByOrigin => $"Not with {Names(subject)}",
            RuleReasons.WrongEthics => $"Needs {Names(subject)}",
            RuleReasons.ForbiddenByEthics => $"Not with {Names(subject)}",
            RuleReasons.WrongCivics => $"Needs {Names(subject)}",
            RuleReasons.Opposite => $"Excluded by {Names(subject)}",
            RuleReasons.NotEnoughPoints => "Not enough trait points",
            RuleReasons.NoPicksLeft => "No trait slots left",
            RuleReasons.NoCivicSlotsLeft => "No civic slots left - release one first",
            RuleReasons.OriginAlreadyChosen => "An origin is already chosen - clear it first",
            RuleReasons.RulerTraitTaken => "The ruler already has a trait - release it first",
            RuleReasons.WrongLeaderClass => $"For a {Names(subject)} ruler",
            RuleReasons.MissingDlc => $"Needs {subject}",
            RuleReasons.NotEnoughEthicsPoints => "Not enough ethics points",
            RuleReasons.EthicGroupTaken => $"Conflicts with {Names(subject)}",
            RuleReasons.GestaltExclusive => "Gestalt consciousness cannot be combined with other ethics",

            // Anything else is a localisation key the game supplied with the condition itself.
            _ => _localizer.Text(reason),
        };
    }

    /// <summary>
    /// Says what is holding a trait on a species.
    /// </summary>
    /// <remarks>
    /// "Fixed by the species class, authority, civics or origin" named four things and blamed none
    /// of them, leaving the player to work out which by removing things until it went away. The
    /// rules know the answer.
    /// </remarks>
    public string Fixed(ForcedTrait forced)
    {
        ArgumentNullException.ThrowIfNull(forced);

        var cause = forced.Cause is { Length: > 0 } key ? _localizer.Text(key) : null;

        return forced.Source switch
        {
            // "a Continental World homeworld" says world twice; what it means is where they evolved.
            ForcedTraitSource.Homeworld when cause is { Length: > 0 } => $"Fixed by starting on a {cause}.",
            ForcedTraitSource.Homeworld => "Fixed by the homeworld.",
            _ when cause is { Length: > 0 } => $"Fixed by {cause}.",
            ForcedTraitSource.SpeciesClass => "Fixed by the species class.",
            ForcedTraitSource.Authority => "Fixed by the authority.",
            ForcedTraitSource.Civic => "Fixed by a civic.",
            _ => "Fixed by the origin.",
        };
    }

    /// <summary>
    /// Says which species a trait wants, rather than only that this one will not do.
    /// </summary>
    /// <remarks>
    /// A player told "not for this species class" has to go and find out which class would take it.
    /// The rules have the list, so it is named. The fallback is for a definition that restricts a
    /// trait without saying to what, which the game's own files do not currently do.
    /// </remarks>
    private string Requires(string? subject, string fallback)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return fallback;
        }

        var names = Names(subject);
        var article = names.Length > 0 && "AEIOU".Contains(char.ToUpperInvariant(names[0])) ? "an" : "a";

        return $"Requires {article} {names} species";
    }

    /// <summary>Names the things a reason refers to, which may be a list.</summary>
    private string Names(string? subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return "something else";
        }

        var names = subject
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => _localizer.Text(k))
            .ToList();

        return names.Count switch
        {
            0 => "something else",
            1 => names[0],
            2 => $"{names[0]} or {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}",
        };
    }
}
