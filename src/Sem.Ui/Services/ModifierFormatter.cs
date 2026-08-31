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
                // Named here as well as in the HTML form, so that sorting a list by this puts the
                // rows in the order they will be read in rather than in the order of a word the
                // reader is never shown.
                return Named(key, _localizer.Text(candidate), html: false);
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

    /// <summary>
    /// How a shroud patron's own key is dressed up into an attunement modifier's key.
    /// </summary>
    /// <remarks>
    /// Three shapes, all of which read as the same anonymous line and all of which name the patron
    /// in the key: <c>mod_add_attunement_the_eater_of_worlds</c> is "Add Attunement with […]",
    /// <c>mod_the_eater_of_worlds_attunement_mult</c> is "Attunement with […]", and
    /// <c>mod_eater_of_worlds_monthly_attunement_add</c> is "Monthly Attunement with […]". Only the
    /// first was handled, so an empire could show one named row and two anonymous ones for the same
    /// patron.
    /// </remarks>
    private static readonly (string Prefix, string Suffix)[] AttunementShapes =
    [
        ("add_attunement_", ""),
        ("", "_attunement_mult"),
        ("", "_monthly_attunement_add"),
    ];

    /// <summary>
    /// Puts the entity's name into a label the game leaves anonymous.
    /// </summary>
    /// <remarks>
    /// The shroud patrons are labelled <c>Add Attunement with [This.GetEaterColor]</c>, and that
    /// scripted value answers <c>UNDISCOVERED_PATRON_ARTICLE</c> — "an Unknown Entity" — until a game
    /// has met the patron. Faithful, and useless in a designer: five separate modifiers all read as
    /// the same anonymous line.
    ///
    /// The key says which one it is. Strip the affixes from
    /// <c>add_attunement_the_eater_of_worlds</c> and <c>the_eater_of_worlds</c> is itself an entry,
    /// resolving through <c>$name_eater$</c> to "Eater of Worlds". Both halves of the swap are read
    /// out of the localisation rather than written here, so this holds in any language.
    /// </remarks>
    private string Named(string key, string label, bool html = true)
    {
        if (Patron(key) is not { } entity)
        {
            return label;
        }

        // Both halves read the same way the label did, so the substitution matches what is in it.
        var anonymous = html
            ? _localizer.Html("UNDISCOVERED_PATRON_ARTICLE", string.Empty)
            : _localizer.Text("UNDISCOVERED_PATRON_ARTICLE", string.Empty);

        var named = html
            ? _localizer.Html(entity, string.Empty)
            : _localizer.Text(entity, string.Empty);

        return anonymous.Length > 0 && named.Length > 0
            ? label.Replace(anonymous, named, StringComparison.Ordinal)
            : label;
    }

    /// <summary>
    /// The patron a modifier's key names, where it names one.
    /// </summary>
    /// <remarks>
    /// The monthly modifiers drop the article — <c>eater_of_worlds_monthly_attunement_add</c> against
    /// the entry <c>the_eater_of_worlds</c> — so the stem is tried both ways. Anything that is not a
    /// patron simply fails to be an entry and is left alone, and even a false match would be
    /// harmless: the swap only touches a label that already says "an Unknown Entity".
    /// </remarks>
    private string? Patron(string key)
    {
        foreach (var (prefix, suffix) in AttunementShapes)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal) ||
                !key.EndsWith(suffix, StringComparison.Ordinal) ||
                key.Length <= prefix.Length + suffix.Length)
            {
                continue;
            }

            var stem = key[prefix.Length..^suffix.Length];

            if (_localizer.Has(stem))
            {
                return stem;
            }

            if (_localizer.Has($"the_{stem}"))
            {
                return $"the_{stem}";
            }
        }

        return null;
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
