using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Which shelf of the wiki a key belongs to, which is the whole of what makes a chip a link.
/// </summary>
/// <remarks>
/// A chip carries a key and nothing else - an ethic inside a civic's requirements is the string
/// <c>ethic_militarist</c> - so pressing one to go and read about it means asking the game what that
/// key is. Worth holding still because the wrong answer is silent: a key read as the wrong kind
/// sends the reader to a page that does not contain it and scrolls to nothing.
/// </remarks>
public sealed class WikiLinkTests
{
    private static readonly WikiLinks Links = new(new GameDatabase
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Civics =
        [
            new CivicDefinition("civic_meritocracy", IsOrigin: false),
            new CivicDefinition("origin_shattered_ring", IsOrigin: true),
        ],
        Ethics = [new EthicDefinition("ethic_militarist", 1, "militarist")],
        Authorities = [new AuthorityDefinition("auth_democratic")],
        SpeciesClasses = [new SpeciesClassDefinition("TOX", "ART")],
        Traits = [new TraitDefinition("trait_intelligent", TraitKind.Species)],
    });

    /// <summary>Each kind of key lands on its own shelf.</summary>
    /// <param name="key">What a chip carries.</param>
    /// <param name="route">Where pressing it should go.</param>
    [Theory]
    [InlineData("civic_meritocracy", "wiki/civics/civic_meritocracy")]
    [InlineData("origin_shattered_ring", "wiki/origins/origin_shattered_ring")]
    [InlineData("ethic_militarist", "wiki/ethics/ethic_militarist")]
    [InlineData("auth_democratic", "wiki/authorities/auth_democratic")]
    [InlineData("TOX", "wiki/species/TOX")]
    [InlineData("trait_intelligent", "wiki/species-traits/trait_intelligent")]
    public void AKeyGoesToTheShelfItIsOn(string key, string route) =>
        Assert.Equal(route, Links.For(key));

    /// <summary>
    /// And a key the wiki has no page for is left alone.
    /// </summary>
    /// <remarks>
    /// Most of what a requirement names is still not a wiki entry. A planet class, an ascension perk
    /// and a global flag are all things a civic can ask for, and drawing them as links would promise
    /// a page behind every one of them.
    /// </remarks>
    [Theory]
    [InlineData("pc_ringworld_habitable")]
    [InlineData("")]
    [InlineData(null)]
    public void AKeyTheWikiDoesNotCoverIsNotALink(string? key)
    {
        Assert.Null(Links.For(key));
        Assert.Null(Links.Shelf(key));
    }

    /// <summary>An origin and a civic are the same record with a flag, and the flag decides.</summary>
    [Fact]
    public void AnOriginIsToldFromACivicByItsFlag()
    {
        Assert.Equal(WikiKind.Civics, Links.Shelf("civic_meritocracy"));
        Assert.Equal(WikiKind.Origins, Links.Shelf("origin_shattered_ring"));
    }

    /// <summary>
    /// A leader trait is not a link, because it is not in the database to be found.
    /// </summary>
    /// <remarks>
    /// Its page is built from a file of the wiki's own, fetched only when somebody opens it, and
    /// nothing outside that page has any reason to name one - no empire can hold one. So a chip
    /// carrying one of these keys would have nowhere to go, and asking the database is the same
    /// answer as asking whether anything else can reach it.
    /// </remarks>
    [Fact]
    public void ALeaderTraitIsNotALinkBecauseNothingElseNamesOne() =>
        Assert.Null(Links.For("leader_trait_carefree"));

    /// <summary>A shelf's own address is the one its tab points at.</summary>
    [Fact]
    public void AShelfHasTheAddressItsTabPointsAt()
    {
        Assert.Equal("wiki/civics", WikiLinks.Section(WikiKind.Civics));
        Assert.Equal("wiki/origins", WikiLinks.Section(WikiKind.Origins));
        Assert.Equal("wiki/ethics", WikiLinks.Section(WikiKind.Ethics));
        Assert.Equal("wiki/authorities", WikiLinks.Section(WikiKind.Authorities));
        Assert.Equal("wiki/species", WikiLinks.Section(WikiKind.Species));
        Assert.Equal("wiki/species-traits", WikiLinks.Section(WikiKind.SpeciesTraits));
        Assert.Equal("wiki/leader-traits", WikiLinks.Section(WikiKind.LeaderTraits));
    }
}
