using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Putting a plan into an empire, and never into something somebody wrote.
/// </summary>
/// <remarks>
/// A plan lives in one of the two biographies because those are fields the game keeps. Which means
/// the feature is one mistake away from deleting a player's writing, and that mistake was made once
/// already: turning planning on wrote over the species biography without asking. These are the tests
/// that stop it coming back.
/// </remarks>
public sealed class EmpirePlansTests
{
    [Fact]
    public void APlanGoesIntoAnEmptyBiography()
    {
        var design = Design();

        Plans().Write(design, Cybernetic, PlanHome.Species, Vocabulary);

        Assert.Contains("Cybernetics", design.Species.Biography!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A biography with writing in it is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// The important half. There is no way to tell a biography somebody meant from one they would
    /// not miss, so anything with words in it is off limits and the choice is the player's.
    /// </remarks>
    [Fact]
    public void APlanNeverWritesOverSomebodysWriting()
    {
        const string theirs = "The Blorg are a friendly people who wish only to be loved.";

        var design = Design();
        design.Species.Biography = theirs;

        Plans().Write(design, Cybernetic, PlanHome.Species, Vocabulary);

        Assert.Equal(theirs, design.Species.Biography);
        Assert.Null(Plans().HomeOf(design, Vocabulary));
    }

    [Fact]
    public void ABiographyWithWritingInItCannotCarryAPlan()
    {
        var design = Design();
        design.Species.Biography = "Something the player wrote.";

        Assert.False(Plans().CanCarry(design, PlanHome.Species, Vocabulary));
        Assert.True(Plans().CanCarry(design, PlanHome.Ruler, Vocabulary));
    }

    /// <summary>A biography already holding a plan is free, because the plan is ours to replace.</summary>
    [Fact]
    public void ABiographyAlreadyHoldingAPlanIsFree()
    {
        var design = Design();
        Plans().Write(design, Cybernetic, PlanHome.Species, Vocabulary);

        Assert.True(Plans().CanCarry(design, PlanHome.Species, Vocabulary));
    }

    [Fact]
    public void MovingAPlanTakesItOutOfTheOneItWasIn()
    {
        var design = Design();
        var plans = Plans();

        plans.Write(design, Cybernetic, PlanHome.Species, Vocabulary);
        plans.Write(design, Cybernetic, PlanHome.Ruler, Vocabulary);

        Assert.Null(design.Species.Biography);
        Assert.Equal(PlanHome.Ruler, plans.HomeOf(design, Vocabulary));
        Assert.Equal(["tradition_cybernetics"], plans.PlanOf(design, Vocabulary).Trees);
    }

    /// <summary>Turning planning off gives the field back rather than leaving a husk in it.</summary>
    [Fact]
    public void ClearingTakesTheWholeBiographyAway()
    {
        var design = Design();
        var plans = Plans();

        plans.Write(design, Cybernetic, PlanHome.Species, Vocabulary);
        plans.Clear(design, PlanHome.Species, Vocabulary);

        Assert.Null(design.Species.Biography);
        Assert.Null(plans.HomeOf(design, Vocabulary));
    }

    /// <summary>Clearing leaves a biography that was never a plan alone.</summary>
    [Fact]
    public void ClearingDoesNotDeleteSomebodysWriting()
    {
        const string theirs = "Notes on the Blorg.";

        var design = Design();
        design.Species.Biography = theirs;

        Plans().Clear(design, PlanHome.Species, Vocabulary);

        Assert.Equal(theirs, design.Species.Biography);
    }

    /// <summary>
    /// A plan that has decided nothing is still a plan, and is still there to go on deciding with.
    /// </summary>
    /// <remarks>
    /// This used to empty the field, which was right until the path started being worked out from
    /// the perks rather than chosen. Now releasing the last perk leaves a plan naming nothing, and
    /// emptying the biography at that moment would switch planning off underneath the player in the
    /// middle of editing. Emptying is Clear's job, and Clear is what the checkbox calls.
    /// </remarks>
    [Fact]
    public void APlanThatHasDecidedNothingIsStillAPlan()
    {
        var design = Design();
        var plans = Plans();

        plans.Write(design, Cybernetic, PlanHome.Species, Vocabulary);
        plans.Write(design, EmpirePlan.Empty, PlanHome.Species, Vocabulary);

        Assert.NotNull(design.Species.Biography);
        Assert.Equal(PlanHome.Species, plans.HomeOf(design, Vocabulary));
        Assert.False(plans.PlanOf(design, Vocabulary).Any);
    }

    /// <summary>
    /// The session's vocabulary names both halves of a plan, not just one.
    /// </summary>
    /// <remarks>
    /// This is a real defect caught in the browser rather than a hypothetical. The vocabulary was
    /// built from the ascension perks alone, so a tradition tree could be written into a biography
    /// and then not read back out of it - the picker took the click, the plan was saved, and the
    /// tree vanished on the next render with nothing said. Anything a plan can name has to be
    /// nameable in both directions.
    /// </remarks>
    [Fact]
    public void TheSessionCanNameEverythingAPlanMayHold()
    {
        var session = new DesignSession(new Sem.Ui.Services.GameData(
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                AscensionPerks = [new AscensionPerkDefinition("ap_flesh")],
                TraditionTrees = [new TraditionTreeDefinition("tradition_cybernetics")],
                Civics = [new CivicDefinition("civic_meritocracy", IsOrigin: false)],
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ap_flesh"] = "The Flesh is Weak",
                ["tradition_cybernetics"] = "Cybernetics",
                ["civic_meritocracy"] = "Meritocracy",
            },
            "assets"));

        Assert.Equal("ap_flesh", session.PlanVocabulary.Perk("The Flesh is Weak"));
        Assert.Equal("tradition_cybernetics", session.PlanVocabulary.Tree("Cybernetics"));
        Assert.Equal("civic_meritocracy", session.PlanVocabulary.Civic("Meritocracy"));
    }

    private static EmpirePlan Cybernetic => new(["tradition_cybernetics"], [], []);

    /// <summary>
    /// What a plan may name here, which has to include the tree the plan above uses.
    /// </summary>
    /// <remarks>
    /// Empty would do for writing and not for reading: a plan is recognised by its marker but its
    /// contents are resolved through this, so a vocabulary that cannot name the tree would write it
    /// and then hand back a plan without it.
    /// </remarks>
    private static PlanVocabulary Vocabulary =>
        new([("tradition_cybernetics", "Cybernetics")], [], []);

    private static EmpireDesign Design() => EmpireDesignsFile.CreateEmpty().Add("Test");

    private static EmpirePlans Plans() => new(new PlanText(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_cybernetics"] = "Cybernetics",
        })));
}
