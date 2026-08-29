using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

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
                    });
                }
            }
        }

        return results;
    }

    /// <summary>Reads the ruler, planet and ship names a name list offers.</summary>
    public static (NameSet Characters, List<string> Planets, List<string> Ships) ReadNamePools(
        CwBlock body,
        IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(text);

        var characters = ReadCharacterNames(body, text);
        var planets = ReadNames(body.GetBlock("planet_names"), text);
        var ships = ReadNames(body.GetBlock("ship_names"), text);

        return (characters, planets, ships);
    }

    /// <summary>
    /// Reads the character names, taking the first culture a list defines.
    /// </summary>
    /// <remarks>
    /// A list may hold several cultures with weights, but every list the player can choose has only
    /// the one, so the weighting is not worth reproducing to pick between a single option.
    /// </remarks>
    private static NameSet ReadCharacterNames(CwBlock body, IReadOnlyDictionary<string, string> text)
    {
        if (body.GetBlock("character_names") is not { } cultures)
        {
            return new NameSet();
        }

        var culture = cultures.GetBlock("default")
                      ?? cultures.Nodes.FirstOrDefault(n => n.Block is not null)?.Block;

        if (culture is null)
        {
            return new NameSet();
        }

        return new NameSet
        {
            FullNames = ResolveAll(culture.GetList("full_names"), text),
            FirstNames = ResolveAll(culture.GetList("first_names"), text),
            FirstNamesMale = ResolveAll(culture.GetList("first_names_male"), text),
            FirstNamesFemale = ResolveAll(culture.GetList("first_names_female"), text),
            SecondNames = ResolveAll(culture.GetList("second_names"), text),
        };
    }

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
}
