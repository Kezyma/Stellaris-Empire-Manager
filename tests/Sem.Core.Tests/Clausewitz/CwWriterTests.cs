using Sem.Clausewitz;

namespace Sem.Core.Tests.Clausewitz;

/// <summary>
/// Covers the other half of the writer's job: nodes this library creates carry no original
/// formatting, so the writer has to lay them out the way the game would have.
/// </summary>
public sealed class CwWriterTests
{
    [Fact]
    public void NewAssignmentUsesTheDesignsFileStyleWithNoSpaces()
    {
        var document = new CwDocument();
        document.Add(CwNode.QuotedAssignment("authority", "auth_corporate"));

        Assert.Equal("authority=\"auth_corporate\"", document.ToText());
    }

    [Fact]
    public void NewBlockPutsItsBraceOnTheNextLineAndIndentsChildren()
    {
        var species = new CwBlock();
        species.Add(CwNode.QuotedAssignment("class", "AVI"));
        species.Add(CwNode.BareAssignment("gender", "not_set"));

        var document = new CwDocument();
        document.Add(CwNode.Assignment("species", species));

        Assert.Equal(
            "species=\r\n{\r\n\tclass=\"AVI\"\r\n\tgender=not_set\r\n}",
            document.ToText());
    }

    [Fact]
    public void NewNestedBlocksIndentByDepth()
    {
        var inner = new CwBlock();
        inner.Add(CwNode.QuotedAssignment("key", "AVI3_CHR_Silver"));

        var outer = new CwBlock();
        outer.Add(CwNode.Assignment("value", inner));

        var document = new CwDocument();
        document.Add(CwNode.Assignment("name", outer));

        Assert.Equal(
            "name=\r\n{\r\n\tvalue=\r\n\t{\r\n\t\tkey=\"AVI3_CHR_Silver\"\r\n\t}\r\n}",
            document.ToText());
    }

    [Fact]
    public void NewListBlockPutsEachElementOnItsOwnLine()
    {
        var colors = new CwBlock();
        foreach (var color in (string[])["ship_steel", "red", "null", "null"])
        {
            colors.Add(new CwNode(CwScalar.Quoted(color)));
        }

        var document = new CwDocument();
        document.Add(CwNode.Assignment("colors", colors));

        Assert.Equal(
            "colors=\r\n{\r\n\t\"ship_steel\"\r\n\t\"red\"\r\n\t\"null\"\r\n\t\"null\"\r\n}",
            document.ToText());
    }

    [Fact]
    public void RepeatedKeysAreWrittenAsSeparateLines()
    {
        var species = new CwBlock();
        species.Add(CwNode.QuotedAssignment("trait", "trait_aquatic"));
        species.Add(CwNode.QuotedAssignment("trait", "trait_organic"));

        var document = new CwDocument();
        document.Add(CwNode.Assignment("species", species));

        Assert.Equal(
            "species=\r\n{\r\n\ttrait=\"trait_aquatic\"\r\n\ttrait=\"trait_organic\"\r\n}",
            document.ToText());
    }

    [Fact]
    public void GameScriptStyleUsesSpacedOperatorsAndSameLineBraces()
    {
        var possible = new CwBlock();
        possible.Add(CwNode.BareAssignment("is_nomadic", "no"));

        var document = new CwDocument();
        document.Add(CwNode.Assignment("origin_example", possible));

        Assert.Equal(
            "origin_example = {\r\n\tis_nomadic = no\r\n}",
            document.ToText(CwWriteOptions.GameScript));
    }

    [Fact]
    public void EmptyNewBlockIsWrittenCompactly()
    {
        var document = new CwDocument();
        document.Add(CwNode.Assignment("possible", new CwBlock()));

        Assert.Equal("possible=\r\n{}", document.ToText());
    }

    [Fact]
    public void ChangingOneValueLeavesEveryOtherByteAlone()
    {
        const string source =
            "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n\tauthority=\"auth_democratic\"\r\n\torigin=\"origin_default\"\r\n}\r\n";

        var document = CwDocument.ParseText(source);
        var empire = (CwBlock)document.Nodes[0].Value;
        empire.Nodes[1].Value = CwScalar.Quoted("auth_corporate");

        Assert.Equal(source.Replace("auth_democratic", "auth_corporate", StringComparison.Ordinal), document.ToText());
    }

    [Fact]
    public void AddingANodeToAParsedBlockIndentsItToMatchItsSiblings()
    {
        const string source = "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n}\r\n";

        var document = CwDocument.ParseText(source);
        var empire = (CwBlock)document.Nodes[0].Value;
        empire.Add(CwNode.QuotedAssignment("origin", "origin_default"));

        Assert.Equal(
            "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n\torigin=\"origin_default\"\r\n}\r\n",
            document.ToText());
    }

    [Fact]
    public void RemovingANodeLeavesTheRestFormattedCorrectly()
    {
        const string source =
            "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n\tflag=\"empire_human_1\"\r\n\torigin=\"origin_default\"\r\n}\r\n";

        var document = CwDocument.ParseText(source);
        var empire = (CwBlock)document.Nodes[0].Value;
        empire.RemoveAt(1);

        Assert.Equal(
            "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n\torigin=\"origin_default\"\r\n}\r\n",
            document.ToText());
    }

    [Fact]
    public void AddingAnEmpireToAParsedDesignsFileStartsItOnANewLine()
    {
        const string source = "\"First\"=\r\n{\r\n\tkey=\"First\"\r\n}\r\n";

        var document = CwDocument.ParseText(source);
        var second = new CwBlock();
        second.Add(CwNode.QuotedAssignment("key", "Second"));
        document.Add(CwNode.Assignment("Second", second, quoteKey: true));

        // The parsed file's trailing newline moves to the end, after the appended empire.
        Assert.Equal(
            "\"First\"=\r\n{\r\n\tkey=\"First\"\r\n}\r\n\"Second\"=\r\n{\r\n\tkey=\"Second\"\r\n}\r\n",
            document.ToText());
    }
}
