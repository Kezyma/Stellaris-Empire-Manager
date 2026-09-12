namespace Sem.GameData;

/// <summary>How a modifier's value should be displayed.</summary>
/// <param name="IsPercentage">Whether the value is a proportion, shown as a percentage.</param>
/// <param name="IsGood">Whether a larger number is the better one.</param>
/// <param name="IsNeutral">Whether the value is neither good nor bad, and so uncoloured.</param>
/// <param name="Decimals">How many decimal places to show at most.</param>
/// <param name="Declared">
/// Whether the game states this, as opposed to it having been guessed from the modifier's name.
/// Guesses are right more often than not but not always, so it is worth being able to count them.
/// </param>
public sealed record ModifierInfo(
    bool IsPercentage,
    bool IsGood,
    bool IsNeutral,
    int Decimals,
    bool Declared)
{
    /// <summary>What to assume about a modifier nothing is known about.</summary>
    public static ModifierInfo Unknown { get; } = new(false, true, false, 2, false);

    /// <summary>
    /// Whether anything actually settled how this is displayed.
    /// </summary>
    /// <remarks>
    /// True when the game's own script said, or the ending said, or the extractor's table said, or
    /// every number the game writes it with agrees. False means it reached the default with nothing
    /// to go on, and the default is a coin toss between "+20%" and "+0.2" - which is how every army
    /// bonus in the app once came to read as a fraction of an army.
    ///
    /// Carried in the data so a test can insist on it rather than repeating the rule that produced
    /// it. Evidence, not proof: <c>monthly_loyalty</c> is written only in fractions and is a flat
    /// amount all the same, which is why the table above exists at all.
    /// </remarks>
    public bool Settled { get; init; }
}

/// <summary>Modifiers that apply only when a condition holds.</summary>
/// <param name="When">The condition, compiled from the block's <c>potential</c>.</param>
/// <param name="Modifiers">What applies while it holds.</param>
public sealed record ConditionalEffects(Requirement When, IReadOnlyDictionary<string, double> Modifiers)
{
    /// <summary>
    /// The sentence the game writes in place of these numbers, where it writes one.
    /// </summary>
    /// <remarks>
    /// Ninety-one tradition swaps carry a <c>custom_tooltip</c>, which is the game saying "for this
    /// kind of empire, describe it this way instead". Reading the modifiers and not the sentence
    /// left the wordier half of a swap unsaid - and eight swaps carry nothing but the sentence, so
    /// for those the empire was shown the ordinary wording and nothing of its own.
    /// </remarks>
    public string? TooltipKey { get; init; }

    /// <summary>Whether that sentence stands in for the numbers rather than joining them.</summary>
    /// <remarks>
    /// The same distinction the option itself draws between <c>custom_tooltip</c> and
    /// <c>custom_tooltip_with_modifiers</c>, and for the same reason: one of them restates what the
    /// numbers already say, and printing both is printing it twice.
    /// </remarks>
    public bool TooltipReplacesModifiers { get; init; }

    /// <summary>What this form of the option unlocks, described by the game's own sentences.</summary>
    /// <remarks>
    /// A swap can bring its own <c>on_enabled</c>, which is where the game states what a tradition
    /// lets you build or do. Sixteen do.
    /// </remarks>
    public IReadOnlyList<string> TagKeys { get; init; } = [];
}

/// <summary>
/// Everything an option does, as the game would describe it.
/// </summary>
/// <remarks>
/// The game does not simply list an option's modifiers. A hand-written tooltip can replace that
/// list, add to it, or be the only thing there is, and some options hide their numbers entirely.
/// Recording which of those applies is what keeps a democratic authority from showing its two
/// modifiers twice, once from the script and once from the tooltip that restates them.
/// </remarks>
public sealed record EffectSet
{
    /// <summary>An option with nothing to say.</summary>
    public static EffectSet None { get; } = new();

    /// <summary>Modifiers that always apply, as keys and their values.</summary>
    public IReadOnlyDictionary<string, double> Modifiers { get; init; } =
        new Dictionary<string, double>();

    /// <summary>Modifiers that apply only in certain empires.</summary>
    public IReadOnlyList<ConditionalEffects> Conditional { get; init; } = [];

    /// <summary>
    /// Localisation keys naming capabilities rather than numbers, such as being able to use a war
    /// doctrine. Each resolves to a complete sentence.
    /// </summary>
    public IReadOnlyList<string> TagKeys { get; init; } = [];

    /// <summary>Extra text shown under the effects heading, for consequences that are not modifiers.</summary>
    public string? DescriptionKey { get; init; }

    /// <summary>Text shown under a separate penalties heading.</summary>
    public string? PenaltyKey { get; init; }

    /// <summary>A hand-written tooltip that stands in place of, or alongside, the modifier list.</summary>
    public string? TooltipKey { get; init; }

    /// <summary>
    /// Whether <see cref="TooltipKey"/> replaces the modifier list rather than adding to it. The
    /// game's default when a tooltip is declared inside a modifier block, unless it opts out.
    /// </summary>
    public bool TooltipReplacesModifiers { get; init; }

    /// <summary>Whether the option's numbers are deliberately not shown.</summary>
    public bool HideModifiers { get; init; }

    /// <summary>Whether there is anything at all to display.</summary>
    public bool IsEmpty =>
        Modifiers.Count == 0 &&
        Conditional.Count == 0 &&
        TagKeys.Count == 0 &&
        DescriptionKey is null &&
        PenaltyKey is null &&
        TooltipKey is null;

    /// <summary>
    /// The modifiers that should be listed, which is nothing when the option suppresses them or
    /// replaces them with its own wording.
    /// </summary>
    public IReadOnlyDictionary<string, double> VisibleModifiers =>
        HideModifiers || TooltipReplacesModifiers ? new Dictionary<string, double>() : Modifiers;
}
