using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// The civics a plan may name, which is not the same question the designer asks.
/// </summary>
/// <remarks>
/// A plan is about the government an empire reforms into, so it has to answer two things the
/// designer never needs to. Whether a civic could be taken on at all after the game has started -
/// ninety-six cannot - and whether one the empire already has could be given up, since one that
/// cannot is spending a slot the plan may not use. Both are the game's own <c>modification</c>
/// field, and neither was read until there was a plan to read it for.
/// </remarks>
public sealed class PlanCivicsTests
{
    /// <summary>A civic no reform could add is not offered, even though the designer offers it.</summary>
    [Fact]
    public void ACivicNoReformCouldAddIsNotOffered()
    {
        var options = Rules.GetPlanCivicOptions(Context(), []);

        Assert.DoesNotContain(options, o => o.Key == "civic_unaddable");
        Assert.Contains(options, o => o.Key == "civic_ordinary");
    }

    /// <summary>
    /// One the empire already holds is offered whatever its rules say, because it is already there.
    /// </summary>
    /// <remarks>
    /// The half that is easy to get wrong. "Cannot be added later" is not "cannot be here" - an
    /// empire that started with it has it, and a plan that dropped it from the list would be unable
    /// to say the empire keeps it.
    /// </remarks>
    [Fact]
    public void OneTheEmpireAlreadyHasIsStillOffered()
    {
        Assert.Contains(Rules.GetPlanCivicOptions(Context(), []), o => o.Key == "civic_beacon_of_liberty");
    }

    /// <summary>
    /// A civic that cannot be given up is reported as locked, in the game's own words.
    /// </summary>
    /// <remarks>
    /// The wording matters as much as the fact. A row that will not respond and does not say why
    /// reads as broken, and the game already has the sentence for it - so the reason travels with
    /// the answer rather than being written again here.
    /// </remarks>
    [Fact]
    public void OneThatCannotBeGivenUpIsLockedAndSaysWhy()
    {
        var locked = Rules.GetLockedCivics(Context());

        var one = Assert.Single(locked);

        Assert.Equal("civic_beacon_of_liberty", one.Key);
        Assert.False(one.Enabled);
        Assert.Contains("CIVIC_NOT_MODDABLE", one.Reasons);
    }

    /// <summary>
    /// The locked ones spend slots the plan cannot use.
    /// </summary>
    /// <remarks>
    /// Three slots in a finished game - two to start with and one from a technology - and a civic
    /// that cannot be given up has already taken one of them.
    /// </remarks>
    [Fact]
    public void WhatIsLockedCountsAgainstTheBudget()
    {
        var locked = Rules.GetLockedCivics(Context());
        var budget = Rules.GetPlanCivicBudget(locked.Count + 1);

        Assert.Equal(2, budget.Spent);
        Assert.Equal(3, budget.Available);

        // And filling the rest blocks what is left, rather than letting a plan name four.
        var full = Rules.GetPlanCivicOptions(Context(), ["civic_ordinary", "civic_functional_architecture"]);

        Assert.False(full.Single(o => o.Key == "civic_needs_utopia").Enabled);
    }

    private static DesignContext Context() => Rules.CreateContext(RulesTestData.ValidEmpire());

    // Built on demand rather than held, so that this and the database below do not depend on which
    // of them the runtime initialises first.
    private static EmpireRules Rules => new(Database);

    /// <summary>
    /// The same little game, with the two halves of <c>modification</c> written on three civics.
    /// </summary>
    private static GameDatabase Database { get; } = RulesTestData.Database with
    {
        Civics =
        [
            // Held by the test empire, and impossible to give up.
            new CivicDefinition("civic_beacon_of_liberty", IsOrigin: false)
            {
                CanRemoveLater = new AlwaysRequirement(false) { FailureText = "CIVIC_NOT_MODDABLE" },
            },

            new CivicDefinition("civic_functional_architecture", IsOrigin: false),
            new CivicDefinition("civic_ordinary", IsOrigin: false),
            new CivicDefinition("civic_needs_utopia", IsOrigin: false),

            new CivicDefinition("civic_unaddable", IsOrigin: false)
            {
                CanAddLater = new AlwaysRequirement(false) { FailureText = "CIVIC_NOT_MODDABLE" },
            },
        ],
    };
}
