using System.Text;
using Sem.Extraction;
using Sem.Extraction.Extractors;
using Sem.GameData;

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

    /// <summary>
    /// The pruner keeps the words for everything a page can open, descriptions included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>loc/en.json</c> is cut down to what the database reaches, seeded one kind at a time. Two
    /// kinds named only their title: the planet classes and the government types, neither of which
    /// had a <c>DescriptionKey</c> to seed from. So sixty-seven planet paragraphs and a hundred and
    /// seventy government ones were dropped, and both wiki shelves drew a name with nothing under
    /// it - on a page whose whole job is saying what a thing is.
    /// </para>
    /// <para>
    /// This is the test that catches it, and it has to live here rather than beside the shelf: a
    /// test over the wiki hands the session its own words and would pass either way. What failed
    /// was the file being written, not the row reading it.
    /// </para>
    /// </remarks>
    [Fact]
    public void KeepsTheDescriptionOfEveryKindAPageCanOpen()
    {
        var database = new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            PlanetClasses = [new PlanetClassDefinition("pc_ocean")],
            GovernmentTypes = [new GovernmentTypeDefinition("gov_test", 10, 0)],
            Ethics = [new EthicDefinition("ethic_militarist", 1, "militarist")],
            Authorities = [new AuthorityDefinition("auth_democratic")],
        };

        var all = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pc_ocean"] = "Ocean World",
            ["pc_ocean_desc"] = "Rocky world with a significant hydrosphere.",
            ["gov_test"] = "Test Dominion",
            ["gov_test_desc"] = "A government of some kind.",
            ["ethic_militarist"] = "Militarist",
            ["ethic_militarist_desc"] = "War is the answer.",
            ["auth_democratic"] = "Democratic",
            ["auth_democratic_desc"] = "Elections every so often.",
            ["unreferenced_key"] = "Nothing points at this.",
        };

        var kept = LocalisationPruner.Prune(database, all);

        // Each kind's name and its prose, which is what a card is made of.
        foreach (var key in new[]
        {
            "pc_ocean", "pc_ocean_desc",
            "gov_test", "gov_test_desc",
            "ethic_militarist", "ethic_militarist_desc",
            "auth_democratic", "auth_democratic_desc",
        })
        {
            Assert.True(kept.ContainsKey(key), $"{key} was pruned away.");
        }

        // And the pruning still prunes, or this would pass by keeping the whole file.
        Assert.False(kept.ContainsKey("unreferenced_key"));
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
