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
        filter.Keys("civics").Add("civic_anglers");
        filter.Keys("civics").Add("civic_agrarian_idyll");

        Assert.True(filter.Matches(Row("Anglers", civics: ["civic_anglers"])));
        Assert.True(filter.Matches(Row("Idyllic", civics: ["civic_agrarian_idyll"])));
        Assert.False(filter.Matches(Row("Neither", civics: ["civic_beacon_of_liberty"])));
    }

    /// <summary>
    /// Switched to "all", a heading asks for every one of them rather than any.
    /// </summary>
    /// <remarks>
    /// The other half of the control beside each list. "Any" is the default because it is what tick
    /// boxes read as and because each extra tick then widens the answer; "all" is what somebody
    /// means when they are looking for a particular combination.
    /// </remarks>
    [Fact]
    public void SwitchedToAllAHeadingAsksForEveryOne()
    {
        var filter = new EmpireFilter();
        filter.Keys("civics").Add("civic_anglers");
        filter.Keys("civics").Add("civic_agrarian_idyll");
        filter.SetRequiresAll("civics", true);

        Assert.True(filter.Matches(Row("Both", civics: ["civic_anglers", "civic_agrarian_idyll"])));
        Assert.False(filter.Matches(Row("One", civics: ["civic_anglers"])));
    }

    /// <summary>Only the headings an empire can hold several of are offered the choice.</summary>
    /// <remarks>
    /// An empire has one authority and any number of civics, so asking for all of two authorities
    /// is a question with no answer - and a control that can only ever empty the list looks broken.
    /// The filter would answer it correctly; the card simply does not ask.
    /// </remarks>
    [Fact]
    public void AllIsOfferedOnlyWhereAnEmpireCanHoldSeveral()
    {
        var several = EmpireFacet.All.Where(f => f.Several).Select(f => f.Key);

        Assert.Equal(["ethics", "civics", "traits", "rulertraits"], several);
    }

    [Fact]
    public void TwoHeadingsBothHaveToBeSatisfied()
    {
        var filter = new EmpireFilter();
        filter.Keys("civics").Add("civic_anglers");
        filter.Keys("ethics").Add("ethic_egalitarian");

        Assert.True(filter.Matches(Row(
            "Both", civics: ["civic_anglers"], ethics: ["ethic_egalitarian"])));

        Assert.False(filter.Matches(Row("Only the civic", civics: ["civic_anglers"])));
        Assert.False(filter.Matches(Row("Only the ethic", ethics: ["ethic_egalitarian"])));
    }

    /// <summary>
    /// An empire holds several of some headings and one of others, and both have to work.
    /// </summary>
    /// <remarks>
    /// The single-valued ones read as a list of none or one, which is what lets the matching treat
    /// every heading the same way - and an empire holding none of the thing being asked about is
    /// the case that would otherwise throw.
    /// </remarks>
    [Fact]
    public void AHeadingAnEmpireHasNothingUnderNeverMatches()
    {
        var filter = new EmpireFilter();
        filter.Keys("origin").Add("origin_default");

        Assert.False(filter.Matches(Row("No origin at all")));
        Assert.True(filter.Matches(Row("Prosperous", origin: "origin_default")));
    }

    [Fact]
    public void TheSearchReadsWithoutRegardToCase()
    {
        var filter = new EmpireFilter { Search = "blorg" };

        Assert.True(filter.Matches(Row("Blorg Commonality")));
        Assert.False(filter.Matches(Row("Scyldari Confederacy")));
    }

    /// <summary>
    /// The search covers every word somebody typed, not only the empire's own name.
    /// </summary>
    /// <remarks>
    /// A file of empires named for their species, or for their ruler, is the ordinary case, and a
    /// box that only read empire names found none of them.
    /// </remarks>
    [Fact]
    public void TheSearchCoversEverythingThatWasTypedRatherThanPicked()
    {
        var row = Row("United Nations of Earth", text: "Sol Blorg Emperor Vlad UNS");

        Assert.True(new EmpireFilter { Search = "Blorg" }.Matches(row));
        Assert.True(new EmpireFilter { Search = "vlad" }.Matches(row));
        Assert.True(new EmpireFilter { Search = "UNS" }.Matches(row));
        Assert.False(new EmpireFilter { Search = "Tzynn" }.Matches(row));
    }

    [Fact]
    public void ClearingPutsEveryHeadingBack()
    {
        var filter = new EmpireFilter { Search = "blorg" };
        filter.Keys("civics").Add("civic_anglers");
        filter.Keys("traits").Add("trait_aquatic");

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
            Assert.NotNull(column.Choices?.Invoke(row) ?? []);
        }
    }

    /// <summary>Every facet reads an empty empire too, since the filter asks all of them.</summary>
    [Fact]
    public void EveryHeadingReadsAnEmptyEmpireWithoutComplaining()
    {
        var row = Row("Bare");

        Assert.All(EmpireFacet.All, facet => Assert.NotNull(facet.Values(row)));
    }

    /// <summary>The columns are addressed by key, so two sharing one would hide each other.</summary>
    [Fact]
    public void NoTwoColumnsShareAKey()
    {
        var keys = EmpireColumn.All.Select(c => c.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A heading that is also a column is called the same thing in both places.
    /// </summary>
    /// <remarks>
    /// The columns are written from the facets for exactly this reason, and the test is here so it
    /// stays that way: a filter labelled "Species" narrowing a column headed "Class" is two names
    /// for one field.
    /// </remarks>
    [Fact]
    public void EveryHeadingHasAColumnCalledTheSameThing()
    {
        foreach (var facet in EmpireFacet.All)
        {
            var column = EmpireColumn.All.SingleOrDefault(c => c.Key == facet.Key);

            Assert.NotNull(column);
            Assert.Equal(facet.Label, column.Header);
        }
    }

    private static EmpireRow Row(
        string name,
        string? text = null,
        IReadOnlyList<string>? civics = null,
        IReadOnlyList<string>? ethics = null,
        string? origin = null) =>
        new()
        {
            Design = EmpireDesignsFile.CreateEmpty().Add(name),
            Name = name,
            Text = text ?? name,
            Ethics = [.. (ethics ?? []).Select(Choice)],
            Civics = [.. (civics ?? []).Select(Choice)],
            Origin = origin is null ? null : Choice(origin),
            Traits = [],
            RulerTraits = [],
            SpeciesName = string.Empty,
            PlanetName = string.Empty,
            RulerName = string.Empty,
            ShipPrefix = string.Empty,
        };

    private static EmpireChoice Choice(string key) => new(key, key, null, null);
}
