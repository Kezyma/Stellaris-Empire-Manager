using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// Turns the game's display text into something an interface can show.
/// </summary>
/// <remarks>
/// The text is marked up in the game's own way: colour runs bounded by section signs, icon
/// placeholders between pound signs, and variables in dollar signs that stand for other entries.
/// This resolves the substitutions and converts the colours to HTML, so the designer reads the way
/// the game does rather than showing raw markup.
/// </remarks>
public sealed partial class Localizer(
    IReadOnlyDictionary<string, string> entries,
    IReadOnlyDictionary<string, string>? textIcons = null,
    Func<string, string>? assetUrl = null,
    IReadOnlyDictionary<string, double>? scriptedValues = null,
    IReadOnlyDictionary<string, string>? scriptedText = null)
{
    /// <summary>How deep a chain of variables standing for other entries is followed.</summary>
    private const int MaxSubstitutionDepth = 8;

    private readonly IReadOnlyDictionary<string, string> _entries = entries ?? new Dictionary<string, string>();

    /// <summary>The numbers the script names rather than writes.</summary>
    private readonly IReadOnlyDictionary<string, double> _scriptedValues =
        scriptedValues ?? new Dictionary<string, double>();

    /// <summary>What each phrase the text calls into script for falls back to, as a key.</summary>
    private readonly IReadOnlyDictionary<string, string> _scriptedText =
        scriptedText ?? new Dictionary<string, string>();

    /// <summary>Where each inline picture lives, by the code that stands for it.</summary>
    private readonly IReadOnlyDictionary<string, string> _textIcons =
        textIcons ?? new Dictionary<string, string>();

    /// <summary>Turns an extracted image's path into an address the page can load.</summary>
    private readonly Func<string, string> _assetUrl = assetUrl ?? (path => path);

    /// <summary>The game's colour letters, as CSS colours.</summary>
    private static readonly Dictionary<char, string> Colors = new()
    {
        ['Y'] = "#e0c14a",
        ['G'] = "#5ec46a",
        ['R'] = "#d95c5c",
        ['B'] = "#5c8fd9",
        ['H'] = "#e0c14a",
        ['L'] = "#a8a8a8",
        ['S'] = "#d95c5c",
        ['T'] = "#7fbfd9",
        ['W'] = "#ffffff",
        ['M'] = "#c77fd9",
        ['E'] = "#e0c14a",
        ['_'] = "#a8a8a8",
    };

    /// <summary>Whether the game has any text under this key.</summary>
    public bool Has(string? key) => key is not null && _entries.ContainsKey(key);

    /// <summary>
    /// A label the game's own empire designer puts on a field.
    /// </summary>
    /// <remarks>
    /// The designer reproduces the game, so it should use the game's words — and then it reads in
    /// whatever language the player has, instead of in whatever English seemed reasonable at the
    /// time. The fallback is what to show if a future patch drops the key.
    /// </remarks>
    public string Label(string key, string fallback) => Text(key, fallback);

    /// <summary>
    /// A heading in the game's own words.
    /// </summary>
    /// <remarks>
    /// Some of the game's labels carry their colon, because it uses them mid-sentence — "Civics:" is
    /// one. A heading has its own punctuation, so a trailing colon is dropped rather than a separate
    /// English word being invented for it.
    /// </remarks>
    public string Heading(string key, string fallback) => Label(key, fallback).TrimEnd(':', ' ');

    /// <summary>
    /// One of the counters the designer keeps, as the game words it.
    /// </summary>
    /// <remarks>
    /// The game writes these as a sentence with the number inside — "Trait Points Left: 2" — so the
    /// label and the figure come out of one entry rather than being stitched together here.
    /// </remarks>
    public string Counter(string key, string fallback, int points) =>
        Text(key, fallback).Replace("$POINTS|H$", points.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

    /// <summary>
    /// A counter written as a spend against an allowance rather than as a number left.
    /// </summary>
    /// <remarks>
    /// The game words some of these as a sentence with the figure inside — <c>TRAIT_POINTS</c> is
    /// "Points: 2" — and others as a bare noun, since it puts the figure elsewhere. Both are wanted
    /// here as one line, so a sentence has the reading substituted into it and a noun has it added
    /// after a colon. Only punctuation is ever added, so nothing is written in English that the
    /// game did not write.
    /// </remarks>
    public string Gauge(string key, string fallback, string reading)
    {
        var text = Text(key, fallback);

        return text.Contains("$POINTS|H$", StringComparison.Ordinal)
            ? text.Replace("$POINTS|H$", reading, StringComparison.Ordinal)
            : $"{text.TrimEnd(':', ' ')}: {reading}";
    }

    /// <summary>
    /// The plain text for a key, with variables resolved and markup stripped. Falls back to the
    /// key itself, which is what the game shows when a translation is missing.
    /// </summary>
    public string Text(string? key, string? fallback = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return fallback ?? string.Empty;
        }

        if (!_entries.TryGetValue(key, out var value))
        {
            return fallback ?? Prettified(key);
        }

        if (_resolved.TryGetValue(key, out var done))
        {
            return done;
        }

        done = StripMarkup(ResolveScripted(ResolveConcepts(Substitute(value, 0))));
        _resolved[key] = done;

        return done;
    }

    /// <summary>
    /// What each key came out as, so the work of resolving it is done once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolving a line walks it for variables, then for concepts, then for scripted text, then
    /// strips the markup - four passes, and every one of them can recurse. It was being done afresh
    /// on every call, and the calls are not rare: a picker resolves every option's name purely to
    /// sort by it, so drawing the civics list resolved two hundred and eighty-one lines that had
    /// been resolved identically the render before.
    /// </para>
    /// <para>
    /// Keyed on the key alone, which is sound because the two branches that consider the fallback
    /// have already returned by this point - the fallback only decides what happens when there is
    /// nothing to resolve. And safe to hold for the life of the localiser because everything the
    /// resolution reads is fixed at construction: the entries, the scripted values, the scripted
    /// text and the icons are all readonly, so a key can only ever come out one way.
    /// </para>
    /// </remarks>
    private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);

    /// <summary>
    /// The text for a key as HTML, keeping the game's colour runs.
    /// </summary>
    /// <remarks>
    /// Everything is HTML-escaped before any markup is added, so a stray angle bracket in the
    /// game's text cannot turn into an element.
    /// </remarks>
    public string Html(string? key, string? fallback = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return System.Net.WebUtility.HtmlEncode(fallback ?? string.Empty);
        }

        if (!_entries.TryGetValue(key, out var value))
        {
            return System.Net.WebUtility.HtmlEncode(fallback ?? Prettified(key));
        }

        if (_rendered.TryGetValue(key, out var done))
        {
            return done;
        }

        done = ToHtml(Substitute(value, 0));
        _rendered[key] = done;

        return done;
    }

    /// <summary>
    /// What each key came out as in HTML, so the markup is rendered once.
    /// </summary>
    /// <remarks>
    /// Held for the same reasons as <see cref="_resolved"/> and sound for the same reasons: the
    /// fallback is only consulted by the branches above, and everything the rendering reads is fixed
    /// at construction. This one was the more expensive of the two to leave uncached - on top of the
    /// variable, concept and scripted passes it walks the line a character at a time to turn the
    /// game's colour runs into spans - and a picker asks it for every option's description on every
    /// render, which for the civics list is eighty-one lines re-rendered identically each time.
    /// </remarks>
    private readonly Dictionary<string, string> _rendered = new(StringComparer.Ordinal);


    /// <summary>
    /// A readable label for a key the game has no text for, held so it is worked out once.
    /// </summary>
    /// <remarks>
    /// This is the answer for every key the game has no text for, which is not the rare case it
    /// sounds like: two of the fifty-two shipsets have an entry, and the rest reach this on every
    /// render that names them.
    /// </remarks>
    private string Prettified(string key)
    {
        if (_prettified.TryGetValue(key, out var done))
        {
            return done;
        }

        done = Prettify(key);
        _prettified[key] = done;

        return done;
    }

    private readonly Dictionary<string, string> _prettified = new(StringComparer.Ordinal);

    /// <summary>The prefixes a key wears to say what kind of thing it names, which a label does not.</summary>
    private static readonly string[] KeyPrefixes =
        ["trait_", "civic_", "origin_", "ethic_", "auth_", "gov_", "pc_"];

    /// <summary>
    /// A readable label for a key the game has no text for, by turning it into words.
    /// </summary>
    public static string Prettify(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var trimmed = key;
        foreach (var prefix in KeyPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                trimmed = trimmed[prefix.Length..];
                break;
            }
        }

        var words = trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? key
            : string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
