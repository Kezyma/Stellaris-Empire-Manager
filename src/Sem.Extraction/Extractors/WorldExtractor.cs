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
    /// <summary>The one block in the planet-class folder that does not declare a planet class.</summary>
    private const string RandomListBlock = "random_list";

    public static List<PlanetClassDefinition> ExtractPlanetClasses(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<PlanetClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/planet_classes"))
        {
            var body = entry.Body;

            // The folder holds one kind of block that is not a planet class: nine random_list
            // entries naming groups of worlds for the galaxy generator. Read as classes they
            // collapsed into a single phantom, since they all share the key, and it sat in the
            // shipped data being counted as a world nobody could ever start on.
            if (entry.Key == RandomListBlock)
            {
                continue;
            }

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

                Sky = loader.Content.Contains($"gfx/portraits/environments/{entry.Key}_sky.dds")
                    ? assets.Register(
                        $"gfx/portraits/environments/{entry.Key}_sky.dds",
                        $"worlds/{entry.Key}_sky.png",
                        maxDimension: 800)
                    : null,

                Scenery = Scenery(entry.Key, loader, assets),
            });
        }

        return results;
    }

    /// <summary>
    /// How many bands of landscape a world can have in front of its sky.
    /// </summary>
    /// <remarks>
    /// Four, as the portrait layout describes: the game paints them from the horizon forwards with
    /// the empire's own city between them.
    /// </remarks>
    private const int SceneryBands = 4;

    /// <summary>The landscape of one world, furthest band first.</summary>
    private static IReadOnlyList<SceneryBand> Scenery(string key, ScriptLoader loader, AssetCatalog assets)
    {
        var layers = new List<SceneryBand>();

        for (var band = 1; band <= SceneryBands; band++)
        {
            var source = $"gfx/portraits/environments/{key}_l{band:00}.dds";

            // Not every world has every band — an arctic world has no second one — and a gap is
            // not a fault. Asking for a picture that is not there is what fills the missing-image
            // report with noise, so it is not asked for. The band each one is keeps the gap from
            // shifting the rest forward when the city is interleaved with them.
            if (loader.Content.Contains(source) &&
                assets.Register(source, $"worlds/{key}_l{band:00}.png", maxDimension: 800) is { } image)
            {
                layers.Add(new SceneryBand(band, image));
            }
        }

        return layers;
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
