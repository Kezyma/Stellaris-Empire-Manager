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
    /// Conditional modifiers are deliberately left out of the totals. They apply only in a game
    /// already under way — once a tradition is adopted, or where a planet exists — so counting them
    /// would promise an empire bonuses it does not start with. They are listed against their own
    /// option instead, where the condition can be shown alongside them.
    /// </remarks>
    public static IReadOnlyList<CombinedModifier> Combine(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var totals = new Dictionary<string, List<EffectSource>>(StringComparer.Ordinal);

        foreach (var (key, effects) in Selected(context))
        {
            foreach (var (modifier, value) in effects.Modifiers)
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
    /// The panel says so when it is true, and said so always before — a footnote about conditional
    /// bonuses under every empire, including the ones that have none. Conditional effects are the
    /// only thing <see cref="Combine"/> leaves out, so this is the whole of what the note is about.
    /// </remarks>
    public static bool AnyConditional(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Selected(context).Any(o => o.Effects.Conditional.Count > 0);
    }

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
