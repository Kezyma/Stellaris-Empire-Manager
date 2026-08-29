using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads planet classes and starting systems.</summary>
internal static class WorldExtractor
{
    /// <summary>
    /// Reads the planet classes, marking those an empire may start on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A starting world is flagged <c>initial</c> and not flagged <c>starting_planet = no</c>. Both
    /// are needed: a volcanic world and an arkship are each <c>initial</c>, and neither is a world a
    /// player may simply choose — an Infernal species class adds the first and an origin the second.
    /// Reading only the first offered both to everyone.
    /// </para>
    /// <para>
    /// Origins reach further still by naming a starting colony of their own, which is how Void
    /// Dwellers begin on a habitat.
    /// </para>
    /// </remarks>
    public static List<PlanetClassDefinition> ExtractPlanetClasses(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<PlanetClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/planet_classes"))
        {
            var body = entry.Body;

            results.Add(new PlanetClassDefinition(entry.Key)
            {
                Climate = body.GetString("climate"),
                IsStartingWorld = body.GetBool("initial") && body.GetBool("starting_planet", defaultValue: true),
                Colonizable = body.GetBool("colonizable"),
                Potential = requirements.CompileTrigger(body.GetBlock("potential")),

                // Each class names its own picture, and the larger of the two is a frame of a strip
                // rather than a file of its own. Reading the name is also what makes this the round
                // planet the game shows, instead of the square ground texture sharing its key.
                Icon = assets.RegisterSprite(
                           body.GetString("icon_large"), $"icons/planets/{entry.Key}.png")
                       ?? assets.RegisterSprite(
                           body.GetString("icon"), $"icons/planets/{entry.Key}.png"),
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
