using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>The pools of names one name list offers.</summary>
/// <param name="Characters">Names for rulers and other leaders.</param>
/// <param name="Planets">Names for colonies.</param>
/// <param name="Ships">Names for ships.</param>
/// <param name="Fleets">
/// Names for fleets. Armies have no equivalent: the game numbers them from a template rather than
/// naming them from a pool, so there is nothing to read.
/// </param>
public sealed record NamePools(
    NameSet Characters,
    IReadOnlyList<string> Planets,
    IReadOnlyList<string> Ships,
    IReadOnlyList<string> Fleets)
{
    /// <summary>The pattern a list numbers its fleets by, where it names none.</summary>
    public string? FleetPattern { get; init; }
}

/// <summary>
/// Reads the pools of names the game draws on when a player presses randomise.
/// </summary>
/// <remarks>
/// Everything here is stored as the words themselves rather than as localisation keys. There are
/// around ten thousand names and nothing ever needs a name's key once it has the name, so carrying
/// both would double the cost for no gain.
/// </remarks>
public static class NameExtractor
{
    /// <summary>
    /// Reads the ready-made species the game offers, which is what its randomise button uses.
    /// </summary>
    /// <remarks>
    /// One entry supplies a species name, its plural, a homeworld, a home system and a matching name
    /// list together. That is why pressing randomise in the game produces five fields that agree
    /// with each other rather than five unrelated words.
    /// </remarks>
    public static List<SpeciesNameSuggestion> ExtractSpeciesNames(
        ScriptLoader loader,
        IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(text);

        var results = new List<SpeciesNameSuggestion>();

        foreach (var path in loader.Content.EnumerateFiles("common/species_names"))
        {
            if (loader.Load(path) is not { } document)
            {
                continue;
            }

            foreach (var group in document.Nodes)
            {
                if (group.Key is not { Length: > 0 } speciesClass || group.Block is not { } entries)
                {
                    continue;
                }

                foreach (var entry in entries.Nodes)
                {
                    if (entry.Block is not { } body ||
                        Resolve(body.GetString("name"), text) is not { Length: > 0 } name)
                    {
                        continue;
                    }

                    results.Add(new SpeciesNameSuggestion(speciesClass, name)
                    {
                        Plural = Resolve(body.GetString("plural"), text),
                        HomePlanet = Resolve(body.GetString("home_planet"), text),
                        HomeSystem = Resolve(body.GetString("home_system"), text),
                        NameList = body.GetString("name_list"),

                        // Kept beside the resolved text, because a design that takes one of these
                        // stores the key: that is how the game writes it, and how the same empire
                        // reads correctly in another language.
                        NameKey = body.GetString("name"),
                        PluralKey = body.GetString("plural"),
                        HomePlanetKey = body.GetString("home_planet"),
                        HomeSystemKey = body.GetString("home_system"),
                    });
                }
            }
        }

        return results;
    }

    /// <summary>Reads the ruler, planet and ship names a name list offers.</summary>
    public static NamePools ReadNamePools(CwBlock body, IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(text);

        var fleets = body.GetBlock("fleet_names");

        return new NamePools(
            ReadCharacterNames(body, text),
            ReadNames(body.GetBlock("planet_names"), text),
            ReadNames(body.GetBlock("ship_names"), text),
            ReadNames(fleets, text))
        {
            FleetPattern = Sequential(fleets, text),
        };
    }

    /// <summary>
    /// Reads the character names out of every culture a list defines.
    /// </summary>
    /// <remarks>
    /// It used to take the first culture only, on the stated grounds that every list a player can
    /// choose holds just one. That is untrue of the two that matter most: HUMAN1 and HUMAN2 each
    /// hold eleven, weighted — names1 through names11 — with their own gendered first and second
    /// names, so a human empire was being offered about a tenth of what the game has.
    ///
    /// The weights decide which culture the game draws from when it wants one name. A list of
    /// suggestions is not drawing one, so the pools are simply added together.
    /// </remarks>
    private static NameSet ReadCharacterNames(CwBlock body, IReadOnlyDictionary<string, string> text)
    {
        if (body.GetBlock("character_names") is not { } cultures)
        {
            return new NameSet();
        }

        var found = cultures.Nodes
            .Select(n => n.Block)
            .OfType<CwBlock>()
            .ToList();

        if (found.Count == 0)
        {
            return new NameSet();
        }

        return new NameSet
        {
            FullNames = ReadGendered(found, "full_names", text),
            FirstNames = ReadGendered(found, "first_names", text),
            SecondNames = ReadGendered(found, "second_names", text),

            // The names a regnal ruler is drawn from, which sixty-one of the seventy-one lists
            // declare and none of which was ever read.
            RegnalFirstNames = ReadGendered(found, "regnal_first_names", text),
            RegnalSecondNames = ReadGendered(found, "regnal_second_names", text),
        };
    }

