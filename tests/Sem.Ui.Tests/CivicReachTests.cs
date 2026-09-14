using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Which civics a player could never be offered, and which only look that way.
/// </summary>
/// <remarks>
/// The wiki shows every civic the game defines, including the ones belonging to fallen empires and
/// pre-FTL societies, and says which is which. Nothing in the data marks them: the only sign is a
/// condition, so the mark is worked out here and every civic in the game depends on getting it right
/// in both directions. Called unreachable, a civic disappears behind a filter; called reachable, it
/// sits in a list of things to pick from and cannot be picked.
/// </remarks>
public sealed class CivicReachTests
{
    private static CivicDefinition Civic(string key, Requirement potential) =>
        new(key, IsOrigin: false) { Potential = potential };

    private static Requirement Country(string key) =>
        new SelectionRequirement(SelectionCategory.CountryType, key);

    /// <summary>The verdict on one civic, read among whatever else is on the shelf with it.</summary>
    private static CivicReach Reach(CivicDefinition civic, params CivicDefinition[] beside) =>
        CivicReach.Across([civic, .. beside])[civic.Key];

    /// <summary>A civic saying nothing about the kind of country is one anybody may have.</summary>
    [Fact]
    public void SayingNothingAboutTheCountryLeavesItOpen()
    {
        var reach = Reach(new CivicDefinition("civic_plain", IsOrigin: false));

        Assert.True(reach.EverOffered);
        Assert.Empty(reach.CountryTypes);
    }

    /// <summary>And one that asks for the country a player is, which is the ordinary case.</summary>
    [Fact]
    public void AskingForThePlayersOwnCountryLeavesItOpen()
    {
        Assert.True(Reach(Civic("civic_plain", Country("default"))).EverOffered);
    }

    /// <summary>
    /// A civic that asks for a country no player can be is out of reach, and says which.
    /// </summary>
    /// <remarks>
    /// Naming the country type is what lets the card say who the civic belongs to rather than only
    /// that it is closed.
    /// </remarks>
    [Fact]
    public void AskingForACountryNoPlayerCanBePutsItOutOfReach()
    {
        var reach = Reach(Civic("civic_fallen", Country("fallen_empire")));

        Assert.False(reach.EverOffered);
        Assert.Equal(["fallen_empire"], reach.CountryTypes);
    }

    /// <summary>One condition among several that must all hold closes it just the same.</summary>
    [Fact]
    public void ACountryGateAmongThingsThatMustAllHoldStillCloses()
    {
        var reach = Reach(Civic("civic_fallen", new AllRequirement(
        [
            new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist"),
            Country("fallen_empire"),
        ])));

        Assert.False(reach.EverOffered);
    }

    /// <summary>
    /// But one among alternatives does not, because the other alternative may still be taken.
    /// </summary>
    /// <remarks>
    /// This is the half a two-valued walk gets wrong by refusing too much. "A fallen empire, or
    /// anybody who is militarist" is a civic a militarist player may have.
    /// </remarks>
    [Fact]
    public void ACountryGateAmongAlternativesDoesNotClose()
    {
        var reach = Reach(Civic("civic_either", new AnyRequirement(
        [
            Country("fallen_empire"),
            new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist"),
        ])));

        Assert.True(reach.EverOffered);
        Assert.Empty(reach.CountryTypes);
    }

    /// <summary>
    /// A condition saying the country is <em>not</em> a given kind leaves it open.
    /// </summary>
    /// <remarks>
    /// The other half, and the one that was actually going to bite. The game writes
    /// <c>NOT = { country_type = primitive }</c> and means "anybody who is not a primitive", which
    /// every player is - and a flat walk over the nested conditions reads it as a gate against them.
    /// The rules layer names the live case at <c>EmpireRules</c>: it is Corporate's own condition,
    /// so the wrong answer here hides one of the authorities from the wiki entirely.
    /// </remarks>
    [Fact]
    public void SayingTheCountryIsNotSomethingLeavesItOpen()
    {
        var reach = Reach(Civic("civic_not_primitive", new NotRequirement(Country("primitive"))));

        Assert.True(reach.EverOffered);
        Assert.Empty(reach.CountryTypes);
    }

