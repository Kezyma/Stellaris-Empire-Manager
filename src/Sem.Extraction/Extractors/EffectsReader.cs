using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads what an option does, in the terms the game itself would describe it.
/// </summary>
/// <remarks>
/// Every kind of option — ethic, trait, civic, origin, authority — states its effects the same few
/// ways, so they are read in one place. The subtlety is that a hand-written tooltip can stand in
/// place of the automatic list of numbers, and an option that both declares one and carries
/// modifiers would otherwise have its numbers shown twice.
/// </remarks>
public static class EffectsReader
{
    /// <summary>
    /// Conditional modifier blocks the game shows in a tooltip.
    /// </summary>
    /// <remarks>
    /// Not every triggered block is displayed. The game's own trait documentation lists which are
    /// and which are not, the unlisted ones being expected to describe themselves through
    /// <c>custom_tooltip_with_modifiers</c> instead. Showing those too would put numbers on screen
    /// that the game never claims.
    /// </remarks>
    private static readonly string[] ShownTriggeredBlocks =
    [
        "triggered_country_modifier",
        "triggered_species_modifier",
    ];

    /// <summary>Fields inside a modifier block that are instructions rather than modifiers.</summary>
    private static readonly HashSet<string> NotModifiers =
        new(StringComparer.Ordinal) { "potential", "custom_tooltip", "show_only_custom_tooltip", "desc" };

    /// <summary>
    /// Reads an option's effects.
    /// </summary>
    /// <param name="body">The option's definition.</param>
    /// <param name="loader">Used to resolve <c>@</c> variables in modifier values.</param>
    /// <param name="requirements">Used to compile the conditions on conditional modifiers.</param>
    /// <param name="tagsKey">
    /// The field holding localisation keys for capabilities, which differs by option: ethics call it
    /// <c>tags</c>, traits <c>localized_tags</c>. A trait's own <c>tags</c> field is a grouping
    /// mechanism with no display text, so it must not be read here.
    /// </param>
    public static EffectSet Read(
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements,
        string? tagsKey = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        var modifiers = new Dictionary<string, double>(StringComparer.Ordinal);
        var conditional = new List<ConditionalEffects>();

        string? tooltip = null;
        var tooltipReplaces = false;

        foreach (var node in body.Nodes)
        {
            if (node.Key is not { } key || node.Block is not { } block)
            {
                continue;
            }

            if (IsAlwaysOnModifierBlock(key))
            {
                Accumulate(modifiers, block, loader);

                // A tooltip declared inside a modifier block stands in for the whole list, unless
                // the block explicitly asks for both.
                if (block.GetString("custom_tooltip") is { Length: > 0 } inner)
                {
                    tooltip = inner;
                    tooltipReplaces = block.GetBool("show_only_custom_tooltip", defaultValue: true);
                }
            }
            else if (ShownTriggeredBlocks.Contains(key, StringComparer.Ordinal))
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                Accumulate(values, block, loader);

                if (values.Count > 0)
                {
                    conditional.Add(new ConditionalEffects(
                        requirements.CompileEffectCondition(block.GetBlock("potential")),
                        values));
                }
            }
            else if (key == "swap_type")
            {
                // A swap replaces parts of the option when its trigger holds, which for effects
                // purposes is the same shape as a conditional modifier.
                var values = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var inner in block.Nodes)
                {
                    if (inner.Key is { } innerKey && inner.Block is { } innerBlock &&
                        IsAlwaysOnModifierBlock(innerKey))
                    {
                        Accumulate(values, innerBlock, loader);
                    }
                }

                if (values.Count > 0)
                {
                    conditional.Add(new ConditionalEffects(
                        requirements.CompileEffectCondition(block.GetBlock("trigger")),
                        values));
                }
            }
        }

        // A tooltip at the top level adds to the list rather than replacing it, which is the whole
        // difference between the two fields the game provides.
        if (body.GetString("custom_tooltip_with_modifiers") is { Length: > 0 } appended)
        {
            tooltip = appended;
            tooltipReplaces = false;
        }

        return new EffectSet
        {
            Modifiers = modifiers,
            Conditional = conditional,
            TagKeys = tagsKey is { Length: > 0 } ? body.GetList(tagsKey) : [],
            DescriptionKey = body.GetString("description"),
            PenaltyKey = body.GetString("negative_description"),
            TooltipKey = tooltip,
            TooltipReplacesModifiers = tooltipReplaces,
            HideModifiers = body.GetBool("hide_modifiers"),
        };
    }

    /// <summary>
    /// Whether a field holds modifiers that always apply.
    /// </summary>
    /// <remarks>
    /// Matched by shape rather than by a list of names, because the game has a family of these —
    /// <c>modifier</c>, <c>country_modifier</c>, <c>species_modifier</c> — and adds to it between
    /// versions.
    /// </remarks>
    private static bool IsAlwaysOnModifierBlock(string key) =>
        !key.StartsWith("triggered_", StringComparison.Ordinal) &&
        (key == "modifier" || key.EndsWith("_modifier", StringComparison.Ordinal));

    /// <summary>
    /// Adds a block's modifiers to a running total.
    /// </summary>
    /// <remarks>
    /// Summed rather than overwritten: an option may state the same modifier in more than one block,
    /// and two half-sized bonuses are one whole one, not the second of them.
    /// </remarks>
    private static void Accumulate(
        Dictionary<string, double> into,
        CwBlock block,
        ScriptLoader loader)
    {
        foreach (var node in block.Nodes)
        {
            if (node.Key is not { } key || node.Scalar is null || NotModifiers.Contains(key))
            {
                continue;
            }

            if (loader.ResolveNumber(node.ScalarValue) is { } value)
            {
                into[key] = into.GetValueOrDefault(key) + value;
            }
        }
    }
}
