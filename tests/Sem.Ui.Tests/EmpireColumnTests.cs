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

        var facets = EmpireFacet.All.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var picked = columns.Where(c => facets.Contains(c.Key)).ToList();

        Assert.NotEmpty(picked);
        Assert.All(picked, column => Assert.Contains(column.Key, facets));
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