    /// <summary>
    /// Reads one kind of name in all three variants a list may offer it.
    /// </summary>
    /// <remarks>
    /// Every kind comes in a plain form and a gendered pair, and which a list uses varies: the human
    /// lists give complete names by gender and no first names at all, while the mammalian ones do
    /// the opposite. Reading only the plain form leaves a dozen lists looking empty when they are
    /// not — including the one a new human empire starts with.
    /// </remarks>
    private static GenderedNames ReadGendered(
        IReadOnlyList<CwBlock> cultures,
        string field,
        IReadOnlyDictionary<string, string> text) =>
        new()
        {
            Any = Gather(cultures, field, text),
            Male = Gather(cultures, $"{field}_male", text),
            Female = Gather(cultures, $"{field}_female", text),
        };

    /// <summary>
    /// One field's names across every culture, in order and without repeats.
    /// </summary>
    /// <remarks>
    /// Distinct because the cultures overlap: several of the human ones share a surname, and a list
    /// offering the same name twice reads as a fault rather than as two cultures agreeing.
    /// </remarks>
    private static IReadOnlyList<string> Gather(
        IReadOnlyList<CwBlock> cultures,
        string field,
        IReadOnlyDictionary<string, string> text) =>
        [
            .. cultures
                .SelectMany(culture => ResolveAll(culture.GetList(field), text))
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    /// Gathers names out of a section that groups them, such as planet names by planet class.
    /// </summary>
    /// <remarks>
    /// The sections nest inconsistently — planet names sit inside a <c>names</c> block within each
    /// group, ship names sit directly in theirs — so this collects every bare value beneath the
    /// section rather than assuming a depth.
    /// </remarks>
    private static List<string> ReadNames(CwBlock? section, IReadOnlyDictionary<string, string> text)
    {
        var keys = new List<string>();

        if (section is not null)
        {
            Collect(section);
        }

        return ResolveAll(keys, text);

        void Collect(CwBlock block)
        {
            foreach (var node in block.Nodes)
            {
                if (!node.IsAssignment && node.Scalar is not null && node.ScalarValue is { Length: > 0 } value)
                {
                    keys.Add(value);
                }
                else if (node.Block is { } nested && node.Key != "sequential_name")
                {
                    Collect(nested);
                }
            }
        }
    }

    private static List<string> ResolveAll(
        IEnumerable<string> keys,
        IReadOnlyDictionary<string, string> text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>();

        foreach (var key in keys)
        {
            if (Resolve(key, text) is { Length: > 0 } name && seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Turns a name's key into the word itself.
    /// </summary>
    /// <remarks>
    /// A handful of names are written literally rather than as keys, so anything with no entry is
    /// kept as it stands — but only when it looks like a word rather than a key that went missing.
    /// </remarks>
    private static string? Resolve(string? key, IReadOnlyDictionary<string, string> text)
    {
        if (key is not { Length: > 0 })
        {
            return null;
        }

        if (text.TryGetValue(key, out var value) && value is { Length: > 0 })
        {
            return value;
        }

        return key.Contains('_') ? null : key;
    }

    /// <summary>
    /// The pattern a list numbers a pool by, rather than naming it.
    /// </summary>
    /// <remarks>
    /// Sixteen lists name no fleets at all and give a template instead — Toxoid 3 carries
    /// <c>sequential_name = TOX3_fleet_names</c>, which reads "Tähtaailaivasto $R$" and comes out as
    /// a numbered series. Reading only the random pool left those lists looking empty when the game
    /// simply counts rather than invents. The <c>$R$</c> is the running number and is left in place;
    /// what fills it is a matter for whoever shows it.
    /// </remarks>
    private static string? Sequential(CwBlock? section, IReadOnlyDictionary<string, string> text)
    {
        if (section?.GetString("sequential_name") is not { Length: > 0 } key)
        {
            return null;
        }

        return text.TryGetValue(key, out var value) && value is { Length: > 0 } ? value : key;
    }
}
