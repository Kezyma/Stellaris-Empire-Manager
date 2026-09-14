using System.Text;
using Sem.Extraction;

namespace Sem.Extraction.Tests;

/// <summary>
/// Writing the game's shared fragments into the definitions that call for them.
/// </summary>
/// <remarks>
/// An installation in memory rather than on disk, because what is under test is what happens when a
/// fragment is written badly - and a real installation, by definition, is not.
/// </remarks>
public sealed class ScriptLoaderTests
{
    /// <summary>A handful of files, standing in for a game folder.</summary>
    private sealed class Files(Dictionary<string, string> content) : IContentSource
    {
        public string Name => "in memory";

        public bool Contains(string relativePath) => content.ContainsKey(relativePath);

        public byte[] Read(string relativePath) => Encoding.UTF8.GetBytes(content[relativePath]);

        public bool ContainsDirectory(string relativeDirectory) =>
            content.Keys.Any(k => k.StartsWith(relativeDirectory + "/", StringComparison.Ordinal));

        public IEnumerable<string> EnumerateFiles(
            string relativeDirectory, string pattern, bool recursive = false) =>
            content.Keys
                .Where(k => k.StartsWith(relativeDirectory + "/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal);
    }

    private static ScriptLoader Loading(Dictionary<string, string> files) =>
        new(new LayeredContent([new Files(files)]));

    /// <summary>
    /// A fragment that names itself is stopped and reported, rather than followed for ever.
    /// </summary>
    /// <remarks>
    /// The depth limit counted nesting into blocks, which is not what it said it counted and not the
    /// case it was written for: a fragment is spliced into the list being walked and re-read at the
    /// same index and the same depth, so a chain of calls had no limit at all. Memory stayed flat
    /// and nothing was thrown - an extraction that simply never finished, which on a run that
    /// renders nine thousand portraits is an afternoon.
    ///
    /// Reachable from any mod, and from a damaged installation, in two lines.
    /// </remarks>
    [Fact]
    public void AFragmentThatNamesItselfIsStoppedAndSaidOutLoud()
    {
        var loader = Loading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common/traits/00_traits.txt"] = "trait_clever = { inline_script = \"loops\" }",
            ["common/inline_scripts/loops.txt"] = "inline_script = \"loops\"",
        });

        // The assertion is that this returns at all.
        var entries = loader.LoadDefinitions("common/traits").ToList();

        Assert.Single(entries);
        Assert.Contains(
            loader.Failures,
            failure => failure.Contains("name each other", StringComparison.Ordinal));
    }

    /// <summary>And two that name each other, which is the same thing a mod writes by accident.</summary>
    [Fact]
    public void TwoFragmentsThatNameEachOtherAreStoppedToo()
    {
        var loader = Loading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common/traits/00_traits.txt"] = "trait_clever = { inline_script = \"there\" }",
            ["common/inline_scripts/there.txt"] = "inline_script = \"back\"",
            ["common/inline_scripts/back.txt"] = "inline_script = \"there\"",
        });

        var entries = loader.LoadDefinitions("common/traits").ToList();

        Assert.Single(entries);
        Assert.NotEmpty(loader.Failures);
    }

    /// <summary>
    /// An ordinary chain of fragments is still followed all the way down.
    /// </summary>
    /// <remarks>
    /// The half worth guarding. A limit that stops a cycle by refusing to follow anything is not a
    /// fix, and what these fragments carry is not decoration - nine species traits keep their
    /// hidden and initial flags in one.
    /// </remarks>
    [Fact]
    public void AFragmentThatCallsAnotherIsStillFollowed()
    {
        var loader = Loading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common/traits/00_traits.txt"] = "trait_clever = { inline_script = \"first\" }",
            ["common/inline_scripts/first.txt"] = "inline_script = \"second\"",
            ["common/inline_scripts/second.txt"] = "inline_script = \"third\"",
            ["common/inline_scripts/third.txt"] = "hidden = yes\ninitial = no",
        });

        var entry = Assert.Single(loader.LoadDefinitions("common/traits").ToList());

        string? Field(string key) => entry.Node.Block!.Nodes
            .FirstOrDefault(n => n.Key == key)?.ScalarValue;

        Assert.Equal("yes", Field("hidden"));
        Assert.Equal("no", Field("initial"));
        Assert.Empty(loader.Failures);
    }
}
