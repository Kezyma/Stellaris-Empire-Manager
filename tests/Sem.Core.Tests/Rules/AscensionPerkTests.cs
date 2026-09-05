using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// Which ascension perks a plan may name, and which the game would refuse alongside them.
/// </summary>
/// <remarks>
/// The interesting thing about a perk is that almost every rule keeping two of them apart is written
/// as "not if that other one is already taken" - a question about state a design does not have. The
/// plan is that state, so these rules become answerable the moment the plan is handed in, and this
/// is what checks that the handing-in works.
/// </remarks>
public sealed class AscensionPerkTests
{
    [Fact]
    public void WithNothingPlannedEverythingIsOffered()
    {
        var options = Rules().GetAscensionPerkOptions(Context(), []);

        Assert.All(options, o => Assert.True(o.Enabled, $"{o.Key} was blocked with nothing planned"));
    }

    /// <summary>
    /// Taking one perk closes the one that rules it out, which is the whole point.
    /// </summary>
    /// <remarks>
    /// Written the way the game writes it: a NOT around "has this other perk". Nothing could answer
    /// that before, so the pair used to be offered together and the game would have refused the
    /// design.
    /// </remarks>
    [Fact]
    public void APerkThatRulesOutAnotherBlocksIt()
    {
        var options = Rules().GetAscensionPerkOptions(Context(), ["ap_flesh"]);

        Assert.False(Find(options, "ap_gene").Enabled);
        Assert.True(Find(options, "ap_flesh").Enabled);
        Assert.True(Find(options, "ap_plain").Enabled);
    }

    /// <summary>And letting it go opens the other again.</summary>
    [Fact]
    public void ReleasingThePerkOpensTheOtherAgain()
    {
        var options = Rules().GetAscensionPerkOptions(Context(), []);

        Assert.True(Find(options, "ap_gene").Enabled);
    }

    /// <summary>
    /// A full plan blocks everything it does not already name, and says why.
    /// </summary>
    /// <remarks>
    /// The reason is marked as a budget one, so the picker dims those differently from the ones
    /// ruled out for good: being full is undone by letting something go, and being wrong is not.
    /// </remarks>
    [Fact]
    public void AFullPlanBlocksWhatItDoesNotHold()
    {
        var rules = Rules();
        var options = rules.GetAscensionPerkOptions(Context(), ["ap_flesh", "ap_plain"]);

        var blocked = Find(options, "ap_third");

        Assert.False(blocked.Enabled);
        Assert.Contains(RuleReasons.NoPerkSlotsLeft, blocked.Reasons);
        Assert.All(blocked.Reasons, r => Assert.True(RuleReasons.IsBudget(r)));

        Assert.True(Find(options, "ap_flesh").Enabled);
    }

    [Fact]
    public void TheBudgetCountsWhatIsPlannedAgainstWhatAGameGrants()
    {
        var budget = Rules().GetAscensionPerkBudget(3);

        Assert.Equal(3, budget.Spent);
        Assert.Equal(2, budget.Available);
    }

    private static OptionState Find(IReadOnlyList<OptionState> options, string key) =>
        options.Single(o => o.Key == key);

    private static EmpireRules Rules() => new(Database);

    private static DesignContext Context() =>
        new EmpireRules(Database).CreateContext(RulesTestData.ValidEmpire());

    /// <summary>Two perks that rule each other out, one that minds nobody, and two slots.</summary>
    private static GameDatabase Database { get; } = RulesTestData.Database with
    {
        Defines = RulesTestData.Database.Defines with { AscensionPerkSlots = 2 },
        AscensionPerks =
        [
            new AscensionPerkDefinition("ap_flesh"),
            new AscensionPerkDefinition("ap_gene")
            {
                Possible = new NotRequirement(
                    new SelectionRequirement(SelectionCategory.AscensionPerk, "ap_flesh")),
            },
            new AscensionPerkDefinition("ap_plain"),
            new AscensionPerkDefinition("ap_third"),
        ],
    };
}
