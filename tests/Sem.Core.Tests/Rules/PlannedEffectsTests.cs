using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// What an empire's modifiers come to once the plan it carries is counted as well.
/// </summary>
/// <remarks>
/// The panel that shows this had no way to ask the question: a perk and a tradition contributed
/// nothing to any total, because nothing summed them, and a context built from a design never
/// carried a plan at all. Both halves are checked here - that the sources arrive, and that the
/// civics swapped for are the ones the empire would actually end up holding.
/// </remarks>
public sealed class PlannedEffectsTests
{
    private const string Held = "civic_held";
    private const string Locked = "civic_locked";
    private const string Wanted = "civic_wanted";
    private const string Perk = "ap_perk";
    private const string Tree = "tradition_tree";

    private static EmpireRules Rules { get; } = new(Database());

    private static DesignContext Context()
    {
        var design = EmpireDesignsFile.CreateEmpty().Add("Test");

        design.Authority = "auth_democratic";
        design.SetCivics([Held, Locked]);

        return Rules.CreateContext(design);
    }

    private static double Total(DesignContext context, string modifier) =>
        DesignEffects.Combine(context).FirstOrDefault(m => m.Key == modifier)?.Total ?? 0;

    [Fact]
    public void WithoutAPlanOnlyTheEmpiresOwnChoicesCount()
    {
        var context = Context();

        Assert.Equal(new[] { Held, Locked }.Order(), context.Civics.Order());
        Assert.Equal(3, Total(context, "happiness"));
        Assert.Equal(0, Total(context, "research"));
    }

    /// <summary>A perk the plan names contributes what it grants.</summary>
    [Fact]
    public void APlannedPerkIsCounted()
    {
        var planned = Rules.WithPlanApplied(Context(), [Perk], [], []);

        Assert.Equal(10, Total(planned, "research"));
    }

    /// <summary>
    /// And a planned tree contributes every tradition inside it, not the tree itself.
    /// </summary>
    /// <remarks>
    /// A tree carries no effects of its own, so counting trees would have counted nothing at all.
    /// Seven entries in the game; three here, worth 1 + 2 + 4.
    /// </remarks>
    [Fact]
    public void APlannedTreeIsCountedThroughItsTraditions()
    {
        var planned = Rules.WithPlanApplied(Context(), [], [Tree], []);

        Assert.Equal(7, Total(planned, "unity"));
    }

    /// <summary>
    /// The planned civics replace the ones that can be reformed away, and only those.
    /// </summary>
    /// <remarks>
    /// The empire holds two: one the game will not let a reform remove, and one it will. Planning a
    /// third means ending with the locked one and the planned one - which is what the plan's own
    /// civic picker already works out, and is why this asks the rules rather than the plan.
    /// </remarks>
    [Fact]
    public void ThePlannedCivicsAreTheOnesTheEmpireEndsWith()
    {
        var planned = Rules.WithPlanApplied(Context(), [], [], [Wanted]);

        Assert.Contains(Locked, planned.Civics);
        Assert.Contains(Wanted, planned.Civics);
        Assert.DoesNotContain(Held, planned.Civics);
    }

    /// <summary>And the totals follow the swap: what was let go stops counting.</summary>
    [Fact]
    public void TheSwappedCivicStopsContributing()
    {
        var planned = Rules.WithPlanApplied(Context(), [], [], [Wanted]);

        // Held was worth 1 of the 3 and is gone; Wanted is worth 5.
        Assert.Equal(2 + 5, Total(planned, "happiness"));
    }

    /// <summary>A plan naming nothing leaves the empire exactly as it was.</summary>
    [Fact]
    public void AnEmptyPlanChangesNothing()
    {
        var planned = Rules.WithPlanApplied(Context(), [], [], []);

        Assert.Equal(Total(Context(), "happiness"), Total(planned, "happiness"));
        Assert.Equal(0, Total(planned, "research"));
    }

    private static EffectSet Granting(string modifier, double value) =>
        new() { Modifiers = new Dictionary<string, double>(StringComparer.Ordinal) { [modifier] = value } };

    private static GameDatabase Database() => new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Authorities = [new AuthorityDefinition("auth_democratic")],
        Civics =
        [
            new CivicDefinition(Held, IsOrigin: false) { Effects = Granting("happiness", 1) },

            // The one a reform cannot take away, so it survives into the planned empire.
            new CivicDefinition(Locked, IsOrigin: false)
            {
                Effects = Granting("happiness", 2),
                CanRemoveLater = new AlwaysRequirement(false),
            },

            new CivicDefinition(Wanted, IsOrigin: false) { Effects = Granting("happiness", 5) },
        ],
        AscensionPerks = [new AscensionPerkDefinition(Perk) { Effects = Granting("research", 10) }],
        TraditionTrees =
        [
            new TraditionTreeDefinition(Tree)
            {
                AdoptionBonus = "tr_adopt",
                Traditions = ["tr_one"],
                FinishBonus = "tr_finish",
            },
        ],
        Traditions =
        [
            new TraditionDefinition("tr_adopt") { Effects = Granting("unity", 1) },
            new TraditionDefinition("tr_one") { Effects = Granting("unity", 2) },
            new TraditionDefinition("tr_finish") { Effects = Granting("unity", 4) },
        ],
    };
}
