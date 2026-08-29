using System.Text;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// Says in words when a conditional modifier applies.
/// </summary>
/// <remarks>
/// The game does not write these out — it evaluates the condition and shows the result — so the
/// wording here is ours. It only has to be good enough to tell the player whether a bonus is one
/// their empire will have, which is mostly a matter of naming the choice involved.
/// </remarks>
public sealed class ConditionWriter(Localizer localizer)
{
    private readonly Localizer _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

    /// <summary>How deep to go before summarising rather than enumerating.</summary>
    private const int MaxDepth = 3;

    /// <summary>
    /// Describes a condition, or returns null when there is nothing worth saying.
    /// </summary>
    public string? Describe(Requirement? requirement)
    {
        if (requirement is null or AlwaysRequirement { Value: true })
        {
            return null;
        }

        // The game's own explanation is better than anything derived from the tree, where it exists.
        if (requirement.FailureText is { Length: > 0 } text && _localizer.Has(text))
        {
            return _localizer.Text(text);
        }

        var written = Write(requirement, depth: 0, negated: false);
        return string.IsNullOrWhiteSpace(written) ? null : written;
    }

    private string? Write(Requirement requirement, int depth, bool negated)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        return requirement switch
        {
            AlwaysRequirement always => always.Value == !negated ? null : "never",

            NotRequirement not => Write(not.Item, depth, !negated),

            AllRequirement all => Join(all.Items, depth, negated, negated ? " or " : " and "),

            AnyRequirement any => Join(any.Items, depth, negated, negated ? " and " : " or "),

            SelectionRequirement selection => Phrase(
                negated ? "not" : null,
                Category(selection.Category),
                _localizer.Text(selection.Key)),

            DlcRequirement dlc => negated ? $"without {dlc.Name}" : $"with {dlc.Name}",

            FieldRequirement field => Phrase(negated ? "not" : null, Words(field.Field), Words(field.Value)),

            PredicateRequirement predicate => negated
                ? $"not {Words(predicate.Name)}"
                : Words(predicate.Name),

            // Something only a game in progress could answer. Saying so is more use than saying
            // nothing, because it tells the player this is not a bonus they start with.
            UnknownRequirement unknown => Words(unknown.Name),

            _ => null,
        };
    }

    private string? Join(IReadOnlyList<Requirement> items, int depth, bool negated, string separator)
    {
        var parts = items
            .Select(i => Write(i, depth + 1, negated))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return parts.Count == 0 ? null : string.Join(separator, parts);
    }

    private static string Phrase(string? prefix, string subject, string value)
    {
        var builder = new StringBuilder();

        if (prefix is { Length: > 0 })
        {
            builder.Append(prefix).Append(' ');
        }

        return builder.Append(subject).Append(' ').Append(value).ToString();
    }

    private static string Category(SelectionCategory category) => category switch
    {
        SelectionCategory.Ethics => "ethic",
        SelectionCategory.Authority => "authority",
        SelectionCategory.Civics => "civic",
        SelectionCategory.Origin => "origin",
        SelectionCategory.Traits => "trait",
        SelectionCategory.SpeciesClass => "species",
        _ => string.Empty,
    };

    /// <summary>Turns a script name such as <c>is_nomadic</c> into ordinary words.</summary>
    private static string Words(string name)
    {
        var trimmed = name.StartsWith("is_", StringComparison.Ordinal) ? name[3..] : name;
        return trimmed.Replace('_', ' ');
    }
}
