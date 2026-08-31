using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads the generator the game names empires with.
/// </summary>
/// <remarks>
/// <para>
/// This app used to invent these. A table of five or six words per authority, written by hand, so
/// that a reptilian imperium was offered the Rethellian Empire, Imperium, Hegemony, Autocracy or
/// Dominion and nothing else — while the game itself, asked to randomise the same empire, would
/// happily answer "Empire of Pakshalika". Reopening such an empire in the designer showed a list its
/// own name was not in.
/// </para>
/// <para>
/// The real generator is <c>common/random_names/00_empire_names.txt</c>: two hundred and seventy-one
/// shapes and a hundred and ninety-three weighted word lists, each shape gated on the kind of empire
/// it suits. It is long but not complicated, and everything it asks about is something a design
/// knows.
/// </para>
/// <para>
/// Read through <see cref="ScriptLoader.LoadEntries"/> rather than <c>LoadDefinitions</c>, because
/// the file repeats the same two keys at the top level instead of naming each entry — resolving
/// overrides by key would keep one format and one word list out of the whole file.
/// </para>
/// </remarks>
internal static class EmpireNameExtractor
{
    /// <summary>Reads the weighted word lists.</summary>
    public static List<EmpireNamePartsList> ExtractParts(ScriptLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        var results = new List<EmpireNamePartsList>();

        foreach (var entry in loader.LoadEntries("common/random_names")
                     .Where(e => e.Key == "empire_name_parts_list"))
        {
            if (entry.Body.GetString("key") is not { Length: > 0 } key ||
                entry.Body.GetBlock("parts") is not { } parts)
            {
                continue;
            }

            results.Add(new EmpireNamePartsList(key, [.. Words(parts)]));
        }

        return results;
    }

    /// <summary>Reads the shapes, with the condition that decides which empires get each.</summary>
    public static List<EmpireNameFormat> ExtractFormats(ScriptLoader loader, RequirementCompiler requirements)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(requirements);

        var results = new List<EmpireNameFormat>();

        foreach (var entry in loader.LoadEntries("common/random_names")
                     .Where(e => e.Key == "empire_name_format"))
        {
            if (entry.Body.GetString("format") is not { Length: > 0 } format)
            {
                continue;
            }

            results.Add(new EmpireNameFormat(format)
            {
                PrefixFormat = entry.Body.GetString("prefix_format"),
                Noun = entry.Body.GetString("noun"),
                Adjective = entry.Body.GetString("adjective"),
                When = Condition(entry.Body, requirements),
            });
        }

        return results;
    }

    /// <summary>
    /// The words in a list, with their weights.
    /// </summary>
    /// <remarks>
    /// Written as <c>Empire = 4</c>, so the key is the word. A word of more than one syllable is
    /// still one token, but a few are quoted where they contain an apostrophe, and the parser has
    /// already taken the quotes off by the time this reads them.
    /// </remarks>
    private static IEnumerable<EmpireNamePart> Words(CwBlock parts)
    {
        foreach (var node in parts.Nodes)
        {
            if (node.Key is { Length: > 0 } word && int.TryParse(node.ScalarValue, System.Globalization.CultureInfo.InvariantCulture, out var weight))
            {
                yield return new EmpireNamePart(word, weight);
            }
        }
    }

    /// <summary>
    /// When a shape applies.
    /// </summary>
    /// <remarks>
    /// Every one of them is written the same way: a weight of zero, raised by a single modifier
    /// whose conditions are the real question. So the condition is that modifier with its own
    /// <c>add</c> taken out, and a shape whose weight is never raised belongs to nobody.
    /// </remarks>
    private static Requirement Condition(CwBlock body, RequirementCompiler requirements)
    {
        if (body.GetBlock("random_weight")?.GetBlock("modifier") is not { } modifier)
        {
            return new AlwaysRequirement(false);
        }

        var conditions = new CwBlock();

        foreach (var node in modifier.Nodes)
        {
            if (node.Key is not ("add" or "factor" or "mult"))
            {
                conditions.Add(node);
            }
        }

        return requirements.CompileTrigger(conditions);
    }
}
