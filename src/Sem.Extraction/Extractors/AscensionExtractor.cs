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

    /// <summary>Where the tradition trees are, which is not where the traditions are.</summary>
    private const string Trees = "common/tradition_categories";

    /// <summary>And where the traditions themselves are.</summary>
    private const string Traditions = "common/traditions";

    /// <summary>The category holding the seven that put an empire on a path.</summary>
    private const string PathCategory = "ap_category_ascensions";

    public static List<AscensionPerkDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var grouping = ReadCategories(loader);
        var results = new List<AscensionPerkDefinition>();

        foreach (var entry in loader.LoadDefinitions(Root))
        {
            var body = entry.Body;
            var category = grouping.GetValueOrDefault(entry.Key);

            results.Add(new AscensionPerkDefinition(entry.Key)
            {
                Category = category,

                // Potential decides whether it is listed at all; possible whether it may be taken
                // now, and its failures are sentences the game wrote to be read.
                Potential = requirements.CompilePlanTrigger(body.GetBlock("potential")),
                Possible = requirements.CompilePlanTrigger(body.GetBlock("possible")),

                Effects = EffectsReader.Read(
                    body, loader, requirements, readsScriptedUnlocks: true),

                // Six perks are shown under another name to the empire that triggers the swap, and
                // a perk's description hangs off its name, so the renamed one carries both.
                Variants = EffectsReader.ReadVariants(
                    body, "tradition_swap", requirements, text, name => $"{name}_desc"),

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
    /// Reads the tradition trees, which is the level a plan is made at.
    /// </summary>
    /// <remarks>
    /// The trees and not the traditions inside them. Five picks within each of thirty-two trees is a
    /// level nobody plans at, it is two hundred and thirty-four more records and twice that many
    /// pieces of text, and the tree's own description already says what the tree is for.
    /// </remarks>
    public static List<TraditionTreeDefinition> ExtractTrees(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<TraditionTreeDefinition>();

        foreach (var entry in loader.LoadDefinitions(Trees))
        {
            var body = entry.Body;
            var potential = requirements.CompilePlanTrigger(body.GetBlock("potential"));

            // One of them exists only so the game has something to hang a tooltip on, and says so
            // with "always = no". Read the mark rather than matching its name, so a second one would
            // be dropped too.
            if (potential is AlwaysRequirement { Value: false })
            {
                continue;
            }

            results.Add(new TraditionTreeDefinition(entry.Key)
            {
                Potential = potential,
                AdoptionBonus = body.GetString("adoption_bonus"),
                FinishBonus = body.GetString("finish_bonus"),
                Traditions = body.GetList("traditions"),

                Icon = assets.RegisterSprite(
                    $"GFX_tradition_category_icon_{entry.Key}",
                    $"icons/traditions/{entry.Key}.png",
                    maxDimension: 64),
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the traditions inside the trees, which is what a tree is worth taking for.
    /// </summary>
    /// <remarks>
    /// Only the modifiers the game states plainly. A tradition also carries a tradition_swap - the
    /// same tradition for an empire of another shape - and those restate the numbers rather than
    /// adding to them, so both would double every one. The reader ignores a key it does not know,
    /// which here is the right thing rather than a gap: the swap says the same as the base block for
    /// a differently-shaped empire, and this app has no such empire to show it to.
    ///
    /// What a tradition unlocks beyond a modifier - an agenda, a building, an edict - is written as
    /// script that runs when it is taken, and is described in the game's own words in the entry
    /// keyed on the tradition plus _delayed. That prose is kept and shown; guessing at the script
    /// would be inventing a second, worse description of something already written.
    /// </remarks>
    public static List<TraditionDefinition> ExtractTraditions(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        IReadOnlyList<TraditionTreeDefinition> trees,
        IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentNullException.ThrowIfNull(text);

        var owner = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            foreach (var key in tree.Traditions)
            {
                owner[key] = tree.Key;
            }

            if (tree.AdoptionBonus is { } adopt)
            {
                owner[adopt] = tree.Key;
            }

            if (tree.FinishBonus is { } finish)
            {
                owner[finish] = tree.Key;
            }
        }

        var results = new List<TraditionDefinition>();

        foreach (var entry in loader.LoadDefinitions(Traditions))
        {
            results.Add(new TraditionDefinition(entry.Key)
            {
                Tree = owner.GetValueOrDefault(entry.Key),
                Effects = EffectsReader.Read(
                    entry.Body, loader, requirements, readsScriptedUnlocks: true),

                // Compiled as a plan's, because that is what asks: the perk it wants is one the plan
                // names, and the technology beside it is something the empire will have by then. As
                // an adoption gate too, which is what lets the six trees that name a flag instead of
                // a perk be read as the perk.
                Possible = requirements.CompileAdoptionGate(entry.Body.GetBlock("possible")),

                DescriptionKey = Described(entry.Key, text),

                // A swap renames the tradition for the empire it belongs to, and its description
                // follows the renamed key rather than the original's.
                Variants = EffectsReader.ReadVariants(
                    entry.Body, "tradition_swap", requirements, text, name => Described(name, text)),

                Icon = assets.RegisterSprite(
                    $"GFX_{entry.Key}",
                    $"icons/traditions/{entry.Key}.png",
                    maxDimension: 48),
            });
        }

        return results;
    }

    /// <summary>
    /// Which of the game's two conventions this tradition's description is written under, if either.
    /// </summary>
    /// <remarks>
    /// <c>_delayed</c> first, because where both exist that is the one the game shows in the tree -
    /// it is the "you will get" wording, which is what a tradition not yet taken is. Sixty-one
    /// traditions have both, a hundred have only the delayed form, nineteen have only the plain one,
    /// and fifty-four have neither and are given nothing rather than their own key tidied up.
    /// </remarks>
    private static string? Described(string key, IReadOnlyDictionary<string, string> text) =>
        text.ContainsKey($"{key}_delayed") ? $"{key}_delayed"
        : text.ContainsKey($"{key}_desc") ? $"{key}_desc"
        : null;

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
