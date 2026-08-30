using Sem.GameData;

namespace Sem.Rules;

/// <summary>A species and the world it came from, all of a piece.</summary>
/// <param name="Name">The species name.</param>
/// <param name="Plural">Its plural.</param>
/// <param name="HomePlanet">A name for the homeworld.</param>
/// <param name="HomeSystem">A name for the home system.</param>
/// <param name="NameList">The name list that goes with it.</param>
// A suggestion is returned as the database holds it. Copying it into a narrower shape dropped the
// localisation keys the game stores a chosen name by, and the copy said nothing the original did
// not.

/// <summary>
/// Invents names the way the game's own randomise buttons do.
/// </summary>
/// <remarks>
/// The game has four of these rather than one shuffle, and they draw on different things: a species
/// comes from a list of ready-made ones, a ruler from the empire's name list, a planet from that
/// list's own planets. Following that shape is what keeps the results consistent — a species picked
/// this way arrives with a homeworld and a name list that suit it.
/// </remarks>
public sealed class NameGenerator(GameDatabase database, Random? random = null)
{
    private readonly GameDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly Random _random = random ?? Random.Shared;

    /// <summary>
    /// Suggests a species, its plural, its homeworld and its home system together.
    /// </summary>
    /// <param name="speciesClass">The class to suit, such as <c>MAM</c>.</param>
    public SpeciesNameSuggestion? Species(string? speciesClass)
    {
        var candidates = _database.SpeciesNames
            .Where(s => speciesClass is null || string.Equals(s.SpeciesClass, speciesClass, StringComparison.Ordinal))
            .ToList();

        // A class the game ships no ready-made species for still gets a name, from anywhere.
        if (candidates.Count == 0)
        {
            candidates = [.. _database.SpeciesNames];
        }

        return Pick(candidates);
    }

    /// <summary>
    /// Invents a ruler's name from a name list.
    /// </summary>
    /// <remarks>
    /// A name is either complete in itself or a first joined to a second, and where a list offers
    /// both the game chooses evenly between them. Gendered lists are used when they have anything in
    /// them, and the ungendered list stands in when they do not.
    /// </remarks>
    public string? Ruler(string? nameList, bool female = false)
    {
        if (Resolve(nameList)?.CharacterNames is not { } names || names.IsEmpty)
        {
            return null;
        }

        var full = names.FullNames.For(female);
        var firsts = names.FirstNames.For(female);
        var seconds = names.SecondNames.For(female);

        var canCombine = firsts.Count > 0;
        var useFull = full.Count > 0 && (!canCombine || _random.Next(2) == 0);

        var gender = female ? "female" : "male";

        if (useFull || !canCombine)
        {
            return LeaderName.Variant(Pick(full), gender) is { Length: > 0 } one ? one : null;
        }

        // A list may give first names and no family names, in which case the first name is the name.
        // Put together rather than set side by side: a family name is as often a frame written round
        // the given one — "$1$ Aburia" — as a word to follow it.
        return seconds.Count > 0
            ? LeaderName.Compose(Pick(firsts), Pick(seconds), gender)
            : LeaderName.Variant(Pick(firsts), gender) is { Length: > 0 } only ? only : null;
    }

    /// <summary>Suggests a planet name from a name list.</summary>
    public string? Planet(string? nameList) => Pick(Resolve(nameList)?.PlanetNames ?? []);

    /// <summary>
    /// Suggests an empire name.
    /// </summary>
    /// <remarks>
    /// The game builds these from a small template language with weighted word lists and
    /// trigger-gated variants. The commonest shape by far is the species adjective followed by a
    /// form of government, which is what this produces; the "Empire of Sol" constructions are not
    /// reproduced.
    /// </remarks>
    public string? Empire(string? speciesName, string? authority) =>
        Pick(EmpireNames(speciesName, authority));

    /// <summary>
    /// Every name the suggestion above would choose between, so a list can offer them all.
    /// </summary>
    /// <remarks>
    /// The same construction, held still: four to six for an authority, which is short enough to
    /// read at a glance rather than scroll. Suggesting is picking one of these, so the two cannot
    /// drift apart.
    /// </remarks>
    public static IReadOnlyList<string> EmpireNames(string? speciesName, string? authority) =>
        speciesName is { Length: > 0 }
            ? [.. SuffixesFor(authority).Select(suffix => $"{Adjective(speciesName)} {suffix}")]
            : [];

    /// <summary>
    /// Turns a species name into an adjective the way the game's naming rules do.
    /// </summary>
    /// <remarks>
    /// The game keeps no adjective alongside a species name; it rewrites the ending, longest match
    /// first, so that Jhabbanid becomes Jhabbanan and Alari becomes Alarian. A name matching nothing
    /// keeps its own form, which is what the game falls back to as well.
    /// </remarks>
    public static string Adjective(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        (string Ending, string Replacement)[] rules =
        [
            ("us", "an"), ("is", "an"), ("es", "an"), ("ss", "an"),
            ("id", "an"), ("ed", "an"), ("ad", "an"), ("od", "an"),
            ("ud", "an"), ("yd", "an"),
            ("i", "ian"), ("r", "ran"), ("a", "an"), ("e", "an"),
        ];

        foreach (var (ending, replacement) in rules)
        {
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(name.AsSpan(0, name.Length - ending.Length), replacement);
            }
        }

        return name;
    }

    /// <summary>
    /// Which name list a species should be named from.
    /// </summary>
    /// <remarks>
    /// A list may point at a different one for this purpose. The three human lists do, so that
    /// randomising a species for the United Nations of Earth offers ordinary human names rather than
    /// that empire's own conventions.
    /// </remarks>
    public string? SpeciesNameSourceFor(string? nameList) =>
        Resolve(nameList) is { RandomNameSource: { Length: > 0 } source } ? source : nameList;

    private NameListDefinition? Resolve(string? key) =>
        key is { Length: > 0 }
            ? _database.NameLists.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal))
            : null;

    /// <summary>
    /// Words the game pairs with a species adjective, chosen to suit the authority.
    /// </summary>
    /// <remarks>
    /// Drawn from the game's own weighted lists, kept short deliberately: a randomise button that
    /// offers a handful of fitting words is more use than one that offers every word the game knows.
    /// </remarks>
    private static string[] SuffixesFor(string? authority) => authority switch
    {
        "auth_imperial" or "auth_dictatorial" =>
            ["Empire", "Imperium", "Hegemony", "Autocracy", "Dominion"],

        "auth_democratic" =>
            ["Republic", "Union", "Commonwealth", "Federation", "Alliance"],

        "auth_oligarchic" =>
            ["Coalition", "Directorate", "Assembly", "Council", "Concord"],

        "auth_corporate" =>
            ["Corporation", "Consortium", "Combine", "Company", "Cartel"],

        "auth_hive_mind" =>
            ["Swarm", "Collective", "Hive", "Brood"],

        "auth_machine_intelligence" =>
            ["Assembly", "Network", "Continuum", "Sequence"],

        _ => ["Empire", "Republic", "Union", "Commonwealth", "Dominion", "Collective"],
    };

    private T? Pick<T>(IReadOnlyList<T> items) =>
        items.Count == 0 ? default : items[_random.Next(items.Count)];
}