    /// <summary>And saying it is not the player's own country closes it.</summary>
    /// <remarks>
    /// The same rule read the other way round, which is the check that the negation is really being
    /// evaluated rather than the word NOT being treated as "leave this one alone".
    /// </remarks>
    [Fact]
    public void SayingTheCountryIsNotThePlayersClosesIt()
    {
        Assert.False(Reach(Civic("civic_anyone_else", new NotRequirement(Country("default")))).EverOffered);
    }

    /// <summary>Two negations cancel, as they do in the script.</summary>
    [Fact]
    public void TwoNegationsCancel()
    {
        Assert.False(Reach(Civic(
            "civic_fallen",
            new NotRequirement(new NotRequirement(Country("fallen_empire"))))).EverOffered);
    }

    /// <summary>
    /// A condition about something nobody has chosen yet refuses nothing.
    /// </summary>
    /// <remarks>
    /// Which is the whole reason the evaluation has three answers rather than two. Nearly every
    /// question here is about a choice the reader has not made, so answering them false would close
    /// almost every civic in the game.
    /// </remarks>
    [Fact]
    public void AQuestionAboutAChoiceNobodyHasMadeRefusesNothing()
    {
        Assert.True(Reach(Civic("civic_conditional", new AllRequirement(
        [
            new SelectionRequirement(SelectionCategory.Ethics, "ethic_gestalt_consciousness"),
            new PredicateRequirement("is_gestalt"),
            new DlcRequirement("Utopia"),
            new UnknownRequirement("some_future_trigger"),
        ]))).EverOffered);
    }

    /// <summary>A gate the game states in Playable rather than Potential closes it too.</summary>
    /// <remarks>
    /// All three of the trees that decide whether an option can be drawn are read, because a civic
    /// no player can satisfy is out of reach whichever of them refuses it. Every one of the sixteen
    /// hidden origins is refused by Playable rather than by a country type, so reading only Potential
    /// would miss the lot.
    /// </remarks>
    [Fact]
    public void AGateStatedInAnyOfTheThreeTreesCloses()
    {
        var playable = new CivicDefinition("civic_a", IsOrigin: false)
        {
            Playable = new AlwaysRequirement(false),
        };

        var possible = new CivicDefinition("civic_b", IsOrigin: false)
        {
            Possible = Country("fallen_empire"),
        };

        Assert.False(Reach(playable).EverOffered);
        Assert.False(Reach(possible).EverOffered);
    }

    /// <summary>
    /// A civic you could only take if you already had it is out of reach.
    /// </summary>
    /// <remarks>
    /// Four civics are written this way, and the game means them: it grants them by event, so
    /// <c>civic_galactic_sovereign</c> appears for an empire that has just become Galactic Emperor
    /// and for nobody else. Nothing in the empire designer can put one in your hand first.
    /// </remarks>
    [Fact]
    public void ACivicThatRequiresItselfIsOutOfReach()
    {
        var reach = Reach(Civic(
            "civic_sovereign",
            new SelectionRequirement(SelectionCategory.Civics, "civic_sovereign")));

        Assert.False(reach.EverOffered);
        Assert.Empty(reach.CountryTypes);
    }

    /// <summary>
    /// And so is a ring of them that each want one of the others.
    /// </summary>
    /// <remarks>
    /// The reason the shelf is read as a whole rather than one civic at a time. Three origins are
    /// written exactly like this - each of the legendary leader ones asks for any of the three - and
    /// a rule that only caught a civic naming itself would let all three through, because no one of
    /// them is refused until the other two are.
    /// </remarks>
    [Fact]
    public void ARingOfCivicsThatEachWantAnotherIsOutOfReach()
    {
        Requirement any = new AnyRequirement(
        [
            new SelectionRequirement(SelectionCategory.Civics, "civic_ring_a"),
            new SelectionRequirement(SelectionCategory.Civics, "civic_ring_b"),
        ]);

        var reaches = CivicReach.Across([Civic("civic_ring_a", any), Civic("civic_ring_b", any)]);

        Assert.False(reaches["civic_ring_a"].EverOffered);
        Assert.False(reaches["civic_ring_b"].EverOffered);
    }

