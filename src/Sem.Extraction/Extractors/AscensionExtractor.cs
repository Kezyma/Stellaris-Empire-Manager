using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the ascension perks, and the game's own grouping of them.</summary>
/// <remarks>
/// Both files under <c>common/ascension_perks</c> hold the same kind of thing - the split into
/// "paths" and the rest is how the game organises its own writing, not a difference in the data - so
/// they are read in one sweep and told apart by the category that names them.
/// </remarks>
internal static class AscensionExtractor
{
    /// <summary>Where the game keeps them.</summary>
    private const string Root = "common/ascension_perks";

    /// <summary>And where it says which are the ascension paths.</summary>
    private const string Categories = "common/ascension_perk_categories";

    /// <summary>The category holding the seven that put an empire on a path.</summary>
    private const string PathCategory = "ap_category_ascensions";

    public static List<AscensionPerkDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var grouping = ReadCategories(loader);
        var results = new List<AscensionPerkDefinition>();

        foreach (var entry in loader.LoadDefinitions(Root))
        {
            var body = entry.Body;
            var category = grouping.GetValueOrDefault(entry.Key);

            results.Add(new AscensionPerkDefinition(entry.Key)
            {
                Category = category,
                IsPath = category == PathCategory,

                // Potential decides whether it is listed at all; possible whether it may be taken
                // now, and its failures are sentences the game wrote to be read.
                Potential = requirements.CompileTrigger(body.GetBlock("potential")),
                Possible = requirements.CompileTrigger(body.GetBlock("possible")),

                Effects = EffectsReader.Read(body, loader, requirements),

                // Through the sprite rather than by convention. The picture a perk uses is not
                // always named after the perk - GFX_ap_colossus draws ap_colossus_project.dds - so
                // guessing the file name is right most of the time, which is the worst way to be
                // wrong.
                Icon = assets.RegisterSprite(
                    $"GFX_{entry.Key}",
                    $"icons/ascension_perks/{entry.Key}.png"),
            });
        }

        return results;
    }

    /// <summary>
    /// Which category each perk belongs to, read from the game's own grouping.
    /// </summary>
    /// <remarks>
    /// The categories are how the game knows which seven are the ascension paths, and taking them
    /// from the file rather than from a list written here means a pack that adds a path is a pack
    /// this reads rather than one it has to be told about.
    /// </remarks>
    private static Dictionary<string, string> ReadCategories(ScriptLoader loader)
    {
        var grouping = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in loader.LoadDefinitions(Categories))
        {
            foreach (var perk in entry.Body.GetList("ascension_perks"))
            {
                grouping[perk] = entry.Key;
            }
        }

        return grouping;
    }
}
