using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The letters that stand in for a picture the installation does not have.
/// </summary>
/// <remarks>
/// Untestable until it moved out of the two components that each had a copy of it. Small, and worth
/// pinning anyway: every branch here is a different kind of name, and the one that indexes into a
/// single word is one character away from throwing on a one-letter one.
/// </remarks>
public sealed class InitialsTests
{
    [Theory]
    [InlineData("Utopia", "UT")]
    [InlineData("Federations", "FE")]
    [InlineData("Synthetic Dawn", "SD")]
    [InlineData("Leviathans Story Pack", "LS")]
    public void TheFirstLettersOfTheFirstTwoWords(string name, string expected) =>
        Assert.Equal(expected, Initials.Of(name));

    /// <summary>A single letter is not two, and asking for two would throw.</summary>
    [Fact]
    public void AOneLetterNameIsOneLetter() => Assert.Equal("X", Initials.Of("x"));

    /// <summary>Nothing to take letters from still has to answer something.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameWithNoWordsIsAQuestionMark(string name) =>
        Assert.Equal("?", Initials.Of(name));

    /// <summary>Extra spacing between words is not a word.</summary>
    [Fact]
    public void RunsOfSpacesAreNotCountedAsWords() =>
        Assert.Equal("SD", Initials.Of("  Synthetic   Dawn  "));
}
