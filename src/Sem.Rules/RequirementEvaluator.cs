using Sem.GameData;

namespace Sem.Rules;

/// <summary>The result of testing a condition against a design.</summary>
/// <param name="Passed">Whether the condition holds.</param>
/// <param name="Reasons">
/// Localisation keys explaining the failure, taken from the game's own script so a blocked option
/// is explained in the same words the game would use. Empty when the condition holds, and possibly
/// empty when it fails but the script offered no explanation.
/// </param>
public readonly record struct Verdict(bool Passed, IReadOnlyList<string> Reasons)
{
    /// <summary>A condition that holds.</summary>
    public static Verdict Pass { get; } = new(true, []);

    /// <summary>A condition that fails with no explanation attached.</summary>
    public static Verdict Fail { get; } = new(false, []);
}

/// <summary>
/// Tests compiled conditions against a design.
/// </summary>
/// <remarks>
/// When a failing condition carries an explanation, that explanation replaces whatever its children
/// would have said. This mirrors the game, which shows one clear line rather than the whole chain
/// of reasoning behind it.
/// </remarks>
public sealed class RequirementEvaluator
{
    /// <summary>Tests a condition and collects the reasons it failed.</summary>
    public Verdict Evaluate(Requirement requirement, DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(context);

        var verdict = EvaluateCore(requirement, context);

        if (verdict.Passed)
        {
            return Verdict.Pass;
        }

        // The nearest explanation wins, so the player reads "Requires the Gestalt Consciousness
        // Ethic" rather than a list of every clause underneath it.
        return requirement.FailureText is { Length: > 0 } text
            ? new Verdict(false, [text])
            : verdict;
    }

    /// <summary>Whether a condition holds, without gathering explanations.</summary>
    public bool IsSatisfied(Requirement requirement, DesignContext context) =>
        Evaluate(requirement, context).Passed;

    private Verdict EvaluateCore(Requirement requirement, DesignContext context) => requirement switch
    {
        AlwaysRequirement always => always.Value ? Verdict.Pass : Verdict.Fail,

        AllRequirement all => EvaluateAll(all, context),

        AnyRequirement any => EvaluateAny(any, context),

        // A negation that fails has nothing useful to report from inside it: the child succeeded,
        // and its reasons describe a failure that did not happen.
        NotRequirement not => Evaluate(not.Item, context).Passed ? Verdict.Fail : Verdict.Pass,

        SelectionRequirement selection => context.Has(selection.Category, selection.Key)
            ? Verdict.Pass
            : Verdict.Fail,

        DlcRequirement dlc => context.OwnedDlc.Contains(dlc.Name) ? Verdict.Pass : Verdict.Fail,

        FieldRequirement field => string.Equals(context.Field(field.Field), field.Value, StringComparison.Ordinal)
            ? Verdict.Pass
            : Verdict.Fail,

        PredicateRequirement predicate => context.Evaluate(predicate.Name) ? Verdict.Pass : Verdict.Fail,

        // Conditions the extractor did not understand permit the option, so a game patch never
        // hides something the player should be able to choose.
        UnknownRequirement unknown => unknown.Assume ? Verdict.Pass : Verdict.Fail,

        _ => Verdict.Pass,
    };

    private Verdict EvaluateAll(AllRequirement all, DesignContext context)
    {
        List<string>? reasons = null;

        foreach (var item in all.Items)
        {
            var verdict = Evaluate(item, context);
            if (verdict.Passed)
            {
                continue;
            }

            reasons ??= [];
            reasons.AddRange(verdict.Reasons);
        }

        return reasons is null ? Verdict.Pass : new Verdict(false, reasons);
    }

    private Verdict EvaluateAny(AnyRequirement any, DesignContext context)
    {
        if (any.Items.Count == 0)
        {
            return Verdict.Pass;
        }

        var reasons = new List<string>();

        foreach (var item in any.Items)
        {
            var verdict = Evaluate(item, context);
            if (verdict.Passed)
            {
                return Verdict.Pass;
            }

            reasons.AddRange(verdict.Reasons);
        }

        return new Verdict(false, reasons);
    }
}
