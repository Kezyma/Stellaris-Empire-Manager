using Sem.Designs;
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
        Assert.Equal(PlanPath.Cybernetic, plans.PlanOf(design, Vocabulary).Path);
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
        Assert.Equal(PlanPath.Unset, plans.PlanOf(design, Vocabulary).Path);
    }

    private static EmpirePlan Cybernetic => new(PlanPath.Cybernetic, [], []);

    private static PlanVocabulary Vocabulary => PlanVocabulary.Empty;

    private static EmpireDesign Design() => EmpireDesignsFile.CreateEmpty().Add("Test");

    private static EmpirePlans Plans() => new(new PlanText(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_cybernetics"] = "Cybernetics",
            ["TRADITIONS"] = "Traditions",
            ["ASCENSION_PERKS"] = "Ascension Perks",
        })));
}
