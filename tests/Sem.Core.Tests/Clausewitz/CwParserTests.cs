using Sem.Clausewitz;

namespace Sem.Core.Tests.Clausewitz;

public sealed class CwParserTests
{
    [Fact]
    public void ParsesNestedBlocks()
    {
        var document = CwDocument.ParseText("species=\r\n{\r\n\tclass=\"AVI\"\r\n\tportrait=\"avi18\"\r\n}\r\n");

        var species = Assert.Single(document.Nodes);
        Assert.Equal("species", species.Key);

        var block = Assert.IsType<CwBlock>(species.Value);
        Assert.Equal(2, block.Nodes.Count);
        Assert.Equal("AVI", block.Nodes[0].ScalarValue);
        Assert.Equal("avi18", block.Nodes[1].ScalarValue);
    }

    [Fact]
    public void KeepsRepeatedKeysAsSeparateEntries()
    {
        // trait= and ethic= repeat, and dropping duplicates would silently delete a player's traits.
        var document = CwDocument.ParseText(
            "ethic=\"ethic_fanatic_xenophile\"\r\nethic=\"ethic_pacifist\"\r\n");

        Assert.Equal(2, document.Nodes.Count);
        Assert.All(document.Nodes, n => Assert.Equal("ethic", n.Key));
        Assert.Equal("ethic_fanatic_xenophile", document.Nodes[0].ScalarValue);
        Assert.Equal("ethic_pacifist", document.Nodes[1].ScalarValue);
    }

    [Fact]
    public void ParsesListBlocksOfBareValues()
    {
        var document = CwDocument.ParseText("colors=\r\n{\r\n\t\"red\"\r\n\t\"black\"\r\n\t\"null\"\r\n}\r\n");

        var block = Assert.IsType<CwBlock>(document.Nodes[0].Value);
        Assert.Equal(3, block.Nodes.Count);
        Assert.All(block.Nodes, n => Assert.False(n.IsAssignment));
        Assert.Equal(["red", "black", "null"], block.Nodes.Select(n => n.ScalarValue));
    }

    [Fact]
    public void ParsesAnonymousBlocksInsideAList()
    {
        // The variables list in a structured name is a sequence of unkeyed blocks.
        var document = CwDocument.ParseText("variables=\r\n{\r\n\t{\r\n\t\tkey=\"1\"\r\n\t}\r\n}\r\n");

        var variables = Assert.IsType<CwBlock>(document.Nodes[0].Value);
        var element = Assert.Single(variables.Nodes);
        Assert.False(element.IsAssignment);
        Assert.NotNull(element.Block);
        Assert.Equal("1", element.Block!.Nodes[0].ScalarValue);
    }

    [Fact]
    public void ParsesQuotedTopLevelKeysUsedByTheDesignsFile()
    {
        var document = CwDocument.ParseText("\"Peacock Dynamics\"=\r\n{\r\n\tkey=\"Peacock Dynamics\"\r\n}\r\n");

        Assert.Equal("Peacock Dynamics", document.Nodes[0].Key);
        Assert.Equal(CwTokenKind.QuotedString, document.Nodes[0].KeyToken!.Kind);
    }

    [Fact]
    public void ParsesEmptyBlocks()
    {
        var document = CwDocument.ParseText("possible = { }");

        var block = Assert.IsType<CwBlock>(document.Nodes[0].Value);
        Assert.Empty(block.Nodes);
    }

    [Fact]
    public void CommentsDoNotBecomeNodes()
    {
        var document = CwDocument.ParseText("# Infernals empire\r\nkey = value # trailing\r\n");

        var node = Assert.Single(document.Nodes);
        Assert.Equal("key", node.Key);
        Assert.Equal("value", node.ScalarValue);
    }

    [Fact]
    public void MissingOperatorParsesAsTwoBareValuesRatherThanFailing()
    {
        // A shipped DLC descriptor contains exactly this malformed line. Refusing to parse it
        // would block DLC detection over a typo in Paradox's own data.
        var document = CwDocument.ParseText("paradoxplaza_store_url \"\"\r\n");

        Assert.Equal(2, document.Nodes.Count);
        Assert.All(document.Nodes, n => Assert.False(n.IsAssignment));
    }

    [Fact]
    public void UnclosedBlockIsReported()
    {
        var error = Assert.Throws<CwSyntaxException>(() => CwDocument.ParseText("species = {\r\n\tclass = AVI\r\n"));
        Assert.Contains("never closed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StrayClosingBraceIsReported()
    {
        var error = Assert.Throws<CwSyntaxException>(() => CwDocument.ParseText("key = value\r\n}\r\n"));
        Assert.Contains("no matching opening brace", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingValueIsReportedAgainstItsKey()
    {
        var error = Assert.Throws<CwSyntaxException>(() => CwDocument.ParseText("authority="));
        Assert.Contains("authority", error.Message, StringComparison.Ordinal);
    }
}
