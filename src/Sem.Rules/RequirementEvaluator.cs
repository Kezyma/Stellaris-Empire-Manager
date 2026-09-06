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

    /// <summary>A condition nothing here can settle, which is allowed to hold.</summary>
    public static Verdict Maybe { get; } = new(true, []) { Unsure = true };

    /// <summary>
    /// Whether the answer is a guess rather than a reading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A third answer, carried alongside the first two rather than replacing them, so that
    /// everything asking merely whether a condition holds keeps getting the permissive answer it
    /// always got. What needs the distinction is the combining: an unsettled term must not decide a
    /// group it sits in, in either direction.
    /// </para>
    /// <para>
    /// False by default on purpose, so a <c>default(Verdict)</c> is still a plain failure.
    /// </para>
    /// </remarks>
    public bool Unsure { get; init; }
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
            return verdict.Unsure ? Verdict.Maybe : Verdict.Pass;
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

    /// <summary>
    /// Whether a condition can be settled from the design alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluating is not the same as deciding. Every method above answers yes to a condition it
    /// cannot read, so that a game patch never hides an option the player should be able to choose.
    /// That is the correct bias for gating and the wrong one for arithmetic: a modifier folded into
    /// a total on the strength of an unread condition is a bonus the empire has been promised
    /// without evidence.
    /// </para>
    /// <para>
    /// So this asks the other question. A condition made only of content packs, of choices the
    /// design has made, and of predicates about the design is answerable now; one that asks whether
    /// a tradition has been adopted or a planet colonised is not, and belongs to a game already
    /// under way.
    /// </para>
    /// </remarks>
    public bool CanDecide(Requirement requirement, DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(context);

        return requirement switch
        {
            AlwaysRequirement => true,
            AllRequirement all => all.Items.All(item => CanDecide(item, context)),
            AnyRequirement any => any.Items.All(item => CanDecide(item, context)),
            NotRequirement not => CanDecide(not.Item, context),
            SelectionRequirement => true,
            DlcRequirement => true,
            FieldRequirement field => context.Field(field.Field) is not null,
            PredicateRequirement predicate => DesignContext.Knows(predicate.Name),

            // A count is read off the plan's own lists, which are as knowable as the selections
            // beside them - and unlike them, the answer is the whole point rather than a gate.
            CountRequirement => true,
            _ => false,
        };
    }

    private Verdict EvaluateCore(Requirement requirement, DesignContext context) => requirement switch
    {
        AlwaysRequirement always => always.Value ? Verdict.Pass : Verdict.Fail,

        AllRequirement all => EvaluateAll(all, context),

        AnyRequirement any => EvaluateAny(any, context),

        // Negating a condition nobody could read does not make it readable, so not knowing survives
        // the negation rather than being turned into a refusal. This used to be a special case
        // matching NOT wrapped directly around an unknown, which is where it usually sits and not
        // where it does the damage: a NOR compiles to Not(Any(...)), and one unread term among
        // seven read ones then decided the whole group.
        //
        // A negation that fails has nothing useful to report from inside it either: the child
        // succeeded, and its reasons describe a failure that did not happen.
        NotRequirement not => Negate(Evaluate(not.Item, context)),

        SelectionRequirement selection => context.Has(selection.Category, selection.Key)
            ? Verdict.Pass
            : Verdict.Fail,

        DlcRequirement dlc => context.OwnedDlc.Contains(dlc.Name) ? Verdict.Pass : Verdict.Fail,

        FieldRequirement field => string.Equals(context.Field(field.Field), field.Value, StringComparison.Ordinal)
            ? Verdict.Pass
            : Verdict.Fail,

        PredicateRequirement predicate => context.Evaluate(predicate.Name) ? Verdict.Pass : Verdict.Fail,

        // Conditions the extractor did not understand, and conditions about a game that has not
        // been played yet, permit the option - so a patch never hides something the player should
        // be able to choose, and a plan is never refused for not having happened.
        UnknownRequirement unknown => unknown.Assume ? Verdict.Maybe : Verdict.Fail,

        // "You must already have three of these" read against the things that come before this one
        // in the plan, which is what makes it a rule about position rather than one a plan could
        // never satisfy.
        CountRequirement count => Compare(context.Count(count.Of), count) ? Verdict.Pass : Verdict.Fail,

        _ => Verdict.Pass,
    };

    private static bool Compare(int have, CountRequirement against) => against.Comparison switch
    {
        CountComparison.Above => have > against.Value,
        CountComparison.Below => have < against.Value,
        CountComparison.AtLeast => have >= against.Value,
        CountComparison.AtMost => have <= against.Value,
        CountComparison.Exactly => have == against.Value,
        _ => true,
    };

    /// <summary>
    /// Turns a verdict around, leaving one nothing could settle unsettled.
    /// </summary>
    /// <remarks>
    /// The whole point of the third answer. "You must not have done X" where nobody knows whether X
    /// was done is still nobody knowing, and reading it as either yes or no invents a fact.
    /// </remarks>
    private static Verdict Negate(Verdict verdict) => verdict switch
    {
        { Unsure: true } => Verdict.Maybe,
        { Passed: true } => Verdict.Fail,
        _ => Verdict.Pass,
    };

    /// <summary>
    /// Every child must hold: one that definitely does not settles it, and otherwise anything
    /// unsettled leaves the group unsettled.
    /// </summary>
    private Verdict EvaluateAll(AllRequirement all, DesignContext context)
    {
        List<string>? reasons = null;
        var unsure = false;

        foreach (var item in all.Items)
        {
            var verdict = Evaluate(item, context);

            if (verdict.Unsure)
            {
                unsure = true;
                continue;
            }

            if (verdict.Passed)
            {
                continue;
            }

            reasons ??= [];
            reasons.AddRange(verdict.Reasons);
        }

        if (reasons is not null)
        {
            return new Verdict(false, reasons);
        }

        return unsure ? Verdict.Maybe : Verdict.Pass;
    }

    /// <summary>
    /// At least one child must hold.
    /// </summary>
    /// <remarks>
    /// A child that definitely holds settles this whatever else is unread, which is what keeps the
    /// exclusions working. The ascension trees rule each other out with a NOR listing every other
    /// ascension and one country flag: planning a cybernetic ascension makes one of those a
    /// definite yes, the group a definite yes, and the negation around it a definite no - while an
    /// empire that has planned none of them leaves the group merely unsettled, and the tree stays
    /// on offer.
    /// </remarks>
    private Verdict EvaluateAny(AnyRequirement any, DesignContext context)
    {
        if (any.Items.Count == 0)
        {
            return Verdict.Pass;
        }

        var reasons = new List<string>();
        var unsure = false;

        foreach (var item in any.Items)
        {
            var verdict = Evaluate(item, context);

            if (verdict.Unsure)
            {
                unsure = true;
                continue;
            }

            if (verdict.Passed)
            {
                return Verdict.Pass;
            }

            reasons.AddRange(verdict.Reasons);
        }

        return unsure ? Verdict.Maybe : new Verdict(false, reasons);
    }
}
