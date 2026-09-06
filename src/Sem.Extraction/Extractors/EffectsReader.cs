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

    /// <summary>
    /// The triggered block the game shows for everything except a trait.
    /// </summary>
    /// <remarks>
    /// Only traits and the ascension content write one - forty-five occurrences among the traits,
    /// twenty among the perks and traditions, and none at all in civics, ethics or governments - so
    /// which of the two rules applies to it decides everything and nothing else is touched.
    /// </remarks>
    private const string PlainTriggeredBlock = "triggered_modifier";

    /// <summary>The swap a tradition or an ascension perk writes, which is not the civics' one.</summary>
    private const string TraditionSwap = "tradition_swap";

    /// <summary>Fields inside a modifier block that are instructions rather than modifiers.</summary>
    private static readonly HashSet<string> NotModifiers =
        new(StringComparer.Ordinal) { "potential", "custom_tooltip", "show_only_custom_tooltip", "desc" };

    /// <summary>
    /// <c>advanced_authority_swap</c> is deliberately not read.
    /// </summary>
    /// <remarks>
    /// The hive mind and machine intelligence authorities each declare several, and every one is
    /// gated on a country flag an event sets — Memory Aggregator, and its siblings — so none of them
    /// applies to an empire being designed. Reading them would show a player bonuses their empire
    /// does not have, including a trait pick. Left out by decision rather than by oversight, which
    /// is what this note is for.
    /// </remarks>
    private const string MidGameAuthoritySwap = "advanced_authority_swap";

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
    /// <param name="hidesTriggeredBlocks">
    /// Whether the plain <c>triggered_modifier</c> is a block the game keeps to itself.
    /// </param>
    /// <remarks>
    /// True only for traits, whose own documentation says which triggered blocks are displayed and
    /// expects the rest to describe themselves in a tooltip. That rule used to be applied to
    /// everything, and it is a rule about traits: an ascension perk's plain triggered_modifier is
    /// shown in game, and reading it as hidden left fifteen perks with an empty effects list -
    /// Interstellar Dominion among them, whose whole effect is three of these blocks and no
    /// always-on one at all. Traits are the only other thing in the game that writes one.
    /// </remarks>
    public static EffectSet Read(
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements,
        string? tagsKey = null,
        bool hidesTriggeredBlocks = false,
        bool readsScriptedUnlocks = false)
    {
        ArgumentNullException.ThrowIfNull(body);

        var modifiers = new Dictionary<string, double>(StringComparer.Ordinal);
        var conditional = new List<ConditionalEffects>();
        var unlocks = new List<string>();

        string? tooltip = null;
        var tooltipReplaces = false;

        // Gathered before the walk, because the base modifiers depend on them: a tradition replaced
        // by a swap does not give what it says at the top, so what it says at the top holds only
        // while no swap has taken over.
        var replacements = Replacements(body, loader, requirements);

        foreach (var node in body.Nodes)
        {
            if (node.Key is not { } key || node.Block is not { } block ||
                string.Equals(key, MidGameAuthoritySwap, StringComparison.Ordinal))
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
            else if (ShownTriggeredBlocks.Contains(key, StringComparer.Ordinal) ||
                     (key == PlainTriggeredBlock && !hidesTriggeredBlocks))
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

            // What the option does that is script rather than a number, named by the sentence the
            // game wrote for it. Nihilistic Acquisition's whole effect is one of these, and without
            // it the perk showed a name and nothing else.
            else if (key == "on_enabled" && readsScriptedUnlocks)
            {
                CollectUnlocks(block, unlocks);
            }
            else if (key == "swap_type")
            {
                // A swap replaces parts of the option when its trigger holds, which for effects
                // purposes is the same shape as a conditional modifier.
                //
                // Not exactly the same shape: a replacement that restated a number the base block
                // also gives would be added to it rather than standing in for it. No option in the
                // game does — of 1,510 read, not one states a modifier both always and inside a
                // swap — and a test says so, which is cheaper and more honest than modelling a
                // replacement nothing needs. Forty civics carry both a base block and a swap; they
                // simply never overlap.
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

        // The bare form of the same field, which stands in for the numbers instead of joining them.
        //
        // This is the other half of not reading the triggered blocks above. The game hides those on
        // purpose and its own trait documentation says to describe them here instead — so having
        // correctly declined to print numbers the game never claims, we then dropped the sentence it
        // wrote in their place, and the option came out with nothing to say. Existential Iteroparity
        // was the plain case: no modifier block at all, its whole effect in one line of text.
        else if (body.GetString("custom_tooltip") is { Length: > 0 } instead)
        {
            tooltip = instead;
            tooltipReplaces = true;
        }

        // The swaps, and what they do to the base.
        //
        // A swap replaces the option when its trigger holds - the game's own README says so - so the
        // numbers at the top are the numbers for an empire no swap has claimed. Prosperity is the
        // plain case: station output normally, and to a nomadic empire three entirely different
        // modifiers instead. Read as always-on, a nomad was shown the one it does not get and none
        // of the three it does.
        //
        // Only the swaps that bring their own effects narrow it. One with inherit_effects = yes uses
        // the base's modifiers, so it changes nothing about when they apply.
        if (replacements.Count > 0)
        {
            conditional.AddRange(replacements.Select(r => r.Effects));

            if (modifiers.Count > 0)
            {
                conditional.Add(new ConditionalEffects(
                    new NotRequirement(new AnyRequirement([.. replacements.Select(r => r.When)])),
                    modifiers));

                modifiers = new Dictionary<string, double>(StringComparer.Ordinal);
            }
        }

        return new EffectSet
        {
            Modifiers = modifiers,
            Conditional = conditional,

            // The scripted unlocks join whatever the option already names, both being sentences
            // rather than numbers, which is what this field is for.
            TagKeys = [.. (tagsKey is { Length: > 0 } ? body.GetList(tagsKey) : []).Concat(unlocks)],
            DescriptionKey = body.GetString("description"),
            PenaltyKey = body.GetString("negative_description"),
            TooltipKey = tooltip,
            TooltipReplacesModifiers = tooltipReplaces,
            HideModifiers = body.GetBool("hide_modifiers"),
        };
    }

    /// <summary>
    /// The wordings an option's swaps put in place of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A swap changes what the option is called and what is said about it as well as what it does,
    /// and only the doing was being read. The game writes this two ways for two kinds of option -
    /// a tradition's <c>tradition_swap</c> names a key and the description follows the same
    /// convention as any tradition's, while a civic's <c>swap_type</c> states both outright - so
    /// the description is taken where it is given and worked out from the new name otherwise.
    /// </para>
    /// <para>
    /// <c>inherit_name = yes</c> keeps the option's own, which eighty-one traditions say and is why
    /// a swap without a name is not simply one that forgot to give one.
    /// </para>
    /// </remarks>
    public static List<OptionVariant> ReadVariants(
        CwBlock body,
        string swapKey,
        RequirementCompiler requirements,
        IReadOnlyDictionary<string, string> text,
        Func<string, string?>? describes = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<OptionVariant>();

        foreach (var node in body.Nodes)
        {
            if (node.Key != swapKey || node.Block is not { } swap)
            {
                continue;
            }

            var name = swap.GetBool("inherit_name") ? null : swap.GetString("name");

            // A swap can name a key the game never wrote - three of them do, among them the machine
            // form of the Synthetics completion bonus. The game shows such a swap under the
            // option's own name, and so does this: keeping the key would put it on screen with its
            // underscores taken out, which is worse than the name it replaced.
            if (name is { Length: > 0 } && !text.ContainsKey(name))
            {
                name = null;
            }

            var described = swap.GetString("description")
                ?? (name is { Length: > 0 } && describes is not null ? describes(name) : null);

            if (name is not { Length: > 0 } && described is not { Length: > 0 })
            {
                continue;
            }

            // Through the effect-condition compiler, because that is what this is: a condition on
            // what to show rather than on what may be chosen. Four of these ask about a federation,
            // which is neither a rule a design breaks nor one it meets.
            found.Add(new OptionVariant(
                requirements.CompileEffectCondition(swap.GetBlock("trigger")),
                name,
                described));
        }

        return found;
    }

    /// <summary>
    /// The swaps that bring modifiers of their own, each with the condition that selects it.
    /// </summary>
    /// <remarks>
    /// <c>inherit_effects = yes</c> means the option's own modifiers are used instead of the swap's,
    /// so such a swap contributes nothing here and does not narrow the base either. The game's
    /// README gives the default as no, which is why the field's absence counts as a replacement.
    /// </remarks>
    private static List<(Requirement When, ConditionalEffects Effects)> Replacements(
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var found = new List<(Requirement, ConditionalEffects)>();

        foreach (var node in body.Nodes)
        {
            if (node.Key != TraditionSwap || node.Block is not { } swap ||
                swap.GetBool("inherit_effects"))
            {
                continue;
            }

            var values = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var inner in swap.Nodes)
            {
                if (inner.Key is { } innerKey && inner.Block is { } innerBlock &&
                    IsAlwaysOnModifierBlock(innerKey))
                {
                    Accumulate(values, innerBlock, loader);
                }
            }

            if (values.Count == 0)
            {
                continue;
            }

            var when = requirements.CompileEffectCondition(swap.GetBlock("trigger"));

            found.Add((when, new ConditionalEffects(when, values)));
        }

        return found;
    }

    /// <summary>
    /// Gathers the sentences an option's script describes itself with.
    /// </summary>
    /// <remarks>
    /// Down through whatever the block holds, because the game wraps these in ifs as often as not -
    /// Galactic Wonders names one tooltip for a megacorp and another for everybody else. Both are
    /// taken: they describe the same unlock in two voices, and inventing a condition for a sentence
    /// would be claiming to know which applies.
    /// </remarks>
    private static void CollectUnlocks(CwBlock block, List<string> into)
    {
        foreach (var node in block.Nodes)
        {
            if (node.Key == "custom_tooltip" && node.ScalarValue is { Length: > 0 } key)
            {
                if (!into.Contains(key, StringComparer.Ordinal))
                {
                    into.Add(key);
                }
            }
            else if (node.Block is { } nested)
            {
                CollectUnlocks(nested, into);
            }
        }
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
