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

        requirement = requirement.Simplified();

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
            // Lower-cased, because this one is written into the middle of a sentence where the
            // outline draws it as a statement of its own. Same answer either way - see Unreachable.
            AlwaysRequirement always => always.Value == !negated
                ? null
                : Unreachable.Words(always.Because).ToLowerInvariant(),

            NotRequirement not => Write(not.Item, depth, !negated),

            AllRequirement all => Join(all.Items, depth, negated, negated ? " or " : " and "),

            AnyRequirement any => Join(any.Items, depth, negated, negated ? " and " : " or "),

            SelectionRequirement selection => Phrase(
                negated ? "not" : null,
                Category(selection.Category),
                _localizer.Text(selection.Key)),

            DlcRequirement dlc => negated ? $"without {dlc.Name}" : $"with {dlc.Name}",

            // The value read rather than spelled out, because half of these name something the game
            // has written words for: a personality that likes a science directorate says so as a
            // field, and the phrase came out "government gov science directorate". The ones with no
            // entry - an election type of none, of democratic - are already ordinary words.
            FieldRequirement field => Phrase(
                negated ? "not" : null,
                Words(field.Field),
                _localizer.Text(field.Value, Words(field.Value))),

            PredicateRequirement predicate => negated
                ? $"not {Words(predicate.Name)}"
                : Words(predicate.Name),

            // Something only a game in progress could answer. Saying so is more use than saying
            // nothing, because it tells the player this is not a bonus they start with. Negated the
            // same way every other arm is: this one alone dropped the "not", so a leader trait that
            // pays out while off the council was headed "When councilor" - the opposite of the truth,
            // printed directly under the game's own sentence saying so.
            // Carrying whatever it named, where the game has a name for it. "has technology" on its
            // own was every one of these, and the perks that ask for mega-engineering and for
            // psionic theory looked like the same condition. A country flag the game never names
            // stays as it was, because a prettified script key is not an answer.
            // A phrase the extractor wrote says the whole thing on its own - see UnknownRequirement.
            UnknownRequirement { Said: { Length: > 0 } written } => negated
                ? $"not {written}"
                : written,

            UnknownRequirement unknown => Phrase(
                negated ? "not" : null,
                Words(unknown.Name),
                unknown.Value is { Length: > 0 } named
                    ? _localizer.Text(named, string.Empty)
                    : string.Empty),

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

        builder.Append(subject);

        // A subject with nothing said about it is still a phrase - "has technology" on its own is
        // what a condition that named no technology comes out as - and it must not come out with a
        // space hanging off the end of it.
        if (value is { Length: > 0 })
        {
            builder.Append(' ').Append(value);
        }

        return builder.ToString();
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
