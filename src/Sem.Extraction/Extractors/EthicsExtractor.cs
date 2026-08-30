using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the ethics.</summary>
internal static class EthicsExtractor
{
    /// <summary>The gestalt ethic, which the game special-cases by key.</summary>
    private const string GestaltKey = "ethic_gestalt_consciousness";

    /// <summary>
    /// Reads the ethics and the categories that make opposing pairs mutually exclusive.
    /// </summary>
    /// <remarks>
    /// Whether an ethic is fanatic is not a flag in the files. The game marks it by omission: an
    /// ethic that has no fanatic form already is one.
    /// </remarks>
    public static List<EthicDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<EthicDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/ethics"))
        {
            var body = entry.Body;
            var category = body.GetString("category");

            if (category is null)
            {
                continue;
            }

            results.Add(new EthicDefinition(entry.Key, body.GetCost(loader), category)
            {
                CategoryValue = loader.ResolveInt(body.GetString("category_value")) ?? 0,
                FanaticVariant = body.GetString("fanatic_variant"),
                RegularVariant = body.GetString("regular_variant"),
                IsGestalt = entry.Key == GestaltKey,

                // An ethic's tags are capability sentences, unlike a trait's, which are grouping
                // labels with no text of their own.
                Effects = EffectsReader.Read(body, loader, requirements, tagsKey: "tags"),

                Icon = assets.Register(
                    $"gfx/interface/icons/ethics/{entry.Key}.dds",
                    $"icons/ethics/{entry.Key}.png"),
            });
        }

        return results;
    }
}
