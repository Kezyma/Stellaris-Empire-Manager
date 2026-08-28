using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads planet classes and starting systems.</summary>
internal static class WorldExtractor
{
    /// <summary>
    /// Reads the planet classes, marking those an empire may start on.
    /// </summary>
    /// <remarks>
    /// The starting worlds are those flagged <c>initial</c>. Origins reach further than that, by
    /// naming a starting colony of their own, which is how Void Dwellers begin on a habitat.
    /// </remarks>
    public static List<PlanetClassDefinition> ExtractPlanetClasses(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<PlanetClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/planet_classes"))
        {
            var body = entry.Body;

            results.Add(new PlanetClassDefinition(entry.Key)
            {
                Climate = body.GetString("climate"),
                IsStartingWorld = body.GetBool("initial"),
                Colonizable = body.GetBool("colonizable"),
                Potential = requirements.CompileTrigger(body.GetBlock("potential")),
                Icon = $"icons/planets/{entry.Key}.png",
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the starting systems, keeping only those empire creation can reach.
    /// </summary>
    /// <remarks>
    /// A system marked <c>custom_empire</c> can be chosen for any empire. One marked <c>origin</c>
    /// appears only when the selected origin names it, whatever else it is marked as, so origin
    /// takes precedence.
    /// </remarks>
    public static List<InitializerDefinition> ExtractInitializers(ScriptLoader loader)
    {
        var results = new List<InitializerDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/solar_system_initializers"))
        {
            var usages = entry.Body.GetStrings("usage");

            var usage = usages.Contains("origin")
                ? InitializerUsage.Origin
                : usages.Contains("custom_empire")
                    ? InitializerUsage.CustomEmpire
                    : InitializerUsage.None;

            if (usage != InitializerUsage.None)
            {
                results.Add(new InitializerDefinition(entry.Key, usage));
            }
        }

        return results;
    }
}
