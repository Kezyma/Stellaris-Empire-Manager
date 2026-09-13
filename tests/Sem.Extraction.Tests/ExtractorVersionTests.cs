using System.Text.RegularExpressions;
using Sem.Extraction;

namespace Sem.Extraction.Tests;

/// <summary>
/// The stamp that decides whether extracted data was built by the code now running.
/// </summary>
/// <remarks>
/// It used to be the informational version, which the SDK suffixes with the git commit because
/// SourceLink is on by default. That made it answer a far bigger question than the one being asked
/// - "has anything changed anywhere in this repository" - so a commit to a stylesheet threw away
/// 223MB of artwork and re-rendered nine thousand portraits. This is here so that cannot come back
/// quietly: adding a commit SHA to the informational version would fail these.
/// </remarks>
public sealed class ExtractorVersionTests
{
    [Fact]
    public void ItNamesTheBuildAndNotTheCommit()
    {
        var version = GameDataExtractor.ExtractorVersion;

        // A product version and a short print of the extracting code. The print is twelve hex
        // characters; a git object name is forty, which is the thing being kept out.
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+\+[0-9a-f]{12}$"), version);
        Assert.DoesNotMatch(new Regex("[0-9a-f]{40}"), version);
    }

    /// <summary>
    /// Asked twice in one process it answers the same, because it is computed once.
    /// </summary>
    /// <remarks>
    /// The stability that matters is across builds of unchanged source, which no test in a single
    /// process can see. It rests on two things checked by hand and written down here: the module
    /// identities this is built from are a function of a deterministic compilation, and the
    /// informational version no longer carries anything that moves per commit.
    /// </remarks>
    [Fact]
    public void ItIsSettledOnceAndNotRecomputed() =>
        Assert.Same(GameDataExtractor.ExtractorVersion, GameDataExtractor.ExtractorVersion);
}
