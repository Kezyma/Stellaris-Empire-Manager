using System.Text;
using Sem.Clausewitz;
using Sem.Io;

namespace Sem.Core.Tests.Clausewitz;

/// <summary>
/// The guarantee this project rests on: parsing a file and writing it back produces the same
/// bytes. Without it, opening someone's empire designs to change one field could silently reformat
/// or corrupt the rest of the file.
/// </summary>
public sealed class CwRoundTripTests
{
    /// <summary>
    /// An empire designs excerpt reproducing the formatting quirks the game emits, including the
    /// tab-only line after <c>variables={</c> and the single-space line after each variable.
    /// </summary>
    private const string EmpireDesignsSample =
        "\"Peacock Dynamics\"=\r\n" +
        "{\r\n" +
        "\tkey=\"Peacock Dynamics\"\r\n" +
        "\tship_prefix=\r\n" +
        "\t{\r\n" +
        "\t\tkey=\"\"\r\n" +
        "\t}\r\n" +
        "\tspecies=\r\n" +
        "\t{\r\n" +
        "\t\tclass=\"AVI\"\r\n" +
        "\t\tgender=not_set\r\n" +
        "\t\ttrait=\"trait_aquatic\"\r\n" +
        "\t\ttrait=\"trait_organic\"\r\n" +
        "\t}\r\n" +
        "\tname=\r\n" +
        "\t{\r\n" +
        "\t\tkey=\"%LEADER_2%\"\r\n" +
        "\t\tvariables=\r\n" +
        "\t\t{\r\n" +
        "\t\t\t\r\n" +                       // tab-only line the game writes after the brace
        "\t\t\t{\r\n" +
        "\t\t\t\tkey=\"1\"\r\n" +
        "\t\t\t\tvalue=\r\n" +
        "\t\t\t\t{\r\n" +
        "\t\t\t\t\tkey=\"AVI3_CHR_Feathers_of\"\r\n" +
        "\t\t\t\t}\r\n" +
        "\t\t\t}\r\n" +
        " \r\n" +                            // single-space line after each variable element
        "\t\t}\r\n" +
        "\t}\r\n" +
        "\tempire_flag=\r\n" +
        "\t{\r\n" +
        "\t\tcolors=\r\n" +
        "\t\t{\r\n" +
        "\t\t\t\"ship_steel\"\r\n" +
        "\t\t\t\"null\"\r\n" +
        "\t\t}\r\n" +
        "\t}\r\n" +
        "\tinitializer=\"\"\r\n" +
        "\tis_nomadic=no\r\n" +
        "\ttexture=1\r\n" +
        "}\r\n";

    /// <summary>Game script style: spaces around operators, braces on the same line, comments.</summary>
    private const string GameScriptSample =
        "# Origins live in the civics folder\r\n" +
        "@civic_default_random_weight = 5\r\n" +
        "\r\n" +
        "origin_void_dwellers = {\r\n" +
        "\tis_origin = yes\r\n" +
        "\ticon = \"gfx/interface/icons/origins/origin_void_dwellers.dds\"\r\n" +
        "\tplayable = { host_has_dlc = Federations }\r\n" +
        "\tpossible = {\r\n" +
        "\t\tspecies_archetype = { NOT = { value = MACHINE } }\r\n" +
        "\t\tis_nomadic = no\r\n" +
        "\t}\r\n" +
        "\tempty_block = { }\r\n" +
        "\trandom_weight = { base = @civic_default_random_weight }\r\n" +
        "}\r\n";

