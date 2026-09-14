using System.Text;
using Sem.Extraction;

namespace Sem.Extraction.Tests;

/// <summary>
/// The localisation format looks like YAML and is not. These cover the details that break a naive
/// reader: an optional version number, escaped quotes, comments after the value, and a byte order
/// mark on every file.
/// </summary>
public sealed class LocalisationTests
{
    private static Dictionary<string, string> Read(string content, bool withByteOrderMark = true)
    {
        var bytes = withByteOrderMark
            ? (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(content)]
            : Encoding.UTF8.GetBytes(content);

        var source = new InMemoryContentSource().Add("localisation/english/test_l_english.yml", bytes);
        return new GameDataExtractor(source.AsContent()).ExtractLocalisation();
    }

    [Fact]
    public void ReadsEntriesWithAndWithoutAVersionNumber()
    {
        var entries = Read("l_english:\n ethic_militarist:0 \"Militarist\"\n auth_democratic_election_tt: \"Resets cooldowns\"\n");

        Assert.Equal("Militarist", entries["ethic_militarist"]);
        Assert.Equal("Resets cooldowns", entries["auth_democratic_election_tt"]);
    }

    [Fact]
    public void SkipsTheLanguageHeaderAndComments()
    {
        var entries = Read("l_english:\n # a comment\n trait_adaptive:0 \"Adaptive\"\n");

        Assert.Single(entries);
        Assert.Equal("Adaptive", entries["trait_adaptive"]);
    }

    /// <summary>
    /// A comment after the value is not part of it.
    /// </summary>
    /// <remarks>
    /// Thirteen lines in the English files put one there, and none of the game's comments carries a
    /// quote of its own - measured across all hundred and fifty thousand entries - so the rule that
    /// the last quote closes the value leaves every one of them outside it.
    /// </remarks>
    [Fact]
    public void ACommentAfterTheValueIsNotPartOfIt()
    {
        var entries = Read("l_english:\n TODO:0 \"placeholder\" #debug string; no need to translate\n");

        Assert.Equal("placeholder", entries["TODO"]);
    }

    /// <summary>
    /// A value keeps the quotes inside it.
    /// </summary>
    /// <remarks>
    /// Paradox does not escape them and fifteen hundred entries have them, so a reader that stopped
    /// at the first quote ended the value there and threw the rest away. This is the shape that
    /// found it: Fanatic Materialist's description stopped at "There is no ", mid-sentence, and read
    /// as ordinary prose the whole way - which is why nothing noticed for as long as it did.
    /// </remarks>
    [Fact]
    public void AValueKeepsTheQuotesInsideIt()
    {
        var entries = Read(
            "l_english:\n ethic_x_desc:0 \"There is no \"divine spark\" in a living mind.\"\n");

        Assert.Equal("There is no \"divine spark\" in a living mind.", entries["ethic_x_desc"]);
    }

    [Fact]
    public void HandlesEscapedQuotesInsideAValue()
    {
        var entries = Read("l_english:\n event.desc:0 \"designated as \\\"New Bratulla\\\".\"\n");

        Assert.Equal("designated as \"New Bratulla\".", entries["event.desc"]);
    }

    [Fact]
    public void KeepsColourCodesAndIconsAndVariablesIntact()
    {
        // These are markup the designer renders later, not noise to strip during reading.
        var entries = Read("l_english:\n mod_x:0 \"§Y£energy£ $VALUE|0$§!\"\n");

        Assert.Equal("§Y£energy£ $VALUE|0$§!", entries["mod_x"]);
    }

    [Fact]
    public void ReadsKeysContainingDotsAndDigits()
    {
        var entries = Read("l_english:\n extreme_frontiers.1415.desc:1 \"Text\"\n");

        Assert.Equal("Text", entries["extreme_frontiers.1415.desc"]);
    }

    [Fact]
    public void TranslatesEscapedNewlinesAndTabs()
    {
        var entries = Read("l_english:\n auth_tt:1 \"First\\nSecond\"\n");

        Assert.Equal("First\nSecond", entries["auth_tt"]);
    }

    [Fact]
    public void ReadsFilesWithoutAByteOrderMark()
    {
        var entries = Read("l_english:\n key:0 \"Value\"\n", withByteOrderMark: false);

        Assert.Equal("Value", entries["key"]);
    }

    [Fact]
    public void IgnoresBlankLinesAndEntriesThatAreNotEntries()
    {
        var entries = Read("l_english:\n\n not_an_entry\n key:0 \"Value\"\n\n");

        Assert.Single(entries);
    }
}
