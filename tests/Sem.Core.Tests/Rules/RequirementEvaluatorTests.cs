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

    /// <summary>
    /// A refusal names what caused it.
    /// </summary>
    /// <remarks>
    /// The case this exists for is Reanimated Armies, whose <c>possible</c> holds two lists of
    /// civics it will not go with. The second carries a sentence of the game's own about Sovereign
    /// Guardianship; the first names Citizen Service and says nothing. So an empire holding both
    /// was told why it could not have the civic, an empire holding only Citizen Service was told
    /// nothing at all, and releasing the guardianship appeared to leave the option refused for no
    /// reason.
    /// </remarks>
    [Fact]
    public void ARefusalNamesWhatTheDesignHolds()
    {
        var context = Context();
        var held = new SelectionRequirement(SelectionCategory.Civics, "civic_beacon_of_liberty");

        var verdict = Evaluator.Evaluate(new NotRequirement(new AnyRequirement([held])), context);

        Assert.False(verdict.Passed);
        Assert.Equal(
            [RuleReasons.For(RuleReasons.Excluded, "civic_beacon_of_liberty")],
            verdict.Reasons);
    }

    /// <summary>
    /// And only where the game left it unexplained, since its own sentence is the better one.
    /// </summary>
    [Fact]
    public void UnlessTheGameSaidItBetter()
    {
        var context = Context();
        var held = new SelectionRequirement(SelectionCategory.Civics, "civic_beacon_of_liberty");

        var verdict = Evaluator.Evaluate(
            new NotRequirement(new AnyRequirement([held])) { FailureText = "civic_tooltip_not_a_beacon" },
            context);

        Assert.False(verdict.Passed);
        Assert.Equal(["civic_tooltip_not_a_beacon"], verdict.Reasons);
    }

    /// <summary>
    /// A refusal caused by something the design does not hold says nothing rather than inventing a
    /// cause. "You must not be a machine" fails a machine empire on what it is, and the reader is
    /// not holding a choice they could release.
    /// </summary>
    [Fact]
    public void AndNamesNothingWhenThereIsNothingToName()
    {
        var context = Context();
        var always = new AlwaysRequirement(true);

        var verdict = Evaluator.Evaluate(new NotRequirement(always), context);

        Assert.False(verdict.Passed);
        Assert.Empty(verdict.Reasons);
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
    /// <summary>
    /// An unread term inside a group does not get to decide the group, in either direction.
    /// </summary>
    /// <remarks>
    /// The case that made this necessary, and the reason the permissive reading is a third answer
    /// rather than a yes. A NOR compiles to Not(Any(...)), and the ascension trees rule each other
    /// out with one that lists every other ascension and a country flag - something no design can
    /// answer. Read as a yes, the Any passed, the Not failed, and the Purity and Mutation trees
    /// disappeared for every empire in the game.
    /// </remarks>
    [Fact]
    public void AnUnreadTermDoesNotDecideTheGroupAroundIt()
    {
        var context = Context();
        var unread = new UnknownRequirement("has_country_flag");
        var absent = new SelectionRequirement(SelectionCategory.AscensionPerk, "ap_not_planned");

        // NOR over things that are not so plus one nobody knows: nothing has ruled this out.
        var exclusion = new NotRequirement(new AnyRequirement([absent, unread]));

        Assert.True(Evaluator.IsSatisfied(exclusion, context));
    }

    /// <summary>
    /// And the exclusion still fires the moment something in it is definitely so.
    /// </summary>
    /// <remarks>
    /// The half that a blanket "treat unknowns as true" would break, and the half that makes the
    /// rule worth having: planning one ascension has to take the others off the list. A definite
    /// yes settles the group whatever else in it is unread.
    /// </remarks>
    [Fact]
    public void ButADefiniteTermStillDoes()
    {
        var context = Context();
        var unread = new UnknownRequirement("has_country_flag");

        // Something this empire definitely is, standing in for the ascension a plan has named.
        var held = new SelectionRequirement(SelectionCategory.Ethics, "ethic_xenophile");

        var exclusion = new NotRequirement(new AnyRequirement([held, unread]));

        Assert.True(Evaluator.IsSatisfied(held, context));
        Assert.False(Evaluator.IsSatisfied(exclusion, context));
    }

    /// <summary>A group that must hold entirely is refused by a term that definitely fails.</summary>
    /// <remarks>
    /// The other direction of the same rule. Not knowing is not permission for everything around it:
    /// a condition with one readable failure in it is still a failure, and still says why.
    /// </remarks>
    [Fact]
    public void ATermThatDefinitelyFailsStillRefuses()
    {
        var context = Context();
        var unread = new UnknownRequirement("has_technology");
        var impossible = new AlwaysRequirement(false) { FailureText = "requires_something" };

        var verdict = Evaluator.Evaluate(new AllRequirement([unread, impossible]), context);

        Assert.False(verdict.Passed);
        Assert.Contains("requires_something", verdict.Reasons);
    }
}
