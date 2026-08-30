using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// Each test starts from a valid empire and breaks it in one specific way, so a failure names the
/// rule that stopped working rather than leaving several candidates.
/// </summary>
public sealed class EmpireRulesTests
{
    private static readonly EmpireRules Rules = new(RulesTestData.Database);

    private static readonly HashSet<string> AllDlc =
        [.. RulesTestData.Database.Dlc.Select(d => d.Name)];

    private static ValidationReport Validate(EmpireDesign design) => Rules.Validate(design, AllDlc);

    private static DesignContext Context(EmpireDesign design) => Rules.CreateContext(design, AllDlc);

    [Fact]
    public void AValidEmpirePassesEveryCheck()
    {
        var report = Validate(RulesTestData.ValidEmpire());

        Assert.True(report.IsValid, report.ToString());
    }

    // -----------------------------------------------------------------------------------------
    // Ethics
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SpendingMoreThanThreeEthicsPointsIsRejected()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetEthics(["ethic_fanatic_militarist", "ethic_fanatic_xenophile"]);

        AssertProblem(design, ValidationArea.Ethics, "points");
    }

    [Fact]
    public void TwoEthicsFromTheSameOpposingPairAreRejected()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetEthics(["ethic_militarist", "ethic_pacifist", "ethic_xenophile"]);

        AssertProblem(design, ValidationArea.Ethics, "Only one ethic");
    }

    [Fact]
    public void GestaltConsciousnessCannotBeCombinedWithOtherEthics()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetEthics(["ethic_gestalt_consciousness", "ethic_xenophile"]);

        AssertProblem(design, ValidationArea.Ethics, "cannot be combined");
    }

    [Fact]
    public void AnEmpireWithNoEthicsIsRejected()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetEthics([]);

        AssertProblem(design, ValidationArea.Ethics, "no ethics");
    }

    [Fact]
    public void EthicOptionsDisableWhatWouldBreakARule()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetEthics(["ethic_militarist"]);

        var options = Rules.GetEthicOptions(Context(design));

        // The opposing ethic shares a category with one already taken.
        Assert.False(Single(options, "ethic_pacifist").Enabled);

        // One point is spent of three, so a two-point fanatic ethic still fits exactly.
        Assert.True(Single(options, "ethic_fanatic_xenophile").Enabled);

        // A gestalt would replace the whole ethos rather than joining it.
        Assert.False(Single(options, "ethic_gestalt_consciousness").Enabled);

        // With a fanatic ethic taken, only one point remains and a second will not fit.
        design.SetEthics(["ethic_fanatic_militarist"]);
        var afterFanatic = Rules.GetEthicOptions(Context(design));

        Assert.False(Single(afterFanatic, "ethic_fanatic_xenophile").Enabled);
        Assert.True(Single(afterFanatic, "ethic_xenophile").Enabled);
    }

    // -----------------------------------------------------------------------------------------
    // Traits
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SpendingMoreTraitPointsThanTheArchetypeAllowsIsRejected()
    {
        var design = RulesTestData.ValidEmpire();

        // Two points available; intelligent alone costs two, and this adds two more.
        design.Species.SetTraits(["trait_organic", "trait_intelligent", "trait_aquatic"]);
        design.PlanetClass = "pc_ocean";

        AssertProblem(design, ValidationArea.Traits, "points");
    }

    [Fact]
    public void TakingMoreTraitsThanAllowedIsRejected()
    {
        var design = RulesTestData.ValidEmpire();

        // Five picks allowed, and traits costing nothing do not count, so six paid traits is one
        // too many even though the drawbacks pay for them.
        design.Species.SetTraits([
            "trait_organic",
            "trait_intelligent",
            "trait_deviants",
            "trait_nerve_stapled",
            "trait_not_gestalt",
            "trait_overtuned_only",
            "trait_aquatic",
        ]);

        var problems = Validate(design).Problems;
        Assert.Contains(problems, p => p.Area == ValidationArea.Traits && p.Message.Contains("traits but may have", StringComparison.Ordinal));
    }

    [Fact]
    public void TraitsCostingNothingDoNotCountTowardsThePickLimit()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_intelligent", "trait_deviants"]);

        var budget = Rules.GetTraitBudget(Context(design));

        // Three traits, but the class trait is free, so two picks are used.
        Assert.Equal(2, budget.Picks.Spent);
        Assert.Equal(5, budget.Picks.Available);
    }

    [Fact]
    public void ACivicThatWidensTheTraitAllowanceIsAppliedBeforeCheckingIt()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetCivics(["civic_natural_design", "civic_functional_architecture"]);

        var budget = Rules.GetTraitBudget(Context(design));

        Assert.Equal(4, budget.Points.Available);
        Assert.Equal(7, budget.Picks.Available);
    }

    [Fact]
    public void OpposingTraitsCannotBothBeTaken()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_intelligent", "trait_nerve_stapled"]);

        AssertProblem(design, ValidationArea.Traits, "cannot be taken");
    }

    [Fact]
    public void ATraitRestrictedToAnotherArchetypeIsRejected()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.Class = "MACHINE";
        design.Species.SetTraits(["trait_machine_unit", "trait_intelligent"]);
        design.SetEthics(["ethic_gestalt_consciousness"]);
        design.Authority = "auth_hive_mind";

        AssertProblem(design, ValidationArea.Traits, "cannot be taken");
    }

    [Fact]
    public void ATraitNeedingAParticularHomeworldIsRejectedElsewhere()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_aquatic"]);
        design.PlanetClass = "pc_continental";

        AssertProblem(design, ValidationArea.Traits, "cannot be taken");
    }

    [Fact]
    public void ATraitNeedingAParticularHomeworldIsAcceptedThere()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_aquatic"]);
        design.PlanetClass = "pc_ocean";

        Assert.True(Validate(design).IsValid, Validate(design).ToString());
    }

    [Fact]
    public void APortraitInTheOverrideListLiftsTheSpeciesClassRestriction()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_psionic_only"]);

        // The species is mammalian, not psionic, so ordinarily this trait is out of reach.
        AssertProblem(design, ValidationArea.Traits, "cannot be taken");

        // The game's own psionic empires rely on this: the portrait stands in for the class.
        design.Species.Portrait = "mam_rat";
        Assert.True(Validate(design).IsValid, Validate(design).ToString());
    }

    [Fact]
    public void TraitsTiedToAnOriginAreRejectedWithoutIt()
    {
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic", "trait_overtuned_only"]);

        AssertProblem(design, ValidationArea.Traits, "cannot be taken");
    }

    [Fact]
    public void TraitOptionsHideWhatTheGameNeverOffers()
    {
        var options = Rules.GetSpeciesTraitOptions(Context(RulesTestData.ValidEmpire()));

        Assert.DoesNotContain(options, o => o.Key == "trait_hidden");
        Assert.DoesNotContain(options, o => o.Key == "trait_not_initial");
    }

    [Fact]
    public void TraitOptionsReportTheContentPackAPlayerIsMissing()
    {
        var design = RulesTestData.ValidEmpire();
        design.PlanetClass = "pc_ocean";

        // Judged as a player who owns nothing.
        var options = Rules.GetSpeciesTraitOptions(Rules.CreateContext(design, new HashSet<string>()));
        var aquatic = Single(options, "trait_aquatic");

        Assert.False(aquatic.Enabled);
        Assert.Equal("Aquatics Species Pack", aquatic.RequiredDlc);
    }

    [Fact]
    public void AForcedTraitNamesWhatIsHoldingIt()
    {
        // "Fixed by the species class, authority, civics or origin" named four things and blamed
        // none of them, leaving a player to find out by removing things until it went away.
        var design = RulesTestData.ValidEmpire();
        var forced = Rules.GetForcedTraitSources(Context(design));

        var organic = Assert.Single(forced, f => f.Trait == "trait_organic");

        Assert.Equal(ForcedTraitSource.SpeciesClass, organic.Source);
        Assert.Equal("MAM", organic.Cause);
    }

    [Fact]
    public void AnOriginThatForcesATraitIsNamedAsTheOneDoingIt()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_void_dwellers";

        var forced = Rules.GetForcedTraitSources(Context(design));
        var dweller = Assert.Single(forced, f => f.Trait == "trait_void_dweller_1");

        Assert.Equal(ForcedTraitSource.Origin, dweller.Source);
        Assert.Equal("origin_void_dwellers", dweller.Cause);
    }

    [Fact]
    public void ATraitRestrictedToAnotherSpeciesSaysWhichOne()
    {
        // "Not for this species class" leaves a player to go and find out which class would take it,
        // and the rules have the answer in hand.
        var options = Rules.GetSpeciesTraitOptions(Context(RulesTestData.ValidEmpire()));
        var psionic = Single(options, "trait_psionic_only");

        Assert.False(psionic.Enabled);
        Assert.Contains(RuleReasons.For(RuleReasons.WrongSpeciesClass, "PSIONIC"), psionic.Reasons);
    }

    [Fact]
    public void RunningOutOfPointsDoesNotPutATraitBeyondReach()
    {
        // Two points to spend and this one costs three, with nothing else standing in its way. The
        // rules still say no — the budget is real — but they say it with a reason the interface
        // knows is temporary, so a player may take the trait anyway and settle up afterwards.
        var design = RulesTestData.ValidEmpire();
        design.Species.SetTraits(["trait_organic"]);

        var options = Rules.GetSpeciesTraitOptions(Context(design));
        var expensive = Single(options, "trait_expensive");

        Assert.False(expensive.Enabled);
        Assert.NotEmpty(expensive.Reasons);
        Assert.All(expensive.Reasons, r => Assert.Contains(r, PassingReasons));
    }

    /// <summary>The reasons that go away on their own, which the interface treats as a warning.</summary>
    private static readonly string[] PassingReasons = [RuleReasons.NotEnoughPoints, RuleReasons.NoPicksLeft];

    [Fact]
    public void TraitsForOtherArchetypesAreHiddenRatherThanListedAsBlocked()
    {
        // A biological species should not be shown the machine traits it could never take.
        var options = Rules.GetSpeciesTraitOptions(Context(RulesTestData.ValidEmpire()));

        Assert.False(Single(options, "trait_machine_unit").Visible);
        Assert.True(Single(options, "trait_intelligent").Visible);
    }

    // -----------------------------------------------------------------------------------------
    // Government
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AnAuthorityWhoseConditionsAreUnmetIsRejectedWithTheGamesExplanation()
    {
        var design = RulesTestData.ValidEmpire();
        design.Authority = "auth_hive_mind";

        var problem = AssertProblem(design, ValidationArea.Authority, "cannot be used");
        Assert.Contains("AUTHORITY_REQUIRES_GESTALT", problem.Reasons);
    }

    [Fact]
    public void AnAuthorityNeedingAnUnownedContentPackIsUnavailable()
    {
        var design = RulesTestData.ValidEmpire();
        var options = Rules.GetAuthorityOptions(Rules.CreateContext(design, new HashSet<string>()));

        Assert.False(Single(options, "auth_corporate").Visible);
        Assert.True(Single(options, "auth_democratic").Visible);
    }

    [Fact]
    public void TakingTheWrongNumberOfCivicsIsRejected()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetCivics(["civic_beacon_of_liberty"]);

        AssertProblem(design, ValidationArea.Civics, "exactly 2");
    }

    [Fact]
    public void ACivicIncompatibleWithAnotherIsRejectedWithTheGamesExplanation()
    {
        var design = RulesTestData.ValidEmpire();
        design.SetCivics(["civic_beacon_of_liberty", "civic_not_with_beacon"]);

        var problem = AssertProblem(design, ValidationArea.Civics, "cannot be combined");
        Assert.Contains("civic_tooltip_not_beacon", problem.Reasons);
    }

    [Fact]
    public void ACivicThatDoesNotApplyIsHiddenRatherThanShownAsBlocked()
    {
        // A civic only gestalts can take should not clutter an ordinary empire's list.
        var options = Rules.GetCivicOptions(Context(RulesTestData.ValidEmpire()));

        Assert.False(Single(options, "civic_gestalt_only").Visible);
        Assert.True(Single(options, "civic_beacon_of_liberty").Visible);
    }

    [Fact]
    public void TheGovernmentIsDerivedFromTheHighestWeightedMatch()
    {
        var design = RulesTestData.ValidEmpire();

        // Both the democratic and beacon governments match; the heavier one wins.
        Assert.Equal("gov_beacon", Rules.DeriveGovernment(Context(design))?.Key);

        design.SetCivics(["civic_functional_architecture", "civic_natural_design"]);
        Assert.Equal("gov_democratic", Rules.DeriveGovernment(Context(design))?.Key);
    }

    [Fact]
    public void EqualWeightsAreBrokenByWhichWasDefinedFirst()
    {
        var design = RulesTestData.ValidEmpire();

        // gov_beacon and gov_beacon_tie both match at the same weight.
        Assert.Equal("gov_beacon", Rules.DeriveGovernment(Context(design))?.Key);
    }

    // -----------------------------------------------------------------------------------------
    // Origins and homeworlds
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AnOriginThatReplacesTheHomeworldRestrictsTheChoiceToIt()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_void_dwellers";

        Assert.Equal(["pc_habitat"], Rules.GetHomeworldOptions(Context(design)));
    }

    [Fact]
    public void AHomeworldNoOriginOverridesMustBeOneTheEmpireCanStartOn()
    {
        var design = RulesTestData.ValidEmpire();
        design.PlanetClass = "pc_habitat";

        AssertProblem(design, ValidationArea.Homeworld, "not a homeworld this empire can start on");
    }

    [Fact]
    public void AHomeworldAnOriginOverridesIsReportedWithoutInvalidatingTheDesign()
    {
        // The game loads such a design and uses the origin's world, so rejecting it would refuse
        // empires the player has been happily playing.
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_void_dwellers";
        design.PlanetClass = "pc_continental";

        var report = Validate(design);

        Assert.True(report.IsValid, report.ToString());
        var warning = Assert.Single(report.Warnings);
        Assert.Equal(ValidationArea.Homeworld, warning.Area);
        Assert.Contains("is ignored", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AHomeworldNeedingAnUnownedContentPackIsNotOffered()
    {
        var design = RulesTestData.ValidEmpire();

        var withoutPack = Rules.GetHomeworldOptions(Rules.CreateContext(design, new HashSet<string>()));
        Assert.DoesNotContain("pc_relic", withoutPack);

        var withPack = Rules.GetHomeworldOptions(
            Rules.CreateContext(design, new HashSet<string> { "Ancient Relics Story Pack" }));
        Assert.Contains("pc_relic", withPack);
    }

    [Fact]
    public void AWorldTheGameNeverOffersIsNotOfferedUntilSomethingAddsIt()
    {
        // A volcanic world is flagged initial and flagged starting_planet = no beside it. Reading
        // only the first put it in front of every empire that owned the pack, which is not a choice
        // the game has: an Infernal species class or a civic has to bring it.
        var design = RulesTestData.ValidEmpire();

        Assert.DoesNotContain("pc_volcanic", Rules.GetHomeworldOptions(Context(design)));

        design.SetCivics(["civic_hearth_of_the_forge"]);
        Assert.Contains("pc_volcanic", Rules.GetHomeworldOptions(Context(design)));
    }

    [Fact]
    public void ANomadicEmpireBeginsOnItsShipAndNowhereElse()
    {
        // The game's own arkship empire has no origin or civic that says so; being nomadic is what
        // puts it there, and the arkship is a world no one else may pick.
        var design = RulesTestData.ValidEmpire();
        design.IsNomadic = true;

        Assert.Equal(["pc_ark"], Rules.GetHomeworldOptions(Context(design)));
    }

    [Fact]
    public void AnOriginThatNamesItsStartingSystemsReplacesTheUsualChoice()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_void_dwellers";

        Assert.Equal(["void_dweller_system"], Rules.GetStartingSystemOptions(Context(design)));
    }

    [Fact]
    public void AnOriginNeedingASecondSpeciesIsRejectedWithoutOne()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_syncretic_evolution";

        AssertProblem(design, ValidationArea.SecondarySpecies, "second species");
    }

    [Fact]
    public void AnOriginNeedingASecondSpeciesIsAcceptedWithOne()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_syncretic_evolution";
        design.AddSecondarySpecies().Class = "MAM";

        Assert.True(Validate(design).IsValid, Validate(design).ToString());
    }

    [Fact]
    public void ForcedTraitsComeFromTheClassAuthorityCivicsAndOrigin()
    {
        var design = RulesTestData.ValidEmpire();
        design.Origin = "origin_void_dwellers";

        var forced = Rules.GetForcedTraits(Context(design));

        Assert.Contains("trait_organic", forced);
        Assert.Contains("trait_void_dweller_1", forced);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------------------------

    private static ValidationProblem AssertProblem(EmpireDesign design, ValidationArea area, string contains)
    {
        var report = Validate(design);

        var problem = report.Problems.FirstOrDefault(
            p => p.Area == area && p.Message.Contains(contains, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            problem is not null,
            $"Expected a {area} problem mentioning '{contains}'. Got: {report}");

        return problem!;
    }

    private static OptionState Single(IReadOnlyList<OptionState> options, string key) =>
        Assert.Single(options, o => o.Key == key);
}
