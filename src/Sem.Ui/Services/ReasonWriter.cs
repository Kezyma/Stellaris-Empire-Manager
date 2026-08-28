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
            RuleReasons.WrongArchetype => "Not for this kind of species",
            RuleReasons.WrongSpeciesClass => "Not for this species class",
            RuleReasons.WrongPlanetClass => $"Needs a {Names(subject)} homeworld",
            RuleReasons.WrongOrigin => $"Only with {Names(subject)}",
            RuleReasons.ForbiddenByOrigin => $"Not with {Names(subject)}",
            RuleReasons.WrongEthics => $"Needs {Names(subject)}",
            RuleReasons.ForbiddenByEthics => $"Not with {Names(subject)}",
            RuleReasons.WrongCivics => $"Needs {Names(subject)}",
            RuleReasons.Opposite => $"Excluded by {Names(subject)}",
            RuleReasons.NotEnoughPoints => "Not enough trait points",
            RuleReasons.NoPicksLeft => "No trait slots left",
            RuleReasons.MissingDlc => $"Needs {subject}",
            RuleReasons.NotEnoughEthicsPoints => "Not enough ethics points",
            RuleReasons.EthicGroupTaken => $"Conflicts with {Names(subject)}",
            RuleReasons.GestaltExclusive => "Gestalt consciousness cannot be combined with other ethics",

            // Anything else is a localisation key the game supplied with the condition itself.
            _ => _localizer.Text(reason),
        };
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
