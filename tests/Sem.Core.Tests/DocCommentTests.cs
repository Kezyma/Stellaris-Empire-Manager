using Sem.Io;

namespace Sem.Core.Tests;

/// <summary>
/// The doc-comment mistake the compiler cannot catch.
/// </summary>
/// <remarks>
/// <para>
/// A member carrying two <c>&lt;summary&gt;</c> elements is always a mistake, and always the same
/// one: something was inserted between a comment and the thing it described, so the comment now sits
/// on the newcomer and the member it was written for has none. The generated XML simply carries both
/// summaries and says nothing.
/// </para>
/// <para>
/// <c>GenerateDocumentationFile</c> does not check for it. Roslyn has diagnostics for malformed XML
/// and for a <c>&lt;param&gt;</c> naming an argument that does not exist, and nothing at all for a
/// repeated <c>&lt;summary&gt;</c> - which is why six of these accumulated unnoticed, and why fixing
/// all six in one pass was immediately followed by a seventh being introduced three commits later,
/// by the same hand, in the same way.
/// </para>
/// <para>
/// So it is checked here instead. A test is the only thing in this solution that can.
/// </para>
/// </remarks>
public sealed class DocCommentTests
{
    /// <summary>No member carries more than one summary.</summary>
    [SkippableFact]
    public void NoMemberCarriesTwoSummaries()
    {
        var root = SandboxLayout.FindRepositoryRoot();

        Skip.If(root is null, "Not running inside the repository, so there are no sources to read.");

        var doubled = new List<string>();

        foreach (var file in Sources(Path.Combine(root!, "src")))
        {
            foreach (var (line, summaries) in Blocks(file))
            {
                if (summaries > 1)
                {
                    doubled.Add($"{Path.GetRelativePath(root!, file).Replace('\\', '/')}:{line}");
                }
            }
        }

        Assert.Empty(doubled);
    }

    /// <summary>Every C# and Razor file under a directory, leaving the build output out.</summary>
    private static IEnumerable<string> Sources(string directory) =>
        Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));

    /// <summary>
    /// Each run of consecutive <c>///</c> lines, with the line it starts on and how many summaries
    /// it opens.
    /// </summary>
    /// <remarks>
    /// A run rather than a parse. What matters is only whether one unbroken comment block declares
    /// the same element twice, and any line that is not a doc comment ends the block - which is
    /// exactly how the compiler decides what a comment is attached to.
    /// </remarks>
    private static IEnumerable<(int Line, int Summaries)> Blocks(string file)
    {
        var lines = File.ReadAllLines(file);
        var start = -1;
        var summaries = 0;

        for (var at = 0; at < lines.Length; at++)
        {
            if (lines[at].TrimStart().StartsWith("///", StringComparison.Ordinal))
            {
                if (start < 0)
                {
                    start = at + 1;
                }

                if (lines[at].Contains("<summary>", StringComparison.Ordinal))
                {
                    summaries++;
                }

                continue;
            }

            if (start >= 0)
            {
                yield return (start, summaries);
            }

            start = -1;
            summaries = 0;
        }

        if (start >= 0)
        {
            yield return (start, summaries);
        }
    }
}
