using Sem.GameData;

namespace Sem.Rules;

/// <summary>Where one contribution to a total came from.</summary>
/// <param name="SourceKey">The option's key, for showing its name.</param>
/// <param name="Value">What it contributes.</param>
public sealed record EffectSource(string SourceKey, double Value);

/// <summary>One modifier, totalled across everything the empire has chosen.</summary>
/// <param name="Key">The modifier.</param>
/// <param name="Total">The sum of every contribution.</param>
/// <param name="Sources">Which choices contributed, largest first.</param>
public sealed record CombinedModifier(string Key, double Total, IReadOnlyList<EffectSource> Sources);

/// <summary>
/// Everything the chosen options do, gathered together.
/// </summary>
/// <remarks>
/// Summing is the only honest way to present this. An empire whose ethic and whose civic both raise
/// ship fire rate has one fire rate, not two entries that the player is left to add up; and showing
/// where each total came from is what makes it possible to see which choice is carrying it.
/// </remarks>
public static class DesignEffects
{
    /// <summary>
    /// Adds up every modifier from the species, ethics, authority, civics, origin and traits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A conditional modifier counts when its condition can be settled from the design and holds.
    /// Half of them can be: a militarist empire's claim influence cost is conditional on not being
    /// nomadic, and whether it is nomadic is a field of the design. Leaving those out understated
    /// the totals for a great many empires — militarist and xenophobe between them account for most
    /// of the conditions in the game — and printed a footnote apologising for it.
    /// </para>
    /// <para>
    /// The rest genuinely belong to a game already under way: once a tradition is adopted, where a
    /// planet exists. Those stay out, since counting them would promise an empire bonuses it does
    /// not start with, and they are listed against their own option where the condition can be
    /// shown alongside them.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CombinedModifier> Combine(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var totals = new Dictionary<string, List<EffectSource>>(StringComparer.Ordinal);

        foreach (var (key, effects) in Selected(context))
        {
            foreach (var (modifier, value) in Applying(effects, context))
            {
                if (value == 0)
                {
                    continue;
                }

                if (!totals.TryGetValue(modifier, out var sources))
                {
                    totals[modifier] = sources = [];
                }

                sources.Add(new EffectSource(key, value));
            }
        }

        return
        [
            .. totals
                .Select(p => new CombinedModifier(
                    p.Key,
                    p.Value.Sum(s => s.Value),
                    [.. p.Value.OrderByDescending(s => Math.Abs(s.Value))]))
                .Where(m => m.Total != 0)
                .OrderBy(m => m.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Every option the empire has chosen, with what it does.
    /// </summary>
    /// <remarks>
    /// An option that describes itself in its own words still contributes its numbers here. The
    /// tooltip replaces the list shown against that option, not the arithmetic — an empire whose
    /// authority gives ten percent faction approval has it whether or not the authority chose to
    /// spell it out.
    /// </remarks>
    /// <summary>
    /// Whether any of the empire's choices carries a modifier that was left out of the totals.
    /// </summary>
    /// <remarks>
    /// Not simply "has a conditional" — most conditions can be settled here and their modifiers are
    /// counted. This is about the ones that cannot be, which are the whole of what the footnote is
    /// for, and are rarer than the footnote's previous readings suggested.
    /// </remarks>
    public static bool AnyConditional(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Selected(context)
            .SelectMany(o => o.Effects.Conditional)
            .Any(group => !Evaluator.CanDecide(group.When, context) && group.Modifiers.Count > 0);
    }

    /// <summary>
    /// One option's modifiers, taking in the conditional ones whose condition is settled and met.
    /// </summary>
    /// <remarks>
    /// Public because the trait budget is worked out from the same numbers. It used to read a
    /// separate flat list the extractor built from the always-on modifiers only, which silently lost
    /// every bonus the game states inside a <c>swap_type</c> — and left the budget bar and the
    /// modifier panel disagreeing on screen about the same civic. Reading both from here is what
    /// makes that disagreement impossible rather than merely fixed.
    /// </remarks>
    public static IEnumerable<KeyValuePair<string, double>> Applying(EffectSet effects, DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(context);

        return ApplyingCore(effects, context);
    }

    private static IEnumerable<KeyValuePair<string, double>> ApplyingCore(EffectSet effects, DesignContext context)
    {
        foreach (var modifier in effects.Modifiers)
        {
            yield return modifier;
        }

        foreach (var group in effects.Conditional)
        {
            if (!Evaluator.CanDecide(group.When, context) || !Evaluator.IsSatisfied(group.When, context))
            {
                continue;
            }

            foreach (var modifier in group.Modifiers)
            {
                yield return modifier;
            }
        }
    }

    /// <summary>
    /// Reads conditions. Stateless, so one is enough for every design the app ever shows.
    /// </summary>
    private static readonly RequirementEvaluator Evaluator = new();

    public static IEnumerable<(string Key, EffectSet Effects)> Selected(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var database = context.Database;

        foreach (var ethic in database.Ethics.Where(e => context.Ethics.Contains(e.Key)))
        {
            yield return (ethic.Key, ethic.Effects);
        }

        foreach (var authority in database.Authorities.Where(a => a.Key == context.Authority))
        {
            yield return (authority.Key, authority.Effects);
        }

        foreach (var civic in database.Civics.Where(c =>
                     context.Civics.Contains(c.Key) || c.Key == context.Origin))
        {
            yield return (civic.Key, civic.Effects);
        }

        foreach (var trait in database.Traits.Where(t => context.Traits.Contains(t.Key)))
        {
            yield return (trait.Key, trait.Effects);
        }
    }
}
