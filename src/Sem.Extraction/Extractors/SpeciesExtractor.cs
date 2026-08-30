using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads species archetypes and species classes.</summary>
internal static class SpeciesExtractor
{
    /// <summary>
    /// Reads the archetypes and the trait budgets they grant.
    /// </summary>
    /// <remarks>
    /// An archetype may inherit its budget from another, which is how Lithoids get the same
    /// allowance as ordinary biological species, so this resolves in two passes.
    /// </remarks>
    public static List<ArchetypeDefinition> ExtractArchetypes(ScriptLoader loader)
    {
        var raw = new List<(string Key, int Points, int MaxTraits, bool Robotic, string? InheritFrom)>();

        foreach (var entry in loader.LoadDefinitions("common/species_archetypes"))
        {
            raw.Add((
                entry.Key,
                loader.ResolveInt(entry.Body.GetString("species_trait_points")) ?? 0,
                loader.ResolveInt(entry.Body.GetString("species_max_traits")) ?? 0,
                entry.Body.GetBool("robotic"),
                entry.Body.GetString("inherit_trait_points_from")));
        }

        var byKey = raw.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var results = new List<ArchetypeDefinition>();

        foreach (var item in raw)
        {
            var points = item.Points;
            var maxTraits = item.MaxTraits;

            // Follow the inheritance chain, with a bound so a bad file cannot loop forever.
            var source = item.InheritFrom;
            for (var depth = 0; depth < 8 && source is not null; depth++)
            {
                if (!byKey.TryGetValue(source, out var parent))
                {
                    break;
                }

                points = parent.Points;
                maxTraits = parent.MaxTraits;
                source = parent.InheritFrom;
            }

            results.Add(new ArchetypeDefinition(item.Key, points, maxTraits, item.Robotic));
        }

        return results;
    }

    /// <summary>Reads the species classes, including which the player may choose.</summary>
    public static List<SpeciesClassDefinition> ExtractSpeciesClasses(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<SpeciesClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/species_classes"))
        {
            var body = entry.Body;

            // A class with no archetype is not a species: several exist purely to contribute a
            // ship or city appearance, and dropping them would take those appearances with them.
            results.Add(new SpeciesClassDefinition(entry.Key, body.GetString("archetype"))
            {
                // An absent playable block means the class is offered, so default to permitted.
                Playable = body.GetBlock("playable") is { } playable
                    ? requirements.CompileTrigger(playable)
                    : new AlwaysRequirement(true),
                Possible = requirements.CompileRequirementsList(body.GetBlock("possible")),
                PossibleSecondary = requirements.CompileRequirementsList(body.GetBlock("possible_secondary")),
                ForcedTrait = body.GetString("trait"),
                GraphicalCulture = body.GetString("graphical_culture"),
                AddedPlanetClasses = body.GetList("added_planet_types"),
                RemovedPlanetClasses = body.GetList("removed_planet_types"),
            });
        }

        return results;
    }
}
