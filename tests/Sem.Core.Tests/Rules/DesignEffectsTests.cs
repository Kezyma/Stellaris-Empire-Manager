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

                            // The shape the game writes four thousand times: a scope guard, and then
                            // the condition it protects. The guard compiles to an unknown, so the
                            // pair is undecidable and the modifier stays out - which is the same
                            // answer it got when the guard compiled to a flat refusal, and the whole
                            // reason changing the guard was safe to do.
                            new ConditionalEffects(
                                new AllRequirement(
                                [
                                    new UnknownRequirement("exists") { Value = "owner" },
                                    new PredicateRequirement("is_nomadic").Negated(),
                                ]),
                                new Dictionary<string, double> { ["pop_growth_speed"] = 0.25 }),
                        ],
                    },
                },
                _ => e,
            }),
        ],

        Traits =
        [
            .. RulesTestData.Database.Traits.Select(t => t.Key switch
            {
                // A ruler trait that does something, so counting it is visible in a total.
                "leader_trait_principled" => t with
                {
                    Effects = new EffectSet
                    {
                        Modifiers = new Dictionary<string, double> { ["country_unity_produces_mult"] = 0.05 },
                    },
                },
                _ => t,
            }),

            // The preference the homeworld forces. Named for the world, which is how the rules find
            // it, and never written into a design - which is the whole point of the test below.
            new TraitDefinition("trait_pc_continental_preference", TraitKind.Species)
            {
                Cost = 0,
                AllowedArchetypes = ["BIOLOGICAL"],
                Effects = new EffectSet
                {
                    Modifiers = new Dictionary<string, double> { ["pc_continental_habitability"] = 0.8 },
                },
            },
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

    /// <summary>
    /// A modifier behind a scope guard stays out of the totals, guard or no guard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>exists = owner</c> used to compile to a flat refusal, which decided the condition and
    /// excluded the modifier. It now compiles to an unknown, which does not decide it - and
    /// <see cref="RequirementEvaluator.CanDecide"/> keeps an undecidable group out of the
    /// arithmetic, so the number is the same.
    /// </para>
    /// <para>
    /// That equivalence is the whole licence for the change. Answering the guard <em>true</em> would
    /// have made the pair decidable on the strength of the rest, and folded in a bonus on evidence
    /// nothing has.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGuardedModifierStaysOutOfTheTotals()
    {
        // Not nomadic, so the half of the condition the design can read is satisfied. The guard is
        // still the reason the group is left alone.
        Assert.Null(Total(Context(nomadic: false), "pop_growth_speed"));
        Assert.Null(Total(Context(nomadic: true), "pop_growth_speed"));
    }

    [Fact]
    public void UnconditionalModifiersAreUnaffected()
    {
        Assert.Equal(0.1, Total(Context(nomadic: false), "ship_fire_rate_mult"));
    }

    [Fact]
    public void TheFootnoteIsAboutWhatWasActuallyLeftOut()
    {
        // Both empires carry conditions; both have ones that could not be settled. Take those away
        // and the note must go, even though the settled conditions remain.
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
                            // Anything holding an unknown anywhere in it, not only an unknown at
                            // the top. A scope guard sits inside an AND beside the condition it
                            // protects, and that pair is exactly as undecidable as a bare unknown.
                            Conditional = [.. e.Effects.Conditional.Where(c => !Unreadable(c.When))],
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

    /// <summary>Whether a condition holds anything the design cannot answer.</summary>
    /// <param name="requirement">The condition.</param>
    /// <returns>True where an unknown is in it anywhere.</returns>
    private static bool Unreadable(Requirement requirement) => requirement switch
    {
        UnknownRequirement => true,
        NotRequirement not => Unreadable(not.Item),
        AllRequirement all => all.Items.Any(Unreadable),
        AnyRequirement any => any.Items.Any(Unreadable),
        _ => false,
    };

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

    [Fact]
    public void TheRulersTraitCounts()
    {
        // The ruler is the empire's, and their trait is one of the things the player chose. It was
        // reaching neither the totals nor the breakdown: Selected walked the species' traits and
        // stopped, though the context has carried the ruler's all along.
        var design = RulesTestData.ValidEmpire();
        design.Ruler.SetTraits(["leader_trait_principled"]);

        var context = new EmpireRules(Database).CreateContext(design, new HashSet<string> { "Utopia" });

        Assert.Contains("leader_trait_principled", DesignEffects.Selected(context).Select(o => o.Key));
        Assert.Equal(0.05, Total(context, "country_unity_produces_mult"));
    }

    [Fact]
    public void TheHomeworldsPreferenceCountsAlthoughNothingWritesItDown()
    {
        // A habitability preference is forced by the world the species evolved on, and is the one
        // forced trait the file never carries - GetWrittenForcedTraits leaves it out so that a
        // design matches what the game itself would write. Its five habitability modifiers are as
        // real as any other, so the totals count it even though design.Species.Traits does not
        // mention it.
        var design = RulesTestData.ValidEmpire();

        var context = new EmpireRules(Database).CreateContext(design, new HashSet<string> { "Utopia" });

        Assert.DoesNotContain("trait_pc_continental_preference", design.Species.Traits);
        Assert.Contains("trait_pc_continental_preference", context.EffectiveTraits);
        Assert.Equal(0.8, Total(context, "pc_continental_habitability"));
    }

    [Fact]
    public void ASecondSpeciesTraitsAreNotTheEmpiresOwn()
    {
        // The founder species is the empire at the point it is being designed; a second species is
        // a population it starts alongside. Its traits belong to it and not to the empire, so they
        // stay out of both the totals and the breakdown. Stated because nothing else pins it - the
        // separation falls out of the context being built from design.Species alone, which is the
        // sort of thing a refactor takes away without noticing.
        var design = RulesTestData.ValidEmpire();
        var second = design.AddSecondarySpecies();
        second.SetTraits(["trait_intelligent", "trait_expensive"]);

        var context = new EmpireRules(Database).CreateContext(design, new HashSet<string> { "Utopia" });

        Assert.DoesNotContain("trait_expensive", context.EffectiveTraits);
        Assert.DoesNotContain("trait_expensive", DesignEffects.Selected(context).Select(o => o.Key));
    }
}
