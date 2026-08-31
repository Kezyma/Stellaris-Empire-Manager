using System.Globalization;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>One modifier as it should appear on screen.</summary>
/// <param name="Key">The modifier's name, for grouping and for a tooltip.</param>
/// <param name="Label">Its display name, already in the player's language.</param>
/// <param name="Value">Its value, formatted the way the game formats it.</param>
/// <param name="Tone">Whether it reads as a benefit, a cost, or neither.</param>
public sealed record FormattedModifier(string Key, string Label, string Value, ModifierTone Tone);

/// <summary>How a modifier's value should be coloured.</summary>
public enum ModifierTone
{
    /// <summary>Neither good nor bad, or not known to be either.</summary>
    Neutral,

    /// <summary>An improvement.</summary>
    Good,

    /// <summary>A drawback.</summary>
    Bad,
}

/// <summary>
/// Turns a modifier and its value into the line the game would show.
/// </summary>
/// <remarks>
/// Two things have to be got right. A modifier's label is written one of three ways in the game's
/// text and the three barely overlap, so all three have to be tried. And whether the value is a
/// proportion or a flat amount cannot be read from the label — the two forms of naval capacity share
/// one — which is why it is worked out during extraction and carried in the database.
/// </remarks>
public sealed class ModifierFormatter(Localizer localizer, GameDatabase database)
{
    private readonly Localizer _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    private readonly GameDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    /// <summary>Formats one modifier.</summary>
    public FormattedModifier Format(string key, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var info = _database.Modifiers.TryGetValue(key, out var known) ? known : ModifierInfo.Unknown;

        return new FormattedModifier(key, Label(key), FormatValue(value, info), ToneOf(value, info));
    }

    /// <summary>
    /// Formats a set of modifiers, in a stable order so a list does not reshuffle itself as the
    /// empire changes.
    /// </summary>
    public IReadOnlyList<FormattedModifier> Format(IReadOnlyDictionary<string, double> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        return [.. modifiers
            .Where(m => m.Value != 0)
            .Select(m => Format(m.Key, m.Value))
            .OrderBy(m => m.Label, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// Finds a modifier's display name.
    /// </summary>
    /// <remarks>
    /// The game writes these three ways: a lowercase prefixed key, an uppercase one, or the
    /// modifier's own name with no prefix at all. Intelligence, for instance, is written the third
    /// way and has no prefixed form anywhere. Failing all three the name itself is made readable,
    /// which is what the game does with a modifier a mod introduced.
    /// </remarks>
    public string Label(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var candidate in Candidates(key))
        {
            if (_localizer.Has(candidate))
            {
                return _localizer.Text(candidate);
            }
        }

        return Localizer.Prettify(Readable(key));
    }

    /// <summary>The same, keeping the colours and inline pictures the game's own label carries.</summary>
    public string LabelHtml(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var candidate in Candidates(key))
        {
            if (_localizer.Has(candidate))
            {
                return Named(key, _localizer.Html(candidate));
            }
        }

        return System.Net.WebUtility.HtmlEncode(Localizer.Prettify(Readable(key)));
    }

    /// <summary>The modifiers whose label hides a name their own key states.</summary>
    private const string AttunementPrefix = "add_attunement_";

    /// <summary>
    /// Puts the entity's name into a label the game leaves anonymous.
    /// </summary>
    /// <remarks>
    /// The shroud patrons are labelled <c>Add Attunement with [This.GetEaterColor]</c>, and that
    /// scripted value answers <c>UNDISCOVERED_PATRON_ARTICLE</c> — "an Unknown Entity" — until a game
    /// has met the patron. Faithful, and useless in a designer: five separate modifiers all read as
    /// the same anonymous line.
    ///
    /// The key says which one it is. Strip the prefix from
    /// <c>add_attunement_the_eater_of_worlds</c> and <c>the_eater_of_worlds</c> is itself an entry,
    /// resolving through <c>$name_eater$</c> to "Eater of Worlds". Both halves of the swap are read
    /// out of the localisation rather than written here, so this holds in any language.
    /// </remarks>
    private string Named(string key, string label)
    {
        if (!key.StartsWith(AttunementPrefix, StringComparison.Ordinal))
        {
            return label;
        }

        var entity = key[AttunementPrefix.Length..];

        if (!_localizer.Has(entity))
        {
            return label;
        }

        var anonymous = _localizer.Html("UNDISCOVERED_PATRON_ARTICLE", string.Empty);
        var named = _localizer.Html(entity, string.Empty);

        return anonymous.Length > 0 && named.Length > 0
            ? label.Replace(anonymous, named, StringComparison.Ordinal)
            : label;
    }

    private static IEnumerable<string> Candidates(string key)
    {
        yield return $"mod_{key}";
        yield return $"MOD_{key.ToUpperInvariant()}";
        yield return key;
    }

    /// <summary>
    /// Trims the grammar of a modifier's name before it is turned into words.
    /// </summary>
    /// <remarks>
    /// The endings say how a value applies, not what it is, and the value beside the label already
    /// shows that: "Scavenge Debris +5%" needs no help from a trailing "Mult".
    /// </remarks>
    private static string Readable(string key)
    {
        foreach (var suffix in (string[])["_mult", "_add"])
        {
            if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return key[..^suffix.Length];
            }
        }

        return key;
    }

    /// <summary>
    /// Formats a value, always signed, because a modifier is a change rather than an amount.
    /// </summary>
    private static string FormatValue(double value, ModifierInfo info)
    {
        var shown = info.IsPercentage ? value * 100 : value;
        var decimals = info.IsPercentage ? Math.Max(0, info.Decimals - 2) : info.Decimals;

        // Trailing zeroes on a whole number read as false precision: the game shows +10%, not
        // +10.00%.
        var text = Math.Round(shown, decimals).ToString(
            "0." + new string('#', Math.Max(0, decimals)),
            CultureInfo.CurrentCulture);

        var sign = shown > 0 ? "+" : string.Empty;
        return $"{sign}{text}{(info.IsPercentage ? "%" : string.Empty)}";
    }

    /// <summary>
    /// Whether a value reads as a benefit.
    /// </summary>
    /// <remarks>
    /// Not simply whether it is positive. Anything an empire pays is better when it is lower, so a
    /// negative influence cost is an improvement and should not be shown in the colour of a loss.
    /// </remarks>
    private static ModifierTone ToneOf(double value, ModifierInfo info)
    {
        if (info.IsNeutral || value == 0)
        {
            return ModifierTone.Neutral;
        }

        var better = info.IsGood ? value > 0 : value < 0;
        return better ? ModifierTone.Good : ModifierTone.Bad;
    }
}
