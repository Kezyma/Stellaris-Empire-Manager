using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// One column of the empire table: what it is called, and what it reads out of a row.
/// </summary>
/// <remarks>
/// A table rather than markup per column, because three separate things have to agree about what
/// the columns are - the header row, the cells, and the picker that turns them on and off - and
/// three lists of seventeen is three chances to disagree.
/// </remarks>
/// <param name="Key">What the column is remembered under.</param>
/// <param name="Header">What it is called.</param>
/// <param name="OnByDefault">Whether it is shown before anybody has chosen.</param>
/// <param name="Choices">What the cell draws, where the cell is things that were picked.</param>
/// <param name="Line">What the cell says, where it is a line of text somebody typed.</param>
/// <param name="Stacked">
/// Whether the cell shows the first of its choices with the rest behind it rather than all of them
/// side by side.
/// </param>
public sealed record EmpireColumn(
    string Key,
    string Header,
    bool OnByDefault,
    Func<EmpireRow, IReadOnlyList<EmpireChoice>>? Choices = null,
    Func<EmpireRow, string>? Line = null,
    bool Stacked = false)
{
    /// <summary>Every column, in the order they are drawn.</summary>
    /// <remarks>
    /// The name is first and is not in the picker: a table of empires with no empire named is a grid
    /// of adjectives, and there would be no way to get the column back.
    /// </remarks>
    public static IReadOnlyList<EmpireColumn> All { get; } =
    [
        Picked("preset", false),
        Picked("government", false),

        // Stacked, because these are the one column whose cells hold alternatives rather than a
        // set: the empire is given one of them, and the likeliest is most of the answer. Laid out
        // flat, five of them set the width of a column nobody asked to be that wide.
        Picked("personality", false, stacked: true),

        // The one column that is not read straight off its heading: nomadic belongs in the cell
        // beside the authority, where the game puts it and where the card already draws it.
        new("authority", "Authority", true, r => r.AuthorityChips),

        Picked("nomadic", false),
        Picked("ethics", true),
        Picked("civics", true),
        Picked("origin", true),
        Picked("class", true),
        new("speciesname", "Species name", false, Line: r => r.SpeciesName),
        Picked("portrait", false),
        Picked("gender", false),
        Picked("namelist", false),
        Picked("traits", false),
        Picked("second", false),
        Picked("secondclass", false),
        Picked("secondtraits", false),
        Picked("homeworld", true),
        new("planet", "Planet", false, Line: r => r.PlanetName),
        Picked("system", false),
        Picked("room", false),
        Picked("shipset", false),
        Picked("bioship", true),
        Picked("advisor", false),
        new("ruler", "Ruler", false, Line: r => r.RulerName),
        Picked("rulerclass", false),
        Picked("rulerportrait", false),
        Picked("rulergender", false),
        Picked("rulertraits", false),
        Picked("plantraditions", false),
        Picked("planperks", false),
        Picked("plancivics", false),
        new("prefix", "Ship prefix", false, Line: r => r.ShipPrefix),
        Picked("spawn", false),
        Picked("fallen", false),
        Picked("flagset", false),

        // The colours the empire is actually drawn in. All off by default, like everything else
        // that is a detail rather than an identity - the table already opens on nine columns. The
        // flag's first two are here because every heading has a column, not because a reader is
        // likely to want them: the flag itself is already drawn in the name cell.
        Picked("primary", false),
        Picked("secondary", false),
        Picked("tertiary", false),
        Picked("shipcolor", false),
        Picked("mapborder", false),
        Picked("mapfill", false),
    ];

    /// <summary>
    /// The column for a heading that is also a filter, named and read the same way as the filter.
    /// </summary>
    /// <remarks>
    /// Written once from the facet, so a column and the control that narrows it can never be called
    /// different things or disagree about what an empire holds.
    /// </remarks>
    private static EmpireColumn Picked(string key, bool onByDefault, bool stacked = false)
    {
        // Named rather than found missing. These run in a static initialiser, so a key with no
        // facet behind it used to surface as a TypeInitializationException wrapping "sequence
        // contains no matching element" - a blank page saying nothing about which of the nineteen
        // keys was wrong. EmpireColumnTests walks this list, so a typo is a red test first.
        var facet = EmpireFacet.All.FirstOrDefault(f => f.Key == key)
            ?? throw new InvalidOperationException(
                $"No empire facet is called '{key}'. A column is written from its facet, so the two "
                + $"lists have to agree; the facets are: "
                + $"{string.Join(", ", EmpireFacet.All.Select(f => f.Key))}.");

        return new EmpireColumn(facet.Key, facet.Label, onByDefault, facet.Values, Stacked: stacked);
    }

    /// <summary>What the column sorts by, which for several choices is all of them run together.</summary>
    public string Text(EmpireRow row) =>
        Line is { } line ? line(row) : string.Join(", ", Choices!(row).Select(c => c.Name));
}
