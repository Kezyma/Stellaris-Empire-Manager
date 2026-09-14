using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// That the table's headings and the filters agree about what an empire holds.
/// </summary>
/// <remarks>
/// A column is written from the facet of the same name, so the two can never disagree about what a
/// heading means - but the name is a string in both places, and the columns are built in a static
/// initialiser. A key with no facet behind it therefore surfaced as a TypeInitializationException
/// wrapping "sequence contains no matching element", which reaches a player as a blank page and a
/// developer as a stack trace naming neither the column nor the key. Reading the list here is enough
/// to turn that into a failing test, and the message now names the key.
/// </remarks>
public sealed class EmpireColumnTests
{
    [Fact]
    public void EveryColumnHasAFacetBehindIt()
    {
        // The initialiser is what is under test: it throws if any key is wrong.
        var columns = EmpireColumn.All;

        Assert.NotEmpty(columns);

        // Which columns are written from a facet is the thing worth stating, and the initialiser
        // has already refused any that names one that does not exist. What used to be here filtered
        // the list by "has a facet" and then asserted that each one had a facet, which cannot fail.
        var facets = EmpireFacet.All.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var free = columns.Where(c => !facets.Contains(c.Key)).Select(c => c.Key).Order();

        // Four, and each is something an empire has rather than something it is given: a typed
        // name, not a choice off a list.
        Assert.Equal(["planet", "prefix", "ruler", "speciesname"], free);
    }

    [Fact]
    public void NoTwoColumnsShareAKey()
    {
        var keys = EmpireColumn.All.Select(c => c.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryColumnHasAHeading() =>
        Assert.All(EmpireColumn.All, column => Assert.False(string.IsNullOrWhiteSpace(column.Header)));
}
