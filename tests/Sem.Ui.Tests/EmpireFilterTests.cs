using Sem.Designs;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Narrowing the empire lists down to the ones somebody asked about.
/// </summary>
/// <remarks>
/// The rule the whole card rests on is "any within a heading, all across them", and it is the kind
/// of rule that is only wrong in a result nobody can explain: a reader who ticks a second civic and
/// watches the list get shorter concludes the filter is broken, and they are right. Worth holding
/// still in tests, because the only other thing checking it is somebody looking at the page.
///
/// Rows are built by hand rather than read from designs, which is what keeps these running without
/// a copy of the game: what is being checked is the matching, and a row is a bag of values however
/// it was filled.
/// </remarks>
public sealed class EmpireFilterTests
{
    [Fact]
    public void AnEmptyFilterKeepsEverything()
    {
        var filter = new EmpireFilter();

        Assert.False(filter.Any);
        Assert.True(filter.Matches(Row("Blorg Commonality")));
    }

    [Fact]
    public void TwoChoicesUnderOneHeadingAskForEither()
    {
        var filter = new EmpireFilter();
        filter.Civics.Add("civic_anglers");
        filter.Civics.Add("civic_agrarian_idyll");

        Assert.True(filter.Matches(Row("Anglers", civics: ["civic_anglers"])));
        Assert.True(filter.Matches(Row("Idyllic", civics: ["civic_agrarian_idyll"])));
        Assert.False(filter.Matches(Row("Neither", civics: ["civic_beacon_of_liberty"])));
    }

    [Fact]
    public void TwoHeadingsBothHaveToBeSatisfied()
    {
        var filter = new EmpireFilter();
        filter.Civics.Add("civic_anglers");
        filter.Ethics.Add("ethic_egalitarian");

        Assert.True(filter.Matches(Row(
            "Both", civics: ["civic_anglers"], ethics: ["ethic_egalitarian"])));

        Assert.False(filter.Matches(Row("Only the civic", civics: ["civic_anglers"])));
        Assert.False(filter.Matches(Row("Only the ethic", ethics: ["ethic_egalitarian"])));
    }

    /// <summary>
    /// An empire holds several of some headings and one of others, and both have to work.
    /// </summary>
    /// <remarks>
    /// The single-valued ones are a separate path - there is no list to look through - and an
    /// empire holding none of the thing being asked about is the case that would otherwise throw.
    /// </remarks>
    [Fact]
    public void AHeadingAnEmpireHasNothingUnderNeverMatches()
    {
        var filter = new EmpireFilter();
        filter.Origins.Add("origin_default");

        Assert.False(filter.Matches(Row("No origin at all")));
        Assert.True(filter.Matches(Row("Prosperous", origin: "origin_default")));
    }

    [Fact]
    public void TheNameIsSearchedWithoutRegardToCase()
    {
        var filter = new EmpireFilter { Search = "blorg" };

        Assert.True(filter.Matches(Row("Blorg Commonality")));
        Assert.False(filter.Matches(Row("Scyldari Confederacy")));
    }

    /// <summary>
    /// The species' name is searched too, which is how most empires are actually found.
    /// </summary>
    /// <remarks>
    /// A file full of empires named for their species is the ordinary case, and typing the species
    /// into a box that only read empire names found nothing at all.
    /// </remarks>
    [Fact]
    public void TheSpeciesNameIsSearchedAsWellAsTheEmpires()
    {
        var filter = new EmpireFilter { Search = "Blorg" };

        Assert.True(filter.Matches(Row("United Nations of Earth", species: "Blorg")));
    }

    [Fact]
    public void ClearingPutsEveryHeadingBack()
    {
        var filter = new EmpireFilter { Search = "blorg" };
        filter.Civics.Add("civic_anglers");
        filter.Traits.Add("trait_aquatic");

        Assert.True(filter.Any);
        Assert.Equal(3, filter.Headings);

        filter.Clear();

        Assert.False(filter.Any);
        Assert.Equal(0, filter.Headings);
        Assert.True(filter.Matches(Row("Anything at all")));
    }

    /// <summary>
    /// Every column reads something out of every row, including the ones a row has nothing for.
    /// </summary>
    /// <remarks>
    /// The table sorts by these, and a sort is a comparison against every other row - so one column
    /// throwing on one empty empire takes the whole table down rather than leaving a blank cell.
    /// </remarks>
    [Fact]
    public void EveryColumnReadsAnEmptyEmpireWithoutComplaining()
    {
        var row = Row("Bare");

        foreach (var column in EmpireColumn.All)
        {
            Assert.NotNull(column.Text(row));
            Assert.NotNull(column.Chips?.Invoke(row) ?? []);
        }
    }

    /// <summary>The columns are addressed by key, so two sharing one would hide each other.</summary>
    [Fact]
    public void NoTwoColumnsShareAKey()
    {
        var keys = EmpireColumn.All.Select(c => c.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    private static EmpireRow Row(
        string name,
        string? species = null,
        IReadOnlyList<string>? civics = null,
        IReadOnlyList<string>? ethics = null,
        string? origin = null) =>
        new()
        {
            Design = EmpireDesignsFile.CreateEmpty().Add(name),
            Name = name,
            Government = string.Empty,
            Ethics = [.. (ethics ?? []).Select(Choice)],
            Civics = [.. (civics ?? []).Select(Choice)],
            Origin = origin is null ? null : Choice(origin),
            SpeciesName = species ?? string.Empty,
            Traits = [],
            PlanetClass = string.Empty,
            PlanetName = string.Empty,
            StartingSystem = string.Empty,
            Shipset = string.Empty,
            Advisor = string.Empty,
            RulerName = string.Empty,
            RulerClass = string.Empty,
            RulerTraits = [],
            ShipPrefix = string.Empty,
        };

    private static EmpireChoice Choice(string key) => new(key, key, null, null);
}
