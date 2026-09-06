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
        var options = Rules().GetAscensionPerkOptions(Context(), [], []);

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
        var options = Rules().GetAscensionPerkOptions(Context(), ["ap_flesh"], []);

        Assert.False(Find(options, "ap_gene").Enabled);
        Assert.True(Find(options, "ap_flesh").Enabled);
        Assert.True(Find(options, "ap_plain").Enabled);
    }

    /// <summary>And letting it go opens the other again.</summary>
    [Fact]
    public void ReleasingThePerkOpensTheOtherAgain()
    {
        var options = Rules().GetAscensionPerkOptions(Context(), [], []);

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
        var options = rules.GetAscensionPerkOptions(Context(), ["ap_flesh", "ap_plain"], []);

        var blocked = Find(options, "ap_third");

        Assert.False(blocked.Enabled);
        Assert.Contains(RuleReasons.NoPerkSlotsLeft, blocked.Reasons);
        Assert.All(blocked.Reasons, r => Assert.True(RuleReasons.IsBudget(r)));

        Assert.True(Find(options, "ap_flesh").Enabled);
    }

    [Fact]
    public void TheBudgetCountsWhatIsPlannedAgainstWhatAGameGrants()
    {
        var budget = Rules().GetAscensionPerkBudget(3, 2);

        Assert.Equal(3, budget.Spent);
        Assert.Equal(2, budget.Available);
    }

    /// <summary>
    /// What a plan may take follows from what it opens, because that is how a game grants it.
    /// </summary>
    /// <remarks>
    /// A perk slot for every tradition tree finished - the modifier is on each
    /// <c>tr_*_finish</c> - and one more from a technology, which is where the eighth comes from
    /// when there are only seven trees. So a plan that opens nothing may still take one, and one
    /// that opens everything may take them all.
    /// </remarks>
    [Fact]
    public void TheBudgetFollowsTheTreesThePlanOpens()
    {
        var rules = new EmpireRules(RulesTestData.Database with
        {
            Defines = RulesTestData.Database.Defines with
            {
                AscensionPerkSlots = 8,
                AscensionPerkSlotsWithoutTraditions = 1,
            },
        });

        Assert.Equal(1, rules.GetAscensionPerkBudget(0, 0).Available);
        Assert.Equal(4, rules.GetAscensionPerkBudget(0, 3).Available);
        Assert.Equal(8, rules.GetAscensionPerkBudget(0, 7).Available);

        // And never past what a game has slots for, however many trees a patch adds.
        Assert.Equal(8, rules.GetAscensionPerkBudget(0, 20).Available);
    }

    /// <summary>
    /// A perk needing two others before it is not offered until two others are there.
    /// </summary>
    /// <remarks>
    /// The game writes this as <c>num_ascension_perks > 1</c> and means "you must already have
    /// two", which against an ordered plan is a rule about position: this one cannot be first or
    /// second. Twenty-five perks carry a condition of that shape, and none of them was read at all
    /// until there was something to count.
    /// </remarks>
    [Fact]
    public void APerkThatNeedsTwoBeforeItWaitsForTwo()
    {
        var rules = Ordered.Rules();

        Assert.False(Find(rules.GetAscensionPerkOptions(Ordered.Context(), [], []), "ap_late").Enabled);
        Assert.False(Find(rules.GetAscensionPerkOptions(Ordered.Context(), ["ap_one"], []), "ap_late").Enabled);
        Assert.True(Find(rules.GetAscensionPerkOptions(Ordered.Context(), ["ap_one", "ap_two"], []), "ap_late").Enabled);
    }

    /// <summary>An order the game would grant is legal; the same perks in another order are not.</summary>
    /// <remarks>
    /// The two halves together are the point. Reordering must not be able to produce a plan the game
    /// would refuse, and it must not refuse one the game would allow - a check that answered no to
    /// everything would pass the first half on its own.
    /// </remarks>
    [Fact]
    public void APerkCannotBeMovedAboveWhatItNeeds()
    {
        var rules = Ordered.Rules();

        Assert.True(rules.IsLegalPerkOrder(Ordered.Context(), ["ap_one", "ap_two", "ap_late"], []));
        Assert.False(rules.IsLegalPerkOrder(Ordered.Context(), ["ap_late", "ap_one", "ap_two"], []));
        Assert.False(rules.IsLegalPerkOrder(Ordered.Context(), ["ap_one", "ap_late", "ap_two"], []));
    }

    /// <summary>
    /// A perk asking for a tradition tree still to be free is blocked when the plan opens them all.
    /// </summary>
    /// <remarks>
    /// The one condition that crosses between the two halves of a plan: the seven ascension perks
    /// ask <c>num_tradition_categories &lt; @max_tradition_trees</c>, so a plan that has already
    /// spoken for every tree has nowhere to put the one the perk would open.
    /// </remarks>
    [Fact]
    public void APerkNeedingATreeSlotIsBlockedWhenThePlanOpensThemAll()
    {
        var rules = Ordered.Rules();

        // Judged where it would sit. The trees open one at a time between the perks, so the perk in
        // the last place has seen them all and the one in the first place has seen one.
        Assert.True(rules.IsLegalPerkOrder(Ordered.Context(), ["ap_path", "ap_one", "ap_two"], ["a", "b", "c"]));
        Assert.False(rules.IsLegalPerkOrder(Ordered.Context(), ["ap_one", "ap_two", "ap_path"], ["a", "b", "c"]));
    }

    /// <summary>
    /// The trees are counted, and the traditions carried alongside them are not.
    /// </summary>
    /// <remarks>
    /// The set a plan's trees live in also holds the tradition that opens each and the one that
    /// finishes it, because the game asks after those by name - <c>has_tradition =
    /// tr_nanotech_adopt</c> is how three of the trees rule each other out. Counting that set says
    /// a plan has opened three times the trees it has, and every "a tree slot must still be free"
    /// runs out two trees early.
    /// </remarks>
    [Fact]
    public void OnlyTheTreesThemselvesAreCounted()
    {
        var rules = Ordered.Rules();

        // Two trees, each carrying an adoption and a completion tradition: six names, two trees.
        Assert.True(Find(rules.GetAscensionPerkOptions(Ordered.Context(), ["ap_one"], ["a", "b"]), "ap_path").Enabled);
    }

    private static OptionState Find(IReadOnlyList<OptionState> options, string key) =>
        options.Single(o => o.Key == key);

    private static EmpireRules Rules() => new(Database);

    private static DesignContext Context() =>
        new EmpireRules(Database).CreateContext(RulesTestData.ValidEmpire());

    /// <summary>Two perks that rule each other out, one that minds nobody, and two slots.</summary>
    /// <remarks>
    /// Both slots granted without opening a tradition tree, so these tests are about which perks
    /// sit together rather than about how many a plan has earned - which is the next fixture's
    /// subject.
    /// </remarks>
    private static GameDatabase Database { get; } = RulesTestData.Database with
    {
        Defines = RulesTestData.Database.Defines with
        {
            AscensionPerkSlots = 2,
            AscensionPerkSlotsWithoutTraditions = 2,
        },
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

    /// <summary>
    /// A second little game, for the rules that are about where a perk sits rather than which ones
    /// sit together.
    /// </summary>
    /// <remarks>
    /// Its own because the fixture above deliberately has two slots, and a perk that needs two
    /// others before it could never be reached in a game that only grants two. Three tradition trees
    /// here, so "a tree slot must still be free" is a condition that can be met and then not met.
    /// </remarks>
    private static class Ordered
    {
        public static EmpireRules Rules() => new(Database);

        public static DesignContext Context() =>
            new EmpireRules(Database).CreateContext(RulesTestData.ValidEmpire());

        public static GameDatabase Database { get; } = RulesTestData.Database with
        {
            Defines = RulesTestData.Database.Defines with
            {
                AscensionPerkSlots = 8,
                TraditionSlots = 3,

                // Enough that the tests below are about the conditions rather than about running
                // out of room: three trees would otherwise allow only four perks.
                AscensionPerkSlotsWithoutTraditions = 8,
            },

            // Each carrying the tradition that opens it and the one that finishes it, because that
            // is what the real ones carry and what the counting has to see past.
            TraditionTrees =
            [
                new TraditionTreeDefinition("a") { AdoptionBonus = "a_adopt", FinishBonus = "a_finish" },
                new TraditionTreeDefinition("b") { AdoptionBonus = "b_adopt", FinishBonus = "b_finish" },
                new TraditionTreeDefinition("c") { AdoptionBonus = "c_adopt", FinishBonus = "c_finish" },
            ],
            AscensionPerks =
            [
                new AscensionPerkDefinition("ap_one"),
                new AscensionPerkDefinition("ap_two"),

                // "num_ascension_perks > 1", which twelve of the game's own perks carry.
                new AscensionPerkDefinition("ap_late")
                {
                    Possible = new CountRequirement(
                        SelectionCategory.AscensionPerk, CountComparison.Above, 1),
                },

                // "num_tradition_categories < @max_tradition_trees", which the seven ascension
                // perks carry and which is the only condition crossing between a plan's two halves.
                new AscensionPerkDefinition("ap_path")
                {
                    Possible = new CountRequirement(
                        SelectionCategory.TraditionTree, CountComparison.Below, 3),
                },
            ],
        };
    }
}
