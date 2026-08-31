using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// Adding up what an empire's choices come to.
/// </summary>
/// <remarks>
/// The interesting part is the conditional modifiers. Every condition in the game was treated as
/// unanswerable and left out, with a footnote saying so — but half of them ask about the design
/// itself. Militarist's claim influence cost is conditional on the empire not being nomadic, and
/// whether it is nomadic is a field of the design sitting right there, so an empire that plainly had
/// the bonus was shown a total without it and an apology underneath.
/// </remarks>
public sealed class DesignEffectsTests
{
    /// <summary>Conditions the design can answer, and one it cannot.</summary>
    private static GameDatabase Database { get; } = RulesTestData.Database with
    {
        Ethics =
        [
            .. RulesTestData.Database.Ethics.Select(e => e.Key switch
            {
                "ethic_fanatic_militarist" => e with
                {
                    Effects = new EffectSet
                    {
                        Modifiers = new Dictionary<string, double> { ["ship_fire_rate_mult"] = 0.1 },
                        Conditional =
                        [
                            new ConditionalEffects(
                                new PredicateRequirement("is_nomadic").Negated(),
                                new Dictionary<string, double> { ["country_claim_influence_cost_mult"] = -0.1 }),

                            new ConditionalEffects(
                                new PredicateRequirement("is_nomadic"),
                                new Dictionary<string, double> { ["scavenge_debris_mult"] = 0.05 }),

                            new ConditionalEffects(
                                new UnknownRequirement("has_tradition"),
                                new Dictionary<string, double> { ["country_unity_produces_mult"] = 0.15 }),
                        ],
                    },
                },
                _ => e,
            }),
        ],
    };

    private static DesignContext Context(bool nomadic)
    {
        var design = RulesTestData.ValidEmpire();
        design.IsNomadic = nomadic;

        return new EmpireRules(Database).CreateContext(design, new HashSet<string> { "Utopia" });
    }

    private static double? Total(DesignContext context, string modifier) =>
        DesignEffects.Combine(context).FirstOrDefault(m => m.Key == modifier)?.Total;

    [Fact]
    public void AConditionTheDesignAnswersIsCountedWhenItHolds()
    {
        var context = Context(nomadic: false);

        Assert.Equal(-0.1, Total(context, "country_claim_influence_cost_mult"));
        Assert.Null(Total(context, "scavenge_debris_mult"));
    }

    [Fact]
    public void TheOtherBranchIsCountedForTheOtherKindOfEmpire()
    {
        var context = Context(nomadic: true);

        Assert.Equal(0.05, Total(context, "scavenge_debris_mult"));
        Assert.Null(Total(context, "country_claim_influence_cost_mult"));
    }

    [Fact]
    public void AConditionAboutAGameInProgressIsLeftOut()
    {
        // "Once a tradition is adopted" cannot be answered from a design, and counting it would
        // promise the empire a bonus it does not start with.
        Assert.Null(Total(Context(nomadic: false), "country_unity_produces_mult"));
    }

    [Fact]
    public void UnconditionalModifiersAreUnaffected()
    {
        Assert.Equal(0.1, Total(Context(nomadic: false), "ship_fire_rate_mult"));
    }

    [Fact]
    public void TheFootnoteIsAboutWhatWasActuallyLeftOut()
    {
        // Both empires carry conditions; both have one that could not be settled. Take that one away
        // and the note must go, even though two settled conditions remain.
        Assert.True(DesignEffects.AnyConditional(Context(nomadic: false)));

        var settled = Database with
        {
            Ethics =
            [
                .. Database.Ethics.Select(e => e.Key == "ethic_fanatic_militarist"
                    ? e with
                    {
                        Effects = e.Effects with
                        {
                            Conditional = [.. e.Effects.Conditional.Where(c => c.When is not UnknownRequirement)],
                        },
                    }
                    : e),
            ],
        };

        var design = RulesTestData.ValidEmpire();
        design.IsNomadic = false;

        var context = new EmpireRules(settled).CreateContext(design, new HashSet<string> { "Utopia" });

        Assert.False(DesignEffects.AnyConditional(context));
        Assert.Equal(-0.1, Total(context, "country_claim_influence_cost_mult"));
    }

    [Fact]
    public void TheFoundersTraitsAreCountedAlongsideTheEmpiresOwnChoices()
    {
        // The founder species is the empire at the point it is being designed, so its traits belong
        // in the same total as its ethics and civics. Stated as a test because it is a decision, not
        // an accident of which lists Selected happens to walk.
        var design = RulesTestData.ValidEmpire();
        var context = new EmpireRules(Database).CreateContext(design, new HashSet<string> { "Utopia" });

        var sources = DesignEffects.Selected(context).Select(o => o.Key).ToList();

        Assert.Contains("trait_intelligent", sources);
        Assert.Contains("ethic_fanatic_militarist", sources);
    }
}
