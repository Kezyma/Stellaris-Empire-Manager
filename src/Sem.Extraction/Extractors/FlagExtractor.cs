using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the flag emblems, backgrounds and colours.</summary>
internal static class FlagExtractor
{
    /// <summary>Where the game keeps flag artwork. Not under <c>gfx</c>, despite being images.</summary>
    private const string FlagRoot = "flags";

    /// <summary>Subfolders holding lower-resolution copies of the same emblems.</summary>
    private static readonly string[] SizeVariantFolders = ["map", "small"];

    /// <summary>
    /// Reads the emblem categories and the background category, skipping the ones the game hides.
    /// </summary>
    /// <remarks>
    /// A category may carry a <c>usage.txt</c> turning it off in the designer, which is how the
    /// enclave, pre-FTL and special emblems stay out of the player's list. A category without that
    /// file is shown.
    /// </remarks>
    public static List<FlagCategoryDefinition> ExtractCategories(ScriptLoader loader, AssetCatalog assets)
    {
        var results = new List<FlagCategoryDefinition>();
        var content = loader.Content;

        foreach (var category in EnumerateCategories(content))
        {
            if (!IsShownInDesigner(loader, category))
            {
                continue;
            }

            var isBackground = category == "backgrounds";
            var files = new List<string>();

            foreach (var source in content.EnumerateFiles($"{FlagRoot}/{category}", "*.dds"))
            {
                if (Path.GetFileName(source) is not { Length: > 0 } name)
                {
                    continue;
                }

                // Backgrounds and emblems are shipped as ingredients rather than finished flags:
                // the colours a player picks are applied when the flag is drawn, and pre-baking
                // every combination of seventy-two colours is not worth contemplating.
                assets.Register(source, $"flags/{category}/{Path.GetFileNameWithoutExtension(name)}.png");
                files.Add(name);
            }

            if (files.Count > 0)
            {
                results.Add(new FlagCategoryDefinition(category, isBackground)
                {
                    Files = [.. files.Order(StringComparer.Ordinal)],
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Reads the named colours a flag can be tinted with.
    /// </summary>
    /// <remarks>
    /// Each colour carries three tints, for the flag, the galaxy map and ship trails. Only the
    /// flag tint matters here. The file also holds suggested colour combinations at its top level,
    /// outside the colours block, which are only used when the game invents an empire.
    /// </remarks>
    public static List<FlagColorDefinition> ExtractColors(ScriptLoader loader)
    {
        var results = new List<FlagColorDefinition>();

        var colors = loader.Load($"{FlagRoot}/colors.txt")?.Nodes
            .FirstOrDefault(n => n.Key == "colors")?.Block;

        if (colors is null)
        {
            return results;
        }

        foreach (var node in colors.Nodes)
        {
            if (node.Key is not { Length: > 0 } key || node.Block is null)
            {
                continue;
            }

            if (ReadRgb(node.Block, "flag") is { } rgb)
            {
                results.Add(new FlagColorDefinition(key, rgb.R, rgb.G, rgb.B));
            }
        }

        return results;
    }

    private static IEnumerable<string> EnumerateCategories(LayeredContent content)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in content.Layers)
        {
            if (layer is not DirectoryContentSource directory)
            {
                continue;
            }

            var root = Path.Combine(directory.Root, FlagRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(path);

                // map and small hold duplicates of their parent's emblems at other sizes.
                if (!SizeVariantFolders.Contains(name, StringComparer.OrdinalIgnoreCase) && seen.Add(name))
                {
                    yield return name;
                }
            }
        }
    }

    private static bool IsShownInDesigner(ScriptLoader loader, string category)
    {
        var usage = loader.Load($"{FlagRoot}/{category}/usage.txt");

        if (usage is null)
        {
            return true;
        }

        return usage.Nodes.FirstOrDefault(n => n.Key == "show_in_designer")?.ScalarValue != "no";
    }

    /// <summary>Reads an <c>rgb { r g b }</c> value, whose spacing in the file is inconsistent.</summary>
    private static (byte R, byte G, byte B)? ReadRgb(CwBlock block, string key)
    {
        var node = block.Nodes.FirstOrDefault(n => n.Key == key);

        // Written as "flag = rgb { 58 38 23 }", so rgb is a bare value followed by a block.
        var channels = node?.Block ?? FindRgbBlock(block, key);
        if (channels is null)
        {
            return null;
        }

        var values = channels.Nodes
            .Where(n => !n.IsAssignment && n.Scalar is not null)
            .Select(n => byte.TryParse(n.ScalarValue, out var b) ? b : (byte?)null)
            .ToList();

        return values.Count >= 3 && values[0] is { } r && values[1] is { } g && values[2] is { } b
            ? (r, g, b)
            : null;
    }

    /// <summary>
    /// Finds the block after an <c>rgb</c> marker. The parser sees "flag = rgb" as an assignment
    /// and the braces that follow as a separate unkeyed element.
    /// </summary>
    private static CwBlock? FindRgbBlock(CwBlock block, string key)
    {
        for (var i = 0; i < block.Nodes.Count - 1; i++)
        {
            if (block.Nodes[i].Key == key && block.Nodes[i].ScalarValue == "rgb")
            {
                return block.Nodes[i + 1].Block;
            }
        }

        return null;
    }
}