    public static TheoryData<string, string> Samples => new()
    {
        { "empire designs", EmpireDesignsSample },
        { "game script", GameScriptSample },
        { "no trailing newline", "key = value" },
        { "leading blank lines", "\r\n\r\nkey = value\r\n" },
        { "empty file", "" },
        { "only whitespace", "\r\n\t \r\n" },
        { "only a comment", "# nothing but a comment\r\n" },
        { "unix line endings", "key = value\nnested = {\n\tinner = 1\n}\n" },
        { "mixed line endings", "a = 1\r\nb = 2\nc = 3\r\n" },
        { "trailing tab after brace", "colors = { red }\t\r\n" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void TextRoundTripsExactly(string description, string source)
    {
        var rewritten = CwDocument.ParseText(source).ToText();

        AssertIdentical(Encoding.UTF8.GetBytes(source), Encoding.UTF8.GetBytes(rewritten), description);
    }

    [Fact]
    public void ByteOrderMarkIsPreserved()
    {
        var withBom = (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("key = value\r\n")];

        var document = CwDocument.Parse(withBom);

        Assert.True(document.Encoding.HasByteOrderMark);
        AssertIdentical(withBom, document.ToBytes(), "BOM file");
    }

    [Fact]
    public void AbsentByteOrderMarkIsNotAdded()
    {
        var withoutBom = Encoding.UTF8.GetBytes("key = value\r\n");

        var document = CwDocument.Parse(withoutBom);

        Assert.False(document.Encoding.HasByteOrderMark);
        AssertIdentical(withoutBom, document.ToBytes(), "no-BOM file");
    }

    [Fact]
    public void BytesThatAreNotValidUtf8SurviveUnchanged()
    {
        // 0xFF is never legal UTF-8. Latin-1 keeps such files byte-exact instead of mangling them.
        var bytes = (byte[])[.. Encoding.ASCII.GetBytes("name = \""), 0xFF, 0xFE, .. Encoding.ASCII.GetBytes("\"\r\n")];

        var document = CwDocument.Parse(bytes);

        Assert.False(document.Encoding.IsUtf8);
        AssertIdentical(bytes, document.ToBytes(), "invalid UTF-8 file");
    }

    [Fact]
    public void NonAsciiTextRoundTripsAsUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("name = \"Blorg Commonality §Y£energy£§!\"\r\n");

        var document = CwDocument.Parse(bytes);

        Assert.True(document.Encoding.IsUtf8);
        AssertIdentical(bytes, document.ToBytes(), "non-ASCII file");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PlayerEmpireDesignFilesRoundTripExactly()
    {
        var files = TestPaths.SandboxDesignFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        foreach (var path in files)
        {
            var original = SafeFile.ReadAllBytes(path);
            AssertIdentical(original, CwDocument.Parse(original).ToBytes(), Path.GetFileName(path));
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PrescriptedEmpireFilesRoundTripExactly()
    {
        var files = TestPaths.SandboxPrescriptedFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        // These 21 files hold the 53 built-in empires used later as the rules-engine corpus.
        Assert.True(files.Count >= 20, $"Expected at least 20 prescripted files, found {files.Count}.");

        foreach (var path in files)
        {
            var original = SafeFile.ReadAllBytes(path);
            AssertIdentical(original, CwDocument.Parse(original).ToBytes(), Path.GetFileName(path));
        }
    }

    /// <summary>
    /// Files under <c>common/</c> that are prose documentation rather than script. The game does
    /// not load them as content and neither will we; they are excluded by name so that any new
    /// unparseable file shows up as a failure instead of hiding here.
    /// </summary>
    private static readonly HashSet<string> NonScriptFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOW_TO_MAKE_NEW_SHIPS.txt",
    };

    /// <summary>
    /// Parses every script file the game ships. This is the broadest correctness check available:
    /// thousands of files written by Paradox's own tools, covering syntax no hand-written fixture
    /// would think to include.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryGameScriptFileRoundTripsExactly()
    {
        var installRoot = TestPaths.InstallRoot;
        Skip.If(installRoot is null, "Stellaris is not installed on this machine.");

        string[] directories =
        [
            Path.Combine(installRoot!, "common"),
            Path.Combine(installRoot!, "gfx", "portraits"),
            Path.Combine(installRoot!, "prescripted_countries"),
            Path.Combine(installRoot!, "flags"),
        ];

        var checkedFiles = 0;
        var failures = new List<string>();

        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.txt", SearchOption.AllDirectories))
            {
                if (NonScriptFiles.Contains(Path.GetFileName(path)))
                {
                    continue;
                }

                var original = SafeFile.ReadAllBytes(path);
                checkedFiles++;

                try
                {
                    // Game content is read leniently: vanilla ships a file with an unclosed block.
                    var rewritten = CwDocument.Parse(original, CwParseOptions.Lenient).ToBytes();
                    if (!original.AsSpan().SequenceEqual(rewritten))
                    {
                        failures.Add($"{Relative(path)}: {DescribeFirstDifference(original, rewritten)}");
                    }
                }
                catch (CwSyntaxException ex)
                {
                    failures.Add($"{Relative(path)}: {ex.Message}");
                }
            }
        }

        Assert.True(checkedFiles > 1000, $"Expected a large corpus, only found {checkedFiles} files.");
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {checkedFiles} game script files failed to round-trip:\r\n" +
            string.Join("\r\n", failures.Take(20)));

        string Relative(string path) => Path.GetRelativePath(installRoot!, path);
    }

    [Fact]
    public void StrictParsingRejectsAnUnclosedBlock()
    {
        // The designs file is parsed strictly: a truncated block means the file was damaged, and
        // silently loading half of someone's empires would be worse than refusing.
        Assert.Throws<CwSyntaxException>(
            () => CwDocument.ParseText("species = {\r\n\tclass = AVI\r\n", options: CwParseOptions.Strict));
    }

    [Fact]
    public void LenientParsingKeepsAnUnclosedBlockAndDoesNotInventABrace()
    {
        const string source = "defined_text = {\r\n\tname = Example\r\n";

        var document = CwDocument.ParseText(source, options: CwParseOptions.Lenient);

        var block = Assert.IsType<CwBlock>(document.Nodes[0].Value);
        Assert.False(block.IsClosed);
        AssertIdentical(Encoding.UTF8.GetBytes(source), Encoding.UTF8.GetBytes(document.ToText()), "unclosed block");
    }

    [Fact]
    public void OptionalScopeSuffixIsPartOfTheIdentifier()
    {
        // owner? = { ... } means "only if the owner exists"; the ? belongs to the scope name.
        var document = CwDocument.ParseText("trigger = { owner? = { has_active_tradition = tr_x } }");

        var trigger = Assert.IsType<CwBlock>(document.Nodes[0].Value);
        Assert.Equal("owner?", trigger.Nodes[0].Key);
    }

    private static void AssertIdentical(byte[] expected, byte[] actual, string description)
    {
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return;
        }

        Assert.Fail($"{description} did not round-trip: {DescribeFirstDifference(expected, actual)}");
    }

    /// <summary>
    /// Describes where two byte arrays diverge, with visible whitespace, because the differences
    /// that matter here are usually a tab against spaces or CRLF against LF.
    /// </summary>
    private static string DescribeFirstDifference(byte[] expected, byte[] actual)
    {
        var limit = Math.Min(expected.Length, actual.Length);
        var offset = 0;
        while (offset < limit && expected[offset] == actual[offset])
        {
            offset++;
        }

        if (offset == limit)
        {
            return $"lengths differ (expected {expected.Length} bytes, wrote {actual.Length}); " +
                   $"content matches up to byte {offset}.";
        }

        return $"first difference at byte {offset} (expected 0x{expected[offset]:X2}, wrote 0x{actual[offset]:X2}).\r\n" +
               $"  expected: {Excerpt(expected, offset)}\r\n" +
               $"  actual  : {Excerpt(actual, offset)}";
    }

    private static string Excerpt(byte[] bytes, int offset)
    {
        var start = Math.Max(0, offset - 30);
        var length = Math.Min(60, bytes.Length - start);

        return Encoding.Latin1.GetString(bytes, start, length)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
