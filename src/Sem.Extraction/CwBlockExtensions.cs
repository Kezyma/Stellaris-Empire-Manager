using Sem.Clausewitz;

namespace Sem.Extraction;

/// <summary>Reading helpers shared by the extraction stages.</summary>
internal static class CwBlockExtensions
{
    /// <summary>The first value for a key, or null.</summary>
    public static string? GetString(this CwBlock block, string key) =>
        block.Nodes.FirstOrDefault(n => n.Key == key)?.ScalarValue;

    /// <summary>Every value for a repeated key, in order.</summary>
    public static IReadOnlyList<string> GetStrings(this CwBlock block, string key) =>
        [.. block.Nodes.Where(n => n.Key == key && n.Scalar is not null).Select(n => n.ScalarValue!)];

    /// <summary>The first block for a key, or null.</summary>
    public static CwBlock? GetBlock(this CwBlock block, string key) =>
        block.Nodes.FirstOrDefault(n => n.Key == key)?.Block;

    /// <summary>The unkeyed values inside a list-style block, such as <c>tags = { a b c }</c>.</summary>
    public static IReadOnlyList<string> GetList(this CwBlock block, string key) =>
        block.GetBlock(key) is { } list
            ? [.. list.Nodes.Where(n => !n.IsAssignment && n.Scalar is not null).Select(n => n.ScalarValue!)]
            : [];

    /// <summary>Reads a <c>yes</c>/<c>no</c> field.</summary>
    public static bool GetBool(this CwBlock block, string key, bool defaultValue = false) =>
        block.GetString(key) switch
        {
            "yes" => true,
            "no" => false,
            _ => defaultValue,
        };

    /// <summary>Reads a <c>yes</c>/<c>no</c> field that may be absent.</summary>
    public static bool? GetBoolOrNull(this CwBlock block, string key) =>
        block.GetString(key) switch
        {
            "yes" => true,
            "no" => false,
            _ => null,
        };

    /// <summary>
    /// Reads a modifier block into modifier keys and their values, following <c>@</c> variables.
    /// </summary>
    public static Dictionary<string, double> GetModifiers(
        this CwBlock block,
        string key,
        ScriptLoader loader)
    {
        var modifiers = new Dictionary<string, double>(StringComparer.Ordinal);

        if (block.GetBlock(key) is not { } modifierBlock)
        {
            return modifiers;
        }

        foreach (var node in modifierBlock.Nodes)
        {
            if (node.Key is { } name && loader.ResolveNumber(node.ScalarValue) is { } value)
            {
                modifiers[name] = value;
            }
        }

        return modifiers;
    }

    /// <summary>
    /// Reads a cost, which may be a plain number or a scripted value. At design time the game uses
    /// the base of a scripted value, since there is no empire yet for its conditions to apply to.
    /// </summary>
    public static int GetCost(this CwBlock block, ScriptLoader loader, string key = "cost")
    {
        var node = block.Nodes.FirstOrDefault(n => n.Key == key);

        if (node is null)
        {
            return 0;
        }

        if (node.Scalar is not null)
        {
            return loader.ResolveInt(node.ScalarValue) ?? 0;
        }

        return node.Block is { } scripted ? loader.ResolveInt(scripted.GetString("base")) ?? 0 : 0;
    }

    /// <summary>
    /// Reads a weight, which may be a plain number or a scripted value with a <c>base</c>.
    /// </summary>
    public static double GetWeight(this CwBlock block, ScriptLoader loader, string key = "weight")
    {
        var node = block.Nodes.FirstOrDefault(n => n.Key == key);

        if (node is null)
        {
            return 0;
        }

        if (node.Scalar is not null)
        {
            return loader.ResolveNumber(node.ScalarValue) ?? 0;
        }

        return node.Block is { } scripted ? loader.ResolveNumber(scripted.GetString("base")) ?? 0 : 0;
    }

    /// <summary>
    /// Finds a key anywhere in a nested structure, used for the defines file, which groups its
    /// values under section blocks.
    /// </summary>
    public static string? FindNestedString(this CwBlock block, string key)
    {
        foreach (var node in block.Nodes)
        {
            if (node.Key == key && node.Scalar is not null)
            {
                return node.ScalarValue;
            }

            if (node.Block is { } nested && nested.FindNestedString(key) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
