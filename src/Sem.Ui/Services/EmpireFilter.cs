using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// What the reader has narrowed the lists down to.
/// </summary>
/// <remarks>
/// <para>
/// Held by the page and handed to both tables, so one set of filters narrows the player's empires
/// and the game's together - which is the point of them: the question "who has Fanatic Purifiers"
/// is not a question about one of the two lists.
/// </para>
/// <para>
/// Any within a heading by default, and all across them. Choosing two civics asks for empires with
/// either, and choosing a civic and an ethic asks for empires with both. That is what a set of
/// checkboxes reads as, and the other way round - two civics meaning "both" - makes each extra tick
/// narrow the list towards nothing, which is a filter that punishes you for exploring it. A heading
/// that can hold several can be switched to "all" for exactly the times that is the question.
/// </para>
/// </remarks>
public sealed class EmpireFilter
{
    /// <summary>Anything typed, matched against every word of the empire that was typed rather than picked.</summary>
    public string Search { get; set; } = string.Empty;

    private readonly Dictionary<string, Chosen> _headings = new(StringComparer.Ordinal);

    /// <summary>What is ticked under one heading, which the control for it edits in place.</summary>
    public HashSet<string> Keys(string facet) => Under(facet).Keys;

    /// <summary>Whether the heading wants every one of them rather than any.</summary>
    public bool RequiresAll(string facet) => Under(facet).All;

    /// <summary>Says whether a heading wants every one of its ticks rather than any.</summary>
    /// <param name="facet">Which heading.</param>
    /// <param name="all">True to require them all, false to accept any.</param>
    public void SetRequiresAll(string facet, bool all) => Under(facet).All = all;

    private Chosen Under(string facet)
    {
        ArgumentException.ThrowIfNullOrEmpty(facet);

        if (!_headings.TryGetValue(facet, out var chosen))
        {
            chosen = new Chosen();
            _headings[facet] = chosen;
        }

        return chosen;
    }

    /// <summary>Whether anything is being asked at all, which is what decides if Clear is offered.</summary>
    public bool Any => Search.Length > 0 || _headings.Values.Any(c => c.Keys.Count > 0);

    /// <summary>How many headings are being asked about, for the line that says so.</summary>
    public int Headings =>
        (Search.Length > 0 ? 1 : 0) + _headings.Values.Count(c => c.Keys.Count > 0);

    /// <summary>Drops the search text and every tick, leaving nothing being asked.</summary>
    public void Clear()
    {
        Search = string.Empty;

        foreach (var chosen in _headings.Values)
        {
            chosen.Keys.Clear();
        }
    }

    /// <summary>Whether one empire answers everything currently being asked.</summary>
    /// <param name="row">The empire to judge.</param>
    /// <returns>True when it matches the search text and every heading's ticks.</returns>
    public bool Matches(EmpireRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (Search.Length > 0 && !row.Text.Contains(Search, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        foreach (var facet in EmpireFacet.All)
        {
            if (!_headings.TryGetValue(facet.Key, out var chosen) || chosen.Keys.Count == 0)
            {
                continue;
            }

            var held = facet.Values(row);

            var satisfied = chosen.All
                ? chosen.Keys.All(key => held.Any(c => c.Key == key))
                : held.Any(c => chosen.Keys.Contains(c.Key));

            if (!satisfied)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class Chosen
    {
        public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);

        public bool All { get; set; }
    }
}
