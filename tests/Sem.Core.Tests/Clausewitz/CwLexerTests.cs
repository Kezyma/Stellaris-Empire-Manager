using Sem.Clausewitz;

namespace Sem.Core.Tests.Clausewitz;

public sealed class CwLexerTests
{
    [Fact]
    public void SplitsAssignmentIntoKeyOperatorAndValue()
    {
        var tokens = new CwLexer("authority=\"auth_corporate\"").Tokenize();

        Assert.Collection(
            tokens,
            t => Assert.Equal((CwTokenKind.BareToken, "authority"), (t.Kind, t.Text)),
            t => Assert.Equal((CwTokenKind.Operator, "="), (t.Kind, t.Text)),
            t => Assert.Equal((CwTokenKind.QuotedString, "\"auth_corporate\""), (t.Kind, t.Text)));
    }

    [Fact]
    public void QuotedStringValueExcludesTheQuotes()
    {
        var tokens = new CwLexer("\"Peacock Dynamics\"").Tokenize();

        Assert.Equal("Peacock Dynamics", tokens[0].Value);
        Assert.Equal("\"Peacock Dynamics\"", tokens[0].Text);
    }

    [Fact]
    public void EmptyQuotedStringIsAValidValue()
    {
        // initializer="" appears 58 times in the player's own designs file.
        var tokens = new CwLexer("initializer=\"\"").Tokenize();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(string.Empty, tokens[2].Value);
    }

    [Fact]
    public void WhitespaceAndCommentsBecomeLeadingTrivia()
    {
        var tokens = new CwLexer("\t# a comment\r\n\tkey=1").Tokenize();

        Assert.Equal("\t# a comment\r\n\t", tokens[0].LeadingTrivia);
        Assert.Equal("key", tokens[0].Text);
    }

    [Fact]
    public void TrailingWhitespaceIsCapturedSeparately()
    {
        var lexer = new CwLexer("key=1\r\n");
        lexer.Tokenize();

        Assert.Equal("\r\n", lexer.TrailingTrivia);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData(">")]
    [InlineData("<")]
    [InlineData(">=")]
    [InlineData("<=")]
    public void RecognisesComparisonOperatorsUsedInTriggerBlocks(string op)
    {
        var tokens = new CwLexer($"num_pops {op} 5").Tokenize();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(CwTokenKind.Operator, tokens[1].Kind);
        Assert.Equal(op, tokens[1].Text);
    }

    [Theory]
    [InlineData("@civic_default_random_weight")]
    [InlineData("gfx/interface/icons/origins/origins_forever_cruise.dds")]
    [InlineData("-0.25")]
    [InlineData("yes")]
    [InlineData("optimize_memory")]
    public void BareTokensCoverVariablesUnquotedPathsNumbersAndKeywords(string text)
    {
        var tokens = new CwLexer(text).Tokenize();

        Assert.Single(tokens);
        Assert.Equal(CwTokenKind.BareToken, tokens[0].Kind);
        Assert.Equal(text, tokens[0].Text);
    }

    [Fact]
    public void InlineMathIsOneTokenRatherThanABlock()
    {
        var tokens = new CwLexer("value = @[ base_cost * 2 ]").Tokenize();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(CwTokenKind.BareToken, tokens[2].Kind);
        Assert.Equal("@[ base_cost * 2 ]", tokens[2].Text);
    }

    [Fact]
    public void BracesAreTheirOwnTokensEvenWithoutSurroundingSpace()
    {
        var tokens = new CwLexer("colors={\"red\"\"black\"}").Tokenize();

        Assert.Equal(6, tokens.Count);
        Assert.Equal(CwTokenKind.LeftBrace, tokens[2].Kind);
        Assert.Equal(CwTokenKind.RightBrace, tokens[5].Kind);
    }

    [Fact]
    public void UnterminatedStringFailsAtItsOwnLineRatherThanSwallowingTheFile()
    {
        var error = Assert.Throws<CwSyntaxException>(
            () => new CwLexer("name=\"unterminated\r\nkey=1\r\n").Tokenize());

        Assert.Contains("Unterminated quoted string", error.Message, StringComparison.Ordinal);
    }
}
