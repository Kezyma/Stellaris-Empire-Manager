using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Turning a compiled condition back into a sentence.
/// </summary>
/// <remarks>
/// What the designer shows about a choice it will not let you make. The game writes its own wording
/// for most conditions and that wording wins; what is left is the tree, and this walks it. The
/// failure to guard against is the same one <see cref="ReasonWriterTests"/> guards: a condition
/// coming out as its own script name in the middle of a picker, which reads as machine text to
/// somebody who only wanted to know why the button is grey.
/// </remarks>
public sealed class ConditionWriterTests
{
    private static ConditionWriter Writer() => new(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ethic_fanatic_xenophile"] = "Fanatic Xenophile",
            ["ethic_militarist"] = "Militarist",
            ["civic_agrarian_idyll"] = "Agrarian Idyll",
            ["origin_shattered_ring"] = "Shattered Ring",
            ["is_xenophile_tooltip"] = "Is some degree of Xenophile",
        }));

    private static Requirement Ethic(string key) =>
        new SelectionRequirement(SelectionCategory.Ethics, key);

    /// <summary>A condition the game wrote a sentence for is given that sentence.</summary>
    /// <remarks>
    /// The game's own explanation is better than anything derived from the tree, and this is the
    /// common case - most conditions arrive carrying one.
    /// </remarks>
    [Fact]
    public void TheGamesOwnWordingWins()
    {
        var requirement = Ethic("ethic_militarist") with { FailureText = "is_xenophile_tooltip" };

        Assert.Equal("Is some degree of Xenophile", Writer().Describe(requirement));
    }

    /// <summary>A selection is named by what kind of thing it is and what it is.</summary>
    [Fact]
    public void ASelectionReadsAsItsKindAndItsName()
    {
        Assert.Equal("ethic Fanatic Xenophile", Writer().Describe(Ethic("ethic_fanatic_xenophile")));
        Assert.Equal(
            "civic Agrarian Idyll",
            Writer().Describe(new SelectionRequirement(SelectionCategory.Civics, "civic_agrarian_idyll")));
    }

    /// <summary>Several that must all hold read as a list joined by "and".</summary>
    [Fact]
    public void EverythingThatMustHoldReadsAsAnAndList()
    {
        var requirement = new AllRequirement([Ethic("ethic_militarist"), Ethic("ethic_fanatic_xenophile")]);

        Assert.Equal("ethic Militarist and ethic Fanatic Xenophile", Writer().Describe(requirement));
    }

    /// <summary>And a choice between them by "or".</summary>
    [Fact]
    public void AChoiceReadsAsAnOrList()
    {
        var requirement = new AnyRequirement([Ethic("ethic_militarist"), Ethic("ethic_fanatic_xenophile")]);

        Assert.Equal("ethic Militarist or ethic Fanatic Xenophile", Writer().Describe(requirement));
    }

    /// <summary>
    /// A negation turns the sentence around, and turns the list's joining word around with it.
    /// </summary>
    /// <remarks>
    /// Not both of these is one or the other missing, so "and" becomes "or" underneath a NOT. Read
    /// the other way it says the opposite of what the game means - the two are not interchangeable
    /// and the mistake would be invisible in any test that only looked at one item.
    /// </remarks>
    [Fact]
    public void ANegatedListSwapsItsJoiningWord()
    {
        var both = new AllRequirement([Ethic("ethic_militarist"), Ethic("ethic_fanatic_xenophile")]);

        Assert.Equal(
            "not ethic Militarist or not ethic Fanatic Xenophile",
            Writer().Describe(new NotRequirement(both)));

        var either = new AnyRequirement([Ethic("ethic_militarist"), Ethic("ethic_fanatic_xenophile")]);

        Assert.Equal(
            "not ethic Militarist and not ethic Fanatic Xenophile",
            Writer().Describe(new NotRequirement(either)));
    }

    /// <summary>Two negations cancel, rather than stacking into "not not".</summary>
    [Fact]
    public void TwoNegationsCancel()
    {
        var requirement = new NotRequirement(new NotRequirement(Ethic("ethic_militarist")));

        Assert.Equal("ethic Militarist", Writer().Describe(requirement));
    }

    /// <summary>A content pack reads as owning it, or not owning it.</summary>
    [Fact]
    public void APackReadsAsHavingIt()
    {
        Assert.Equal("with Utopia", Writer().Describe(new DlcRequirement("Utopia")));
        Assert.Equal(
            "without Utopia",
            Writer().Describe(new NotRequirement(new DlcRequirement("Utopia"))));
    }

    /// <summary>A script name is turned into ordinary words rather than shown as written.</summary>
    /// <remarks>
    /// <c>is_nomadic</c> is not a thing to put in front of somebody. The <c>is_</c> goes and the
    /// underscores become spaces, which is the whole of the transformation and all it needs to be.
    /// </remarks>
    [Fact]
    public void AScriptNameIsTurnedIntoWords()
    {
        Assert.Equal(
            "nomadic yes",
            Writer().Describe(new FieldRequirement("is_nomadic", "yes")));

        Assert.Equal(
            "gestalt consciousness",
            Writer().Describe(new PredicateRequirement("is_gestalt_consciousness")));
    }

    /// <summary>
    /// Something only a running game could answer is still named.
    /// </summary>
    /// <remarks>
    /// Saying so is more use than saying nothing: it tells the player this is not something they
    /// start with, rather than leaving a blank where a reason should be.
    /// </remarks>
    [Fact]
    public void SomethingOnlyAGameCouldAnswerIsStillNamed()
    {
        Assert.Equal("has encountered a crisis", Writer().Describe(
            new UnknownRequirement("has_encountered_a_crisis")));
    }

    /// <summary>
    /// And it is negated like everything else, rather than stating the opposite of the truth.
    /// </summary>
    /// <remarks>
    /// This arm alone ignored the negation, so a leader trait whose bonus applies while NOT on the
    /// council was headed "When councilor", immediately under the game's own sentence saying the
    /// reverse.
    /// </remarks>
    [Fact]
    public void AndSomethingOnlyAGameCouldAnswerIsNegatedTheSameWay()
    {
        Assert.Equal("not councilor", Writer().Describe(
            new NotRequirement(new UnknownRequirement("is_councilor"))));
    }

    /// <summary>
    /// The same thing asked twice is said once.
    /// </summary>
    /// <remarks>
    /// "A, or A and B" is A. The game writes its scripted triggers this way so they answer from
    /// either scope - Mining Rush asks whether the owner is nomadic, or whether the thing FROM
    /// points at is a country and is nomadic - and read literally the heading over its numbers came
    /// out as the same clause twice with a scope check wedged between them.
    /// </remarks>
    [Fact]
    public void ASecondWayOfAskingTheSameThingIsSaidOnce()
    {
        var said = Writer().Describe(new AnyRequirement(
        [
            Ethic("ethic_militarist"),
            new AllRequirement([new UnknownRequirement("is_scope_type"), Ethic("ethic_militarist")]),
        ]));

        Assert.Equal("ethic Militarist", said);
    }

    /// <summary>And the same law the other way up.</summary>
    [Fact]
    public void AndTheSameWhereEverythingMustHold()
    {
        var said = Writer().Describe(new AllRequirement(
        [
            Ethic("ethic_militarist"),
            new AnyRequirement([Ethic("ethic_fanatic_xenophile"), Ethic("ethic_militarist")]),
        ]));

        Assert.Equal("ethic Militarist", said);
    }

    /// <summary>A branch that is genuinely a second option is not absorbed.</summary>
    [Fact]
    public void ARealAlternativeSurvives()
    {
        var said = Writer().Describe(new AnyRequirement(
        [
            Ethic("ethic_militarist"),
            new AllRequirement(
                [new UnknownRequirement("is_scope_type"), Ethic("ethic_fanatic_xenophile")]),
        ]));

        Assert.Equal("ethic Militarist or scope type and ethic Fanatic Xenophile", said);
    }

    /// <summary>A condition that is always true has nothing to say, and says nothing.</summary>
    /// <remarks>
    /// Null rather than an empty string, because the caller draws nothing at all for null and would
    /// draw an empty line for the other.
    /// </remarks>
    [Fact]
    public void AConditionThatIsAlwaysTrueSaysNothing()
    {
        Assert.Null(Writer().Describe(new AlwaysRequirement(true)));
        Assert.Null(Writer().Describe(null));
    }

    /// <summary>And one that can never hold says so in a word.</summary>
    [Fact]
    public void AConditionThatCanNeverHoldSaysNever()
    {
        Assert.Equal("never", Writer().Describe(new AlwaysRequirement(false)));
    }

    /// <summary>An empty list has nothing in it to describe, so there is no sentence.</summary>
    [Fact]
    public void AnEmptyListProducesNothingRatherThanAnEmptySentence()
    {
        Assert.Null(Writer().Describe(new AllRequirement([])));
        Assert.Null(Writer().Describe(new AnyRequirement([])));
    }

    /// <summary>
    /// A tree deeper than three levels stops rather than running on.
    /// </summary>
    /// <remarks>
    /// The game's conditions nest arbitrarily and a sentence four levels deep is not one anybody
    /// reads, so there is a limit. What it produces is nothing at all rather than a truncated
    /// sentence, and that is worth knowing rather than assuming.
    /// </remarks>
    [Fact]
    public void AVeryDeepTreeStopsRatherThanRunningOn()
    {
        Requirement deep = Ethic("ethic_militarist");

        for (var level = 0; level < 6; level++)
        {
            deep = new AllRequirement([deep]);
        }

        // Nothing at all, which is what the limit produces: the innermost item is past it, so it
        // describes as nothing, and every list above it is then a list of nothing. Worth pinning
        // because it has a cost - an option blocked by a condition this deep gives no reason for
        // itself - and that is a decision rather than an accident.
        Assert.Null(Writer().Describe(deep));
    }
}
