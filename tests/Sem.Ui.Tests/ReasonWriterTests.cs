using Sem.Rules;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The sentences a blocked option gives for itself.
/// </summary>
/// <remarks>
/// Nearly every condition in the game arrives wrapped in wording the game wrote, and that wording
/// wins. What is left is the handful the game states as a bare list, and those are phrased here -
/// so the failure this guards against is a reason falling through to the localizer and being drawn
/// as its own key, which reads as machine text in the middle of a picker.
/// </remarks>
public sealed class ReasonWriterTests
{
    /// <summary>
    /// Something the option asks for and the design has not got is named.
    /// </summary>
    /// <remarks>
    /// The one the ascension trees needed. Nothing on the Cybernetics tree asks for The Flesh is
    /// Weak - <c>tr_cybernetics_adopt</c> does, in a condition the game never wrote a sentence for -
    /// so every ascension tree was blocked and none of them said why.
    /// </remarks>
    [Fact]
    public void SomethingTheDesignHasNotGotIsNamed()
    {
        var reason = RuleReasons.For(RuleReasons.Missing, "ap_the_flesh_is_weak");

        Assert.Equal("Needs The Flesh is Weak", Writer().Describe(reason));
    }

    /// <summary>A reason naming several things reads as a choice between them.</summary>
    [Fact]
    public void OneOfSeveralReadsAsAChoice()
    {
        var reason = RuleReasons.For(RuleReasons.Missing, "ap_the_flesh_is_weak, ap_mind_over_matter");

        Assert.Equal("Needs The Flesh is Weak or Mind Over Matter", Writer().Describe(reason));
    }

    /// <summary>
    /// A reason the game did write a sentence for is shown in the game's own words.
    /// </summary>
    /// <remarks>
    /// Here to say which way round the two are: the phrasing above is the fallback, and anything
    /// carrying its own wording keeps it.
    /// </remarks>
    [Fact]
    public void TheGamesOwnWordingIsLeftAlone()
    {
        Assert.Equal("Is some degree of Xenophile", Writer().Describe("is_xenophile_tooltip"));
    }

    private static ReasonWriter Writer() => new(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ap_the_flesh_is_weak"] = "The Flesh is Weak",
            ["ap_mind_over_matter"] = "Mind Over Matter",
            ["is_xenophile_tooltip"] = "Is some degree of Xenophile",
        }));
}
