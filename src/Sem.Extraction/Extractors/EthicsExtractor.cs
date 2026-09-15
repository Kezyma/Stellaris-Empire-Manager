using Sem.Clausewitz;
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
    /// <param name="loader">The script loader.</param>
    /// <param name="requirements">Used to compile the conditions on conditional modifiers.</param>
    /// <param name="assets">Where the icons are registered.</param>
    /// <param name="text">The game's localisation, read to tell a drift toward from a drift away.</param>
    /// <param name="detail">Filled in with what a page about the ethics needs.</param>
    /// <returns>The ethics.</returns>
    public static List<EthicDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        IReadOnlyDictionary<string, string> text,
        List<EthicDetail> detail)
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

            detail.Add(new EthicDetail(entry.Key)
            {
                DriftsInto = body.GetBool("use_for_pops", defaultValue: true),
                Drift = Drift(body, text),
            });
        }

        return results;
    }

    /// <summary>
    /// Every sentence the game writes about a pop leaning toward this ethic, or away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hundred and thirty-one across the eight ordinary ethics, each one already written for a
    /// player and shown in the game's own ethics-drift tooltip: "Empire is at war", "Pop has trait
    /// Weak", "Empire has Inward Perfection Civic". The trigger beside each is the same statement
    /// in script and is deliberately left where it is - see <see cref="EthicDrift"/>.
    /// </para>
    /// <para>
    /// Which way it pulls comes from the sentence rather than from the key. The game writes the
    /// green ones with a plus and the red ones with a minus and is completely consistent about it,
    /// where the keys are not: only a hundred and thirteen of the hundred and thirty-one are named
    /// <c>_POS</c> or <c>_NEG</c>, and the rest are named after whatever they ask about.
    /// </para>
    /// </remarks>
    /// <param name="body">The ethic's definition.</param>
    /// <param name="text">The game's localisation.</param>
    /// <returns>The sentences, in the order the game lists them.</returns>
    private static List<EthicDrift> Drift(CwBlock body, IReadOnlyDictionary<string, string> text)
    {
        var drift = new List<EthicDrift>();

        foreach (var node in body.Nodes)
        {
            if (node.Key != "pop_attraction_tag" || node.Block is null)
            {
                continue;
            }

            if (node.Block.GetString("desc") is { Length: > 0 } key)
            {
                drift.Add(new EthicDrift(key, !Discourages(text.GetValueOrDefault(key))));
            }
        }

        return drift;
    }

    /// <summary>Whether a sentence is one of the red ones.</summary>
    /// <param name="said">The sentence, as the game wrote it.</param>
    /// <returns>Whether it pushes pops away rather than drawing them.</returns>
    private static bool Discourages(string? said) =>
        said is { Length: > 1 } && said[0] == '\u00A7' && said[1] == 'R';
}
