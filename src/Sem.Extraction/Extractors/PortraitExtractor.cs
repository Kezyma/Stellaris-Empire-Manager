using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the portrait picker's tabs, sets and individual portraits.</summary>
/// <remarks>
/// Choosing a portrait goes through three files. Categories are the tabs, each naming the sets it
/// shows; sets belong to a species class and list portraits with whatever gates them; the portrait
/// definitions themselves supply the skin variants a design stores as an index.
/// </remarks>
internal static class PortraitExtractor
{
    /// <summary>Reads the tabs the portrait picker is divided into.</summary>
    public static List<PortraitCategoryDefinition> ExtractCategories(ScriptLoader loader)
    {
        var results = new List<PortraitCategoryDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/portrait_categories"))
        {
            var name = entry.Body.GetString("name") ?? entry.Key;
            results.Add(new PortraitCategoryDefinition(entry.Key, name, entry.Body.GetList("sets")));
        }

        return results;
    }

    /// <summary>
    /// Reads the portrait sets.
    /// </summary>
    /// <remarks>
    /// Order matters and must be preserved exactly. The game uses conditional groups with no
    /// condition at all purely to arrange the picker, so sorting or removing duplicates here would
    /// rearrange the player's portrait list for no reason.
    /// </remarks>
    public static List<PortraitSetDefinition> ExtractSets(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<PortraitSetDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/portrait_sets"))
        {
            var body = entry.Body;
            var portraits = new List<PortraitEntry>();

            foreach (var node in body.Nodes)
            {
                switch (node.Key)
                {
                    case "portraits" or "non_randomized_portraits" when node.Block is not null:
                        AddAll(node.Block, new AlwaysRequirement(true));
                        break;

                    case "conditional_portraits" when node.Block is not null:
                        // The playable condition decides what the player may choose; the
                        // randomizable one only affects generated empires.
                        AddAll(
                            node.Block.GetBlock("portraits"),
                            requirements.CompileTrigger(node.Block.GetBlock("playable")));
                        break;
                }
            }

            results.Add(new PortraitSetDefinition(entry.Key, body.GetString("species_class"))
            {
                Portraits = portraits,
            });

            void AddAll(CwBlock? list, Requirement condition)
            {
                if (list is null)
                {
                    return;
                }

                foreach (var element in list.Nodes.Where(n => !n.IsAssignment && n.Scalar is not null))
                {
                    portraits.Add(new PortraitEntry(element.ScalarValue!, condition));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Reads the individual portraits and how many skin variants each has.
    /// </summary>
    /// <remarks>
    /// Every portrait in 4.4 is a three-dimensional model rather than an image, so nothing here can
    /// produce a picture. Thumbnails are rendered separately and attached afterwards.
    /// </remarks>
    public static List<PortraitDefinition> ExtractPortraits(ScriptLoader loader)
    {
        var results = new List<PortraitDefinition>();
        var textureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<(string Key, string Default)>();

        foreach (var (_, document) in loader.LoadDirectory("gfx/portraits/portraits"))
        {
            foreach (var node in document.Nodes)
            {
                switch (node.Key)
                {
                    case "portraits" when node.Block is not null:
                        foreach (var portrait in node.Block.Nodes)
                        {
                            if (portrait.Key is not { Length: > 0 } key || portrait.Block is null)
                            {
                                continue;
                            }

                            var textures = CountTextures(portrait.Block);
                            textureCounts[key] = textures;

                            if (seen.Add(key))
                            {
                                results.Add(new PortraitDefinition(key) { TextureCount = textures });
                            }
                        }

                        break;

                    // A group stands in for a portrait wherever one can be named, and picks a
                    // likeness by gender at runtime. The picker offers the group and shows its
                    // default.
                    case "portrait_groups" when node.Block is not null:
                        foreach (var group in node.Block.Nodes)
                        {
                            if (group.Key is { Length: > 0 } key &&
                                group.Block?.GetString("default") is { Length: > 0 } fallback)
                            {
                                groups.Add((key, fallback));
                            }
                        }

                        break;
                }
            }
        }

        foreach (var (key, fallback) in groups)
        {
            if (seen.Add(key))
            {
                results.Add(new PortraitDefinition(key)
                {
                    ResolvesTo = fallback,
                    TextureCount = textureCounts.GetValueOrDefault(fallback),
                });
            }
        }

        return results;
    }

    private static int CountTextures(CwBlock portrait) =>
        portrait.GetBlock("character_textures") is { } textures
            ? textures.Nodes.Count(n => !n.IsAssignment && n.Scalar is not null)
            : 0;
}
