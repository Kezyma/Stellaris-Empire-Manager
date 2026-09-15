using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// A condition turned into something a page can indent.
/// </summary>
/// <remarks>
/// <para>
/// The wiki draws these as nested bullets with the join written over each group, because run
/// together into a sentence they cannot be read: Corporate Dominion's own conditions come out as
/// "not ethic Gestalt Consciousness and not authority Corporate and Oligarchic Authority and not
/// Xenophobe or Fanatic Xenophobe", where the reader has to reconstruct four levels of nesting from
/// a word order that does not carry them.
/// </para>
/// <para>
/// What is asserted here is the shape, which is the part a reader cannot check and a screenshot
/// would not show. Two things in particular: a negation is pushed down to the leaves rather than
/// left on a group, so every bullet is a tick or a cross against one thing; and the levels that say
/// nothing are dropped, because the game nests far more deeply than it means to.
/// </para>
/// </remarks>
public sealed class ConditionReaderTests
{
    private static ConditionReader Reader() =>
        new(
            new Localizer(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ethic_militarist"] = "Militarist",
                ["ethic_pacifist"] = "Pacifist",
                ["auth_corporate"] = "Corporate",
                ["civic_meritocracy"] = "Meritocracy",
                ["IS_NOMADIC"] = "Is a Nomadic Empire",
            }),
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                Ethics = [new EthicDefinition("ethic_militarist", 1, "militarist") { Icon = "icons/militarist.png" }],
                Dlc = [new DlcDefinition("utopia", "Utopia", null, null, true) { Icon = "icons/utopia.png" }],
                Icons = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GFX_toggle_nomad"] = "icons/nomad.png",
                },
            });

    private static Requirement Ethic(string key) =>
        new SelectionRequirement(SelectionCategory.Ethics, key);

    /// <summary>A condition that always holds is nothing to draw.</summary>
    /// <remarks>
    /// Most civics state four of their five trees this way, and a bullet saying "always" under a
    /// heading saying "Allowed when" is a line that has to be read to learn it says nothing.
    /// </remarks>
    [Fact]
    public void AConditionThatAlwaysHoldsIsNothingToDraw()
    {
        Assert.Null(Reader().Read(null));
        Assert.Null(Reader().Read(new AlwaysRequirement(true)));
    }

    /// <summary>One thing asked for is one bullet with a tick.</summary>
    [Fact]
    public void OneThingAskedForIsOneBulletWithATick()
    {
        var node = Reader().Read(Ethic("ethic_militarist"))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.True(node.Wanted);
        Assert.Equal("Militarist", node.Chip!.Name);
        Assert.Equal("icons/militarist.png", node.Chip.Icon);
    }

    /// <summary>And one asked against is the same bullet with a cross.</summary>
    [Fact]
    public void OneThingAskedAgainstIsTheSameBulletWithACross()
    {
        var node = Reader().Read(new NotRequirement(Ethic("ethic_militarist")))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.False(node.Wanted);
        Assert.Equal("Militarist", node.Chip!.Name);
    }

    /// <summary>Two negations cancel, as they do in the script.</summary>
    [Fact]
    public void TwoNegationsCancel()
    {
        Assert.True(Reader().Read(
            new NotRequirement(new NotRequirement(Ethic("ethic_militarist"))))!.Wanted);
    }

    /// <summary>Things that must all hold are a group saying so.</summary>
    [Fact]
    public void ThingsThatMustAllHoldAreAGroupSayingSo()
    {
        var node = Reader().Read(new AllRequirement([Ethic("ethic_militarist"), Ethic("ethic_pacifist")]))!;

        Assert.Equal(ConditionJoin.All, node.Join);
        Assert.Equal(["Militarist", "Pacifist"], node.Parts.Select(p => p.Chip!.Name));
        Assert.All(node.Parts, p => Assert.True(p.Wanted));
    }

    /// <summary>
    /// A negated group becomes the other join with every part negated.
    /// </summary>
    /// <remarks>
    /// De Morgan, and the reason no group ever carries a negation. "Not all of these" drawn
    /// literally is a crossed-out heading over ticked bullets, which reads as the opposite of itself
    /// - each bullet says you need the thing and the heading two lines up says you do not.
    /// </remarks>
    [Fact]
    public void ANegatedGroupBecomesTheOtherJoinWithItsPartsNegated()
    {
        var node = Reader().Read(new NotRequirement(
            new AllRequirement([Ethic("ethic_militarist"), Ethic("ethic_pacifist")])))!;

        Assert.Equal(ConditionJoin.Any, node.Join);
        Assert.All(node.Parts, p => Assert.False(p.Wanted));
    }

    /// <summary>And the mirror: not any of these is all of them, none held.</summary>
    [Fact]
    public void NotAnyOfTheseIsAllOfThemNotHeld()
    {
        var node = Reader().Read(new NotRequirement(
            new AnyRequirement([Ethic("ethic_militarist"), Ethic("ethic_pacifist")])))!;

        Assert.Equal(ConditionJoin.All, node.Join);
        Assert.All(node.Parts, p => Assert.False(p.Wanted));
    }

    /// <summary>
    /// A group holding one thing is drawn as the thing.
    /// </summary>
    /// <remarks>
    /// The game writes <c>NOT = { AND = { has_ethic = gestalt } }</c> where it means "not a
    /// gestalt", and Corporate Dominion states both of its own conditions that way. Drawn literally
    /// that is a heading and an indent around a single bullet, twice.
    /// </remarks>
    [Fact]
    public void AGroupHoldingOneThingIsDrawnAsTheThing()
    {
        var node = Reader().Read(new NotRequirement(new AllRequirement([Ethic("ethic_militarist")])))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.False(node.Wanted);
        Assert.Equal("Militarist", node.Chip!.Name);
    }

    /// <summary>Parts that say nothing are dropped rather than drawn as blanks.</summary>
    [Fact]
    public void PartsThatSayNothingAreDropped()
    {
        var node = Reader().Read(new AllRequirement(
        [
            new AlwaysRequirement(true),
            Ethic("ethic_militarist"),
            new AlwaysRequirement(true),
        ]))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.Equal("Militarist", node.Chip!.Name);
    }

    /// <summary>
    /// An "any of" a constant already satisfies asks nothing at all.
    /// </summary>
    /// <remarks>
    /// The other half of dropping the parts that say nothing. In an "all of" a satisfied part is
    /// filler and the rest of the group still stands; in an "any of" it settles the whole group, so
    /// what is left beside it is not a condition any more.
    ///
    /// Evangelising Zealots is the one in the game: it allows a default country or an exiled one,
    /// the first is every design there is and the second is a country type no design can be. Read
    /// the other way the page said "Played by: Never" above a list of conditions an empire plainly
    /// can meet.
    /// </remarks>
    [Fact]
    public void AnAnyOfAConstantSatisfiesAsksNothing()
    {
        var node = Reader().Read(new AllRequirement(
        [
            new AnyRequirement([new AlwaysRequirement(true), new AlwaysRequirement(false)]),
            Ethic("ethic_militarist"),
        ]))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.Equal("Militarist", node.Chip!.Name);
    }

    /// <summary>And an "any of" nothing in it can satisfy still says so.</summary>
    [Fact]
    public void AnAnyOfNothingCanSatisfyStillSaysNever()
    {
        var node = Reader().Read(
            new AnyRequirement([new AlwaysRequirement(false), new AlwaysRequirement(false)]))!;

        Assert.Equal("Never", node.Text);
    }

    /// <summary>
    /// A group of the same kind inside a group is lifted into it.
    /// </summary>
    /// <remarks>
    /// Indentation is the only thing carrying the nesting, so a level that adds none is a level that
    /// costs the reader an indent and tells them nothing.
    /// </remarks>
    [Fact]
    public void AGroupOfTheSameKindIsLiftedIntoItsParent()
    {
        var node = Reader().Read(new AllRequirement(
        [
            Ethic("ethic_militarist"),
            new AllRequirement([Ethic("ethic_pacifist"), new SelectionRequirement(SelectionCategory.Authority, "auth_corporate")]),
        ]))!;

        Assert.Equal(ConditionJoin.All, node.Join);
        Assert.Equal(["Militarist", "Pacifist", "Corporate"], node.Parts.Select(p => p.Chip!.Name));
    }

    /// <summary>A group of a different kind keeps its level, because that level is the meaning.</summary>
    [Fact]
    public void AGroupOfADifferentKindKeepsItsLevel()
    {
        var node = Reader().Read(new AllRequirement(
        [
            Ethic("ethic_militarist"),
            new AnyRequirement([Ethic("ethic_pacifist"), new SelectionRequirement(SelectionCategory.Authority, "auth_corporate")]),
        ]))!;

        Assert.Equal(ConditionJoin.All, node.Join);
        Assert.Equal(2, node.Parts.Count);
        Assert.Equal(ConditionJoin.Any, node.Parts[1].Join);
    }

    /// <summary>A content pack is a chip wearing the badge the pack bar wears.</summary>
    [Fact]
    public void APackIsAChipWearingItsOwnBadge()
    {
        var node = Reader().Read(new DlcRequirement("Utopia"))!;

        Assert.Equal("Utopia", node.Chip!.Name);
        Assert.Equal("icons/utopia.png", node.Chip.Icon);
    }

    /// <summary>
    /// Something with no chip is said in words rather than dropped.
    /// </summary>
    /// <remarks>
    /// A named check about the design as a whole has no artwork and is often the only thing standing
    /// between a player and the option. A bullet list missing the one condition that blocks you is
    /// worse than no list at all.
    /// </remarks>
    [Fact]
    public void SomethingWithNoChipIsStillSaid()
    {
        var node = Reader().Read(new PredicateRequirement("is_gestalt"))!;

        Assert.Null(node.Chip);
        Assert.Equal("Is Gestalt", node.Text);
    }

    /// <summary>A condition nothing can satisfy says so, rather than coming back empty.</summary>
    /// <remarks>
    /// Every one of the sixteen hidden origins is written this way, and it is the whole story about
    /// them: an empty panel would say the game asks nothing of you.
    /// </remarks>
    [Fact]
    public void AConditionNothingCanSatisfySaysSo()
    {
        var node = Reader().Read(new AlwaysRequirement(false))!;

        Assert.Equal(ConditionJoin.Leaf, node.Join);
        Assert.Equal("Never", node.Text);

        // Neither mark. It is a statement about the condition, not a thing to hold, and drawn like
        // the others it read "must not have: never".
        Assert.True(node.Plain);
    }

    /// <summary>
    /// Being nomadic is a chip with the game's own toggle artwork, not a sentence.
    /// </summary>
    /// <remarks>
    /// The one plain field in the whole corpus, on eighty-six civics. Drawn as words it read "Is
    /// Nomadic is No" - a sentence in the middle of a column of chips, and one negation harder to
    /// read than it needs to be.
    /// </remarks>
    [Fact]
    public void BeingNomadicIsAChipWithTheGamesOwnArtwork()
    {
        var node = Reader().Read(new FieldRequirement("is_nomadic", "yes"))!;

        Assert.Equal("Is a Nomadic Empire", node.Chip!.Name);
        Assert.Equal("icons/nomad.png", node.Chip.Icon);
        Assert.True(node.Wanted);
    }

    /// <summary>
    /// And the field's own value decides the mark, not only the nesting around it.
    /// </summary>
    /// <remarks>
    /// <c>is_nomadic = no</c> asked for is a cross against being nomadic, and asked against is a
    /// tick for it. Reading the polarity of the tree alone gets both backwards.
    /// </remarks>
    [Fact]
    public void TheFieldsOwnValueDecidesTheMark()
    {
        Assert.False(Reader().Read(new FieldRequirement("is_nomadic", "no"))!.Wanted);

        Assert.True(Reader().Read(
            new NotRequirement(new FieldRequirement("is_nomadic", "no")))!.Wanted);
    }

    /// <summary>A field nothing has artwork for is still said, in words.</summary>
    [Fact]
    public void AFieldWithNoArtworkIsStillSaid()
    {
        var node = Reader().Read(new FieldRequirement("election_type", "oligarchic"))!;

        Assert.Null(node.Chip);
        Assert.Equal("Election Type is Oligarchic", node.Text);
    }

    /// <summary>
    /// A chip carries what the thing does, so hovering it has something to show.
    /// </summary>
    /// <remarks>
    /// These are drawn as OptionChip, the same chip the empire list draws, and a chip with neither
    /// prose nor numbers opens an empty panel. The effects come from the same lookup as the icon.
    /// </remarks>
    [Fact]
    public void AChipCarriesWhatTheThingDoes()
    {
        var node = Reader().Read(Ethic("ethic_militarist"))!;

        Assert.NotNull(node.Chip!.Effects);
    }

    /// <summary>A key the shipped text has no words for is made readable rather than shown raw.</summary>
    [Fact]
    public void AKeyNobodyNamesIsMadeReadable()
    {
        var node = Reader().Read(
            new SelectionRequirement(SelectionCategory.CountryType, "fallen_empire"))!;

        Assert.Equal("Fallen Empire", node.Chip!.Name);
    }
}
