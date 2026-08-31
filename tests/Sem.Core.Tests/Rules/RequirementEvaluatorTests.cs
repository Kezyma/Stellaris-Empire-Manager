using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// How a compiled condition is read against a design.
/// </summary>
/// <remarks>
/// The policy the evaluator exists to implement has two halves that pull opposite ways. Whether an
/// option may be picked fails open, so script this project does not understand never hides a choice
/// the player is entitled to. Whether a bonus applies fails closed, because a number folded into a
/// total on the strength of an unread condition is one the empire has been promised without
/// evidence. Both halves are pinned here.
/// </remarks>
public sealed class RequirementEvaluatorTests
{
    private static readonly RequirementEvaluator Evaluator = new();

    private static DesignContext Context() =>
        DesignContext.FromDesign(RulesTestData.ValidEmpire(), RulesTestData.Database);

    /// <summary>
    /// The permissive assumption has to survive being negated, or it only holds in half the
    /// positions a condition can appear in — and fails in the one case the mechanism exists for, a
    /// patch introducing script the extractor has not met. <c>unknown = no</c> compiles to
    /// Not(Unknown), which refused the option outright.
    /// </summary>
    [Fact]
    public void AnUnknownConditionPermitsTheOption()
    {
        Assert.True(Evaluator.IsSatisfied(new UnknownRequirement("some_future_trigger"), Context()));
    }

    [Fact]
    public void AndStillPermitsItWhenNegated()
    {
        var negated = new NotRequirement(new UnknownRequirement("some_future_trigger"));

        Assert.True(Evaluator.IsSatisfied(negated, Context()));
    }

    /// <summary>
    /// Negating a condition nobody could read does not make it readable, so the other half of the
    /// policy has to hold too: the bonus behind it is still left out of the totals.
    /// </summary>
    [Fact]
    public void ButNeitherFormCanBeDecided()
    {
        var context = Context();
        var unknown = new UnknownRequirement("some_future_trigger");

        Assert.False(Evaluator.CanDecide(unknown, context));
        Assert.False(Evaluator.CanDecide(new NotRequirement(unknown), context));
    }

    /// <summary>A condition the design can answer is still negated the ordinary way.</summary>
    [Fact]
    public void AKnownConditionIsStillNegatedNormally()
    {
        var context = Context();
        var gestalt = new SelectionRequirement(SelectionCategory.Ethics, "ethic_gestalt_consciousness");

        Assert.False(Evaluator.IsSatisfied(gestalt, context));
        Assert.True(Evaluator.IsSatisfied(new NotRequirement(gestalt), context));
        Assert.True(Evaluator.CanDecide(new NotRequirement(gestalt), context));
    }
}
