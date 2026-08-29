using Sem.Extraction.Extractors;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// Whether a modifier reads as "+10%" or "+10" is not something its label reveals — the flat and
/// proportional forms of naval capacity share one label string — so it has to be worked out. These
/// check the cases where getting it wrong would be visible.
/// </summary>
public sealed class ModifierCatalogTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static ModifierCatalog? Catalog { get; } =
        InstallRoot is null
            ? null
            : ModifierCatalog.Read(LayeredContent.ForInstall(InstallRoot), InstallRoot);

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void UsesTheGamesOwnSettingsWhereItStatesThem()
    {
        Skip.If(Catalog is null, "Stellaris is not installed on this machine.");

        Assert.True(Catalog!.Count > 100, $"Expected the scripted modifiers, found {Catalog.Count}.");

        var declared = Catalog.Describe("pop_job_amenities_mult");

        Assert.True(declared.Declared);
        Assert.True(declared.IsPercentage);
        Assert.True(declared.IsGood);
    }

    [Fact]
    public void TheSuffixDecidesWhereThereIsOne()
    {
        Assert.True(ModifierCatalog.Empty.Describe("ship_fire_rate_mult").IsPercentage);
        Assert.False(ModifierCatalog.Empty.Describe("envoys_add").IsPercentage);

        // The suffix wins over the value, since a proportion may still be a whole number.
        Assert.True(ModifierCatalog.Empty.Describe("something_mult", [1.0]).IsPercentage);
        Assert.False(ModifierCatalog.Empty.Describe("something_add", [0.25]).IsPercentage);
    }

    [Fact]
    public void WithoutASuffixTheValuesDecide()
    {
        // These two are the reason a name-only rule is not good enough. Both lack a suffix, and the
        // game shows the first as ten percent and the second as one extra leader.
        Assert.True(ModifierCatalog.Empty.Describe("faction_approval", [0.10]).IsPercentage);
        Assert.False(ModifierCatalog.Empty.Describe("country_leader_pool_size", [1.0]).IsPercentage);

        Assert.True(ModifierCatalog.Empty.Describe("all_technology_research_speed", [0.10]).IsPercentage);
        Assert.True(ModifierCatalog.Empty.Describe("pop_government_ethic_attraction", [0.50]).IsPercentage);
        Assert.False(ModifierCatalog.Empty.Describe("planet_stability", [5.0]).IsPercentage);
    }

    [Fact]
    public void AModifierUsedBothWaysIsTreatedAsFlat()
    {
        // A fraction somewhere and a whole number elsewhere means the fraction cannot be a
        // proportion of one, so the flat reading is the safe one.
        Assert.False(ModifierCatalog.Empty.Describe("mixed_thing", [0.5, 3.0]).IsPercentage);
    }

    [Fact]
    public void SomethingAnEmpirePaysIsBetterWhenItIsLower()
    {
        Assert.False(ModifierCatalog.Empty.Describe("country_claim_influence_cost_mult").IsGood);
        Assert.False(ModifierCatalog.Empty.Describe("ships_upkeep_mult").IsGood);
        Assert.True(ModifierCatalog.Empty.Describe("ship_fire_rate_mult").IsGood);
    }

    [Fact]
    public void NothingIsClaimedToBeDeclaredWhenItWasInferred()
    {
        Assert.False(ModifierCatalog.Empty.Describe("ship_fire_rate_mult").Declared);
    }
}
