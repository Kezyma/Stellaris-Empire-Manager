using Sem.Extraction;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// Which traits reach the wiki's own file, and which reach both it and the database.
/// </summary>
/// <remarks>
/// The thirty-four an empire may start with are not a separate kind of thing: every one declares a
/// leader class like any other leader trait and carries <c>starting_ruler_trait</c> on top. The
/// classifier answers with the more specific kind, which is right for the database - but reading
/// that as "not a leader trait" cut twenty-seven upgrade chains in half, leaving a second and third
/// tier on the page with no first.
/// </remarks>
public sealed class LeaderTraitPackTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    /// <summary>The extractor, read once, since it walks thirty-five thousand files.</summary>
    private static readonly Lazy<GameDataExtractor> Read = new(() =>
    {
        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        extractor.Extract();
        return extractor;
    });

    /// <summary>A trait an empire may start with is carried in both places, and says so.</summary>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AStartingTraitIsCarriedInBothPlaces()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var extractor = Read.Value;
        var database = extractor.Extract();

        var starting = database.Traits
            .Where(t => t.Kind == TraitKind.StartingRuler)
            .Select(t => t.Key)
            .ToHashSet(StringComparer.Ordinal);

        var canStart = extractor.LeaderTraits
            .Where(t => t.CanStart)
            .Select(t => t.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(34, starting.Count);
        Assert.Equal(starting, canStart);

        // And nothing else claims it, which is the half that could go wrong quietly: the column
        // would read yes down the page and nobody would question it.
        Assert.Equal(34, extractor.LeaderTraits.Count(t => t.CanStart));
    }

    /// <summary>
    /// Every chain has its first tier, which is the whole point of carrying them twice.
    /// </summary>
    /// <remarks>
    /// Twenty-seven of the game's upgrade chains begin at a trait an empire may start with. Without
    /// them the page showed a second and third tier whose first did not exist, and the second tier
    /// had to borrow its name from a trait nothing on the page named.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryUpgradeChainHasItsFirstTier()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var carried = Read.Value.LeaderTraits.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        var dangling = Read.Value.LeaderTraits
            .SelectMany(t => t.Replaces.Select(r => (Trait: t.Key, Missing: r)))
            .Where(pair => !carried.Contains(pair.Missing))
            .ToList();

        Assert.True(
            dangling.Count == 0,
            $"{dangling.Count} tier(s) replace a trait the file does not carry: " +
            string.Join(", ", dangling.Take(5).Select(d => $"{d.Trait} -> {d.Missing}")));
    }

    /// <summary>
    /// A trait naming its leader class as a bare word is still a leader's.
    /// </summary>
    /// <remarks>
    /// <c>leader_trait_rift_warped</c> writes <c>leader_class = all</c> rather than a block, and has
    /// no <c>leader_trait_type</c> to fall back on. Asking only for a block filed it as a species
    /// trait - one leader trait sitting in the list the designer offers a founding species from.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ATraitNamingItsClassAsABareWordIsALeadersNotASpeciesTrait()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        Assert.Contains(Read.Value.LeaderTraits, t => t.Key == "leader_trait_rift_warped");
        Assert.DoesNotContain(Read.Value.Extract().Traits, t => t.Key == "leader_trait_rift_warped");
    }

    /// <summary>And the database still holds only what a design can hold.</summary>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheDatabaseStillCarriesNoLeaderOnlyTraits()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Read.Value.Extract();

        Assert.DoesNotContain(database.Traits, t => t.Kind == TraitKind.Leader);
        Assert.Equal(34, database.Traits.Count(t => t.Kind == TraitKind.StartingRuler));
        Assert.True(
            database.Traits.Count(t => t.Kind == TraitKind.Species) >= 360,
            $"Only {database.Traits.Count(t => t.Kind == TraitKind.Species)} species traits survived.");
    }
}
