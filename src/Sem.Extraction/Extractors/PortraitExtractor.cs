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
        var attachmentLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<(
            string Key,
            string Default,
            Dictionary<string, string> Members,
            Dictionary<string, IReadOnlyList<string>> Phenotypes)>();

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

                            // What this likeness wears on its head is not always an attachment: the
                            // game lets a portrait rename the control, and calls it a hairstyle for
                            // a human and a hat for a reptilian.
                            if (portrait.Block.GetString("custom_attachment_label") is { Length: > 0 } label)
                            {
                                attachmentLabels[key] = label;
                            }

                            if (seen.Add(key))
                            {
                                results.Add(new PortraitDefinition(key)
                                {
                                    TextureCount = textures,
                                    AttachmentLabelKey = attachmentLabels.GetValueOrDefault(key),
                                });
                            }
                        }

                        break;

                    // A group stands in for a portrait wherever one can be named, and picks a
                    // likeness by gender at runtime. The picker offers the group and shows its
                    // default.
                    case "portrait_groups" when node.Block is not null:
                        foreach (var group in node.Block.Nodes)
                        {
                            if (group.Key is { Length: > 0 } key && group.Block is { } body &&
                                body.GetString("default") is { Length: > 0 } fallback)
                            {
                                groups.Add((key, fallback, ReadMembers(body), ReadPhenotypes(body)));
                            }
                        }

                        break;
                }
            }
        }

        foreach (var (key, fallback, members, phenotypes) in groups)
        {
            if (seen.Add(key))
            {
                results.Add(new PortraitDefinition(key)
                {
                    ResolvesTo = fallback,
                    Members = members,
                    Phenotypes = phenotypes,
                    TextureCount = textureCounts.GetValueOrDefault(fallback),

                    // A group is what a design stores, and it is the group's control the player
                    // uses, so it takes the word its default likeness uses.
                    AttachmentLabelKey = attachmentLabels.GetValueOrDefault(fallback),
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Which likeness a group shows for each gender.
    /// </summary>
    /// <remarks>
    /// Written as a list of additions, each gated on a trigger, under the scope for the situation
    /// being asked about — and one of those scopes is the empire designer, which the game names
    /// <c>game_setup</c> and comments as running with a species and a government but no country. A
    /// portrait offered for more than one gender, as these are for the indeterminate case, is left
    /// to whichever claims it first, since either answer is one the game would give.
    /// </remarks>
    private static Dictionary<string, string> ReadMembers(CwBlock group)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);

        if (group.GetBlock("game_setup") is not { } scope)
        {
            return members;
        }

        foreach (var addition in scope.Nodes)
        {
            if (addition.Key != "add" || addition.Block is not { } body ||
                body.GetBlock("portraits")?.Nodes.FirstOrDefault(n => n.Scalar is not null)?.ScalarValue
                    is not { Length: > 0 } portrait)
            {
                continue;
            }

            foreach (var gender in Genders(body.GetBlock("trigger")))
            {
                members.TryAdd(gender, portrait);
            }
        }

        return members;
    }

    /// <summary>
    /// Every likeness a group offers for each gender, rather than only the first.
    /// </summary>
    /// <remarks>
    /// The same <c>game_setup</c> block read whole. Each addition names a run of portraits and the
    /// genders it is for, and the human group's two runs are five faces each — which is what the
    /// game's own appearance control steps through, and what a design naming
    /// <c>human_female_05</c> is pointing into.
    /// </remarks>
    private static Dictionary<string, IReadOnlyList<string>> ReadPhenotypes(CwBlock group)
    {
        var phenotypes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (group.GetBlock("game_setup") is not { } scope)
        {
            return [];
        }

        foreach (var addition in scope.Nodes)
        {
            if (addition.Key != "add" || addition.Block is not { } body)
            {
                continue;
            }

            var portraits = body.GetList("portraits");

            if (portraits.Count == 0)
            {
                continue;
            }

            foreach (var gender in Genders(body.GetBlock("trigger")))
            {
                // A face offered to more than one gender — the indeterminate case takes both runs —
                // belongs in each of their lists, and only once in each.
                var faces = phenotypes.TryGetValue(gender, out var existing) ? existing : phenotypes[gender] = [];

                faces.AddRange(portraits.Where(p => !faces.Contains(p, StringComparer.Ordinal)));
            }
        }

        return phenotypes.ToDictionary(
            p => p.Key,
            p => (IReadOnlyList<string>)p.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The genders a trigger accepts.
    /// </summary>
    /// <remarks>
    /// The condition is nested — a ruler scope holding an <c>OR</c> of gender comparisons — and the
    /// only part of it that matters here is which genders are named, so the tree is searched for
    /// them rather than compiled. Compiling it would ask the rules engine about a ruler that does
    /// not exist yet.
    /// </remarks>
    private static IEnumerable<string> Genders(CwBlock? trigger)
    {
        if (trigger is null)
        {
            yield break;
        }

        foreach (var node in trigger.Nodes)
        {
            if (node.Key == "gender" && node.ScalarValue is { Length: > 0 } gender)
            {
                yield return gender;
            }

            foreach (var nested in Genders(node.Block))
            {
                yield return nested;
            }
        }
    }

    private static int CountTextures(CwBlock portrait) =>
        portrait.GetBlock("character_textures") is { } textures
            ? textures.Nodes.Count(n => !n.IsAssignment && n.Scalar is not null)
            : 0;
}