    /// <summary>
    /// But a civic that wants one somebody can actually take is within reach.
    /// </summary>
    /// <remarks>
    /// The other side of the same rule, and the one that keeps it from refusing everything: the
    /// editor draws the list again after every pick, so wanting another civic is a thing you satisfy
    /// in two steps rather than a thing that shuts you out.
    /// </remarks>
    [Fact]
    public void ACivicThatWantsOneAnybodyCanTakeIsWithinReach()
    {
        var reaches = CivicReach.Across(
        [
            new CivicDefinition("civic_first", IsOrigin: false),
            Civic("civic_second", new SelectionRequirement(SelectionCategory.Civics, "civic_first")),
        ]);

        Assert.True(reaches["civic_first"].EverOffered);
        Assert.True(reaches["civic_second"].EverOffered);
    }

    /// <summary>
    /// Ruling out a civic nobody can have refuses nothing.
    /// </summary>
    /// <remarks>
    /// Mutual exclusion is written this way all over the game - "not if you have that one" - and a
    /// pair that rule each other out would close both if the rule were read as refusing rather than
    /// as one more thing that depends.
    /// </remarks>
    [Fact]
    public void RulingOutACivicNobodyCanHaveRefusesNothing()
    {
        var reaches = CivicReach.Across(
        [
            Civic("civic_x", new NotRequirement(new SelectionRequirement(SelectionCategory.Civics, "civic_y"))),
            Civic("civic_y", new NotRequirement(new SelectionRequirement(SelectionCategory.Civics, "civic_x"))),
        ]);

        Assert.True(reaches["civic_x"].EverOffered);
        Assert.True(reaches["civic_y"].EverOffered);
    }

    /// <summary>A civic asking for two kinds of country names both of them.</summary>
    [Fact]
    public void EveryCountryTypeItAsksForIsNamed()
    {
        var reach = Reach(Civic(
            "civic_fallen",
            new AnyRequirement([Country("fallen_empire"), Country("awakened_fallen_empire")])));

        Assert.False(reach.EverOffered);
        Assert.Equal(["awakened_fallen_empire", "fallen_empire"], reach.CountryTypes);
    }

    /// <summary>
    /// A country type only mentioned in the negative is not named as one it asks for.
    /// </summary>
    /// <remarks>
    /// Printing it would tell the reader a closed civic belongs to primitives when what the script
    /// says is that it does not.
    /// </remarks>
    [Fact]
    public void ACountryTypeOnlyRuledOutIsNotNamed()
    {
        var reach = Reach(Civic("civic_fallen", new AllRequirement(
        [
            Country("fallen_empire"),
            new NotRequirement(Country("primitive")),
        ])));

        Assert.False(reach.EverOffered);
        Assert.Equal(["fallen_empire"], reach.CountryTypes);
    }

    /// <summary>
    /// A civic closed by something other than the country names no country at all.
    /// </summary>
    /// <remarks>
    /// <c>civic_great_khans_vision</c> is the live case, and the reason the country types are worked
    /// out by asking a second time with the country forgotten. Its condition permits an ordinary
    /// empire's country type and then requires the civic itself - so a card that simply read the
    /// country clause would announce it as belonging to awakened marauders, when what shuts the
    /// player out is that the game only ever grants it by event.
    /// </remarks>
    [Fact]
    public void ACivicClosedBySomethingElseNamesNoCountry()
    {
        var reach = Reach(Civic("civic_khan", new AllRequirement(
        [
            new SelectionRequirement(SelectionCategory.Civics, "civic_khan"),
            new AnyRequirement([Country("awakened_marauders"), Country("default")]),
        ])));

        Assert.False(reach.EverOffered);
        Assert.Empty(reach.CountryTypes);
    }
}
