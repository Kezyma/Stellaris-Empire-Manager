using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sem.Designs;
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
    IReadOnlyDictionary<string, double>? scriptedValues = null)
{
    /// <summary>How deep a chain of variables standing for other entries is followed.</summary>
    private const int MaxSubstitutionDepth = 8;

    private readonly IReadOnlyDictionary<string, string> _entries = entries ?? new Dictionary<string, string>();

    /// <summary>The numbers the script names rather than writes.</summary>
    private readonly IReadOnlyDictionary<string, double> _scriptedValues =
        scriptedValues ?? new Dictionary<string, double>();

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
    /// One of the counters the designer keeps, as the game words it.
    /// </summary>
    /// <remarks>
    /// The game writes these as a sentence with the number inside — "Trait Points Left: 2" — so the
    /// label and the figure come out of one entry rather than being stitched together here.
    /// </remarks>
    public string Counter(string key, string fallback, int points) =>
        Text(key, fallback).Replace("$POINTS|H$", points.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

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
            return fallback ?? Prettify(key);
        }

        return StripMarkup(ScriptedToken().Replace(ResolveConcepts(Substitute(value, 0)), string.Empty));
    }

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
            return System.Net.WebUtility.HtmlEncode(fallback ?? Prettify(key));
        }

        return ToHtml(Substitute(value, 0));
    }

    /// <summary>
    /// Renders a piece of the game's text that is already in hand, rather than one looked up by key.
    /// </summary>
    public string HtmlOf(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : ToHtml(Substitute(value, 0));

    /// <summary>
    /// Reads a name out of a design.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A design stores a name either as text the player typed or as a localisation key, and the two
    /// look identical in the file apart from a <c>literal</c> flag. Showing the key as though it
    /// were the name is why every built-in empire read
    /// <c>PRESCRIPTED_species_name_iferyx</c> in its own name field.
    /// </para>
    /// <para>
    /// A key may be a template with its own variables, and those nest: the player's own file goes
    /// four deep. Four of the templates are the engine's own and have no entry in the game's text at
    /// all — <c>%ADJ%</c>, <c>%ADJECTIVE%</c> and the two leader forms — so they are composed here.
    /// The Blessed Oxanalytoran Union is stored as <c>%ADJ%</c> over <c>Blessed</c> over
    /// <c>%ADJECTIVE%</c> of <c>SPEC_Oxanalytor</c> over <c>Union</c>, and comes back out as its
    /// name.
    /// </para>
    /// </remarks>
    public string Name(LocRef? name, string? fallback = null)
    {
        if (name is null || name.IsEmpty)
        {
            return fallback ?? string.Empty;
        }

        return Name(name, 0) is { Length: > 0 } text ? text : fallback ?? string.Empty;
    }

    private string Name(LocRef name, int depth)
    {
        if (name.IsLiteral || depth >= MaxSubstitutionDepth)
        {
            return name.Key;
        }

        // The engine builds these itself; the game's text files have no entry for any of them, so
        // looking them up finds nothing and the name comes out as one of its own fragments.
        switch (name.Key)
        {
            case AdjWrapper:
            case LeaderOnePart:
                return Words(name, depth, "1");

            case LeaderTwoParts:
                return Words(name, depth, "1", "2");

            case LocRef.AdjectiveTemplate:
                // "Oxanalytor" becomes "Oxanalytoran", and whatever follows it follows it: the
                // Blessed Oxanalytoran Union keeps its Union in the same variable.
                var species = Variable(name, "adjective", depth);

                return Join(
                    species.Length > 0 ? NameGenerator.Adjective(species) : string.Empty,
                    Words(name, depth, "1"));
        }

        // A name's own variables are more specific than the game's text, so they are filled first
        // and whatever is left over is looked up.
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var head = _entries.TryGetValue(name.Key, out var template)

            // A placeholder nothing filled is dropped rather than shown. The Commonwealth of Man's
            // species adjective is stored as "Human $1$" — the same entry serves the empire's own
            // name, where something does fill it — and a player reading the species field should see
            // "Human", not the machinery.
            ? Tidy(StripMarkup(Substitute(Fill(template, name, depth, used), 0)))

            // No text under the key. The game's own empire name parts are like this —
            // "Corporate_Alliance" is shown as the words it spells.
            : Prettify(name.Key);

        // Whatever the text did not ask for still belongs to the name: a species adjective is stored
        // as the plain word "Human" with the empire's noun hanging off it, and the game writes the
        // two together.
        return Join(head, Words(name, depth, Positional.Where(p => !used.Contains(p)).ToArray()));
    }

    /// <summary>The variables a name carries by position rather than by name.</summary>
    private static readonly string[] Positional = ["1", "2"];

    /// <summary>The placeholder wrapping a name built from an adjective and a noun.</summary>
    private const string AdjWrapper = "%ADJ%";

    private const string LeaderOnePart = "%LEADER_1%";

    private const string LeaderTwoParts = "%LEADER_2%";

    /// <summary>The named variables of a name, in order, as words with spaces between them.</summary>
    private string Words(LocRef name, int depth, params string[] keys) =>
        Join([.. keys.Select(k => Variable(name, k, depth))]);

    /// <summary>One variable of a name, resolved, or nothing where the name has no such variable.</summary>
    private string Variable(LocRef name, string key, int depth) =>
        name.Variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value is { } value
            ? Name(value, depth + 1)
            : string.Empty;

    private static string Join(params string[] parts) =>
        string.Join(' ', parts.Where(p => p.Length > 0));

    /// <summary>
    /// Removes the placeholders nothing filled, and the gaps they leave behind.
    /// </summary>
    /// <remarks>
    /// Only names are treated this way. Elsewhere an unresolved token is left visible so it can be
    /// chased, but a name is something a player reads and types over, and half a template in a text
    /// box is worse than a slightly shorter name.
    /// </remarks>
    private static string Tidy(string value) =>
        value.Contains('$', StringComparison.Ordinal)
            ? Whitespace().Replace(VariableReference().Replace(value, string.Empty), " ").Trim()
            : value;

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Whitespace();

    /// <summary>
    /// Puts a stored name's own variables into the text its key resolved to, recording which of them
    /// the text actually asked for.
    /// </summary>
    private string Fill(string template, LocRef name, int depth, HashSet<string> used)
    {
        if (name.Variables.Count == 0)
        {
            return template;
        }

        return VariableReference().Replace(template, match =>
        {
            var wanted = match.Groups[1].Value;

            var variable = name.Variables.FirstOrDefault(v =>
                string.Equals(v.Key, wanted, StringComparison.OrdinalIgnoreCase));

            if (variable?.Value is not { } value)
            {
                return match.Value;
            }

            used.Add(wanted);
            return Name(value, depth + 1);
        });
    }

    /// <summary>
    /// A readable label for a key the game has no text for, by turning it into words.
    /// </summary>
    public static string Prettify(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var trimmed = key;
        foreach (var prefix in (string[])["trait_", "civic_", "origin_", "ethic_", "auth_", "gov_", "pc_"])
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

    /// <summary>Replaces variables standing for other entries with those entries' text.</summary>
    private string Substitute(string value, int depth)
    {
        if (depth >= MaxSubstitutionDepth || !value.Contains('$', StringComparison.Ordinal))
        {
            return value;
        }

        return VariableReference().Replace(value, match =>
        {
            var name = match.Groups[1].Value;

            // A name beginning with @ stands for a number the script declared rather than another
            // piece of text, and what follows the bar says how to write it.
            if (name.StartsWith('@'))
            {
                return _scriptedValues.TryGetValue(name[1..], out var number)
                    ? Number(number, match.Groups[2].Value)
                    : match.Value;
            }

            return _entries.TryGetValue(name, out var replacement)
                ? Substitute(replacement, depth + 1)
                : match.Value;
        });
    }

    /// <summary>
    /// Writes a number the way the text asks for it.
    /// </summary>
    /// <remarks>
    /// The flags after the bar are the game's own number format, and the ones its text actually uses
    /// are <c>*0</c>, <c>0</c>, <c>0%</c>, <c>%0</c>, <c>+0%</c>, <c>0%+</c> and <c>0=+%</c>. A digit
    /// is how many decimal places to keep, a per cent sign multiplies by a hundred and adds one, and
    /// a plus sign forces the sign onto a positive number. Anything else is written plainly rather
    /// than guessed at.
    /// </remarks>
    private static string Number(double value, string flags)
    {
        var percent = flags.Contains('%', StringComparison.Ordinal);
        var signed = flags.Contains('+', StringComparison.Ordinal);
        var places = flags.FirstOrDefault(char.IsAsciiDigit) is var digit && digit != '\0'
            ? digit - '0'
            : 2;

        var shown = percent ? value * 100 : value;
        var text = shown.ToString($"F{places}", CultureInfo.InvariantCulture);

        return (signed && shown > 0 ? "+" : string.Empty) + text + (percent ? "%" : string.Empty);
    }

    /// <summary>Removes the game's markup, for places that show plain text.</summary>
    private static string StripMarkup(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                // A section sign starts a colour run or ends one; either way it is two characters.
                case '§':
                    i++;
                    break;

                // Icon placeholders have no equivalent in plain text.
                case '£':
                    while (i + 1 < value.Length && value[i + 1] != '£')
                    {
                        i++;
                    }

                    i++;
                    break;

                default:
                    builder.Append(value[i]);
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Converts the game's markup into HTML, escaping everything else.</summary>
    private string ToHtml(string value)
    {
        value = ResolveConcepts(value);
        value = ScriptedToken().Replace(value, string.Empty);

        var builder = new StringBuilder(value.Length + 32);
        var openSpans = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '§' && i + 1 < value.Length)
            {
                var code = value[++i];

                if (code == '!')
                {
                    if (openSpans > 0)
                    {
                        builder.Append("</span>");
                        openSpans--;
                    }
                }
                else if (Colors.TryGetValue(code, out var color))
                {
                    builder.Append("<span style=\"color:").Append(color).Append("\">");
                    openSpans++;
                }

                continue;
            }

            if (c == '£')
            {
                var close = value.IndexOf('£', i + 1);

                if (close < 0)
                {
                    break;
                }

                AppendIcon(builder, value[(i + 1)..close]);
                i = close;
                continue;
            }

            if (c == '\n')
            {
                builder.Append("<br>");
                continue;
            }

            builder.Append(System.Net.WebUtility.HtmlEncode(c.ToString()));
        }

        // A run the game never closed would otherwise leak its colour into the rest of the page.
        for (var i = 0; i < openSpans; i++)
        {
            builder.Append("</span>");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes one of the little pictures that appear inside the game's sentences.
    /// </summary>
    /// <remarks>
    /// A code may carry a frame number after a vertical bar, which names a variant of the same
    /// picture and can be ignored. A code with no picture is dropped rather than shown as text: the
    /// game's sentences read as though the symbol were a word, so a bare code in the middle of one
    /// looks like a fault.
    /// </remarks>
    private void AppendIcon(StringBuilder builder, string code)
    {
        var name = code.Split('|')[0];

        if (name.Length == 0 || !_textIcons.TryGetValue(name, out var path))
        {
            return;
        }

        builder.Append("<img class=\"sem-text-icon\" src=\"")
            .Append(System.Net.WebUtility.HtmlEncode(_assetUrl(path)))
            .Append("\" alt=\"\">");
    }

    /// <summary>
    /// Replaces the game's links to its own glossary with the words they display.
    /// </summary>
    /// <remarks>
    /// Written as <c>['concept_pop']</c>, or with the text to show given after the concept, as in
    /// <c>['concept_habitat_1', $tech_habitat_1$]</c>. The link itself has nowhere to go in a
    /// designer, so only the words survive.
    /// </remarks>
    private string ResolveConcepts(string value) =>
        !value.Contains("['", StringComparison.Ordinal)
            ? value
            : ConceptLink().Replace(value, match =>
            {
                if (match.Groups[2].Success && match.Groups[2].Value.Trim() is { Length: > 0 } shown)
                {
                    return shown;
                }

                var key = match.Groups[1].Value;
                return _entries.TryGetValue(key, out var text) ? text : Prettify(key);
            });

    /// <summary>
    /// A variable standing for another entry, or — with a leading <c>@</c> — for a number the script
    /// declared. What follows the bar is a format, and is captured so numbers can honour it.
    /// </summary>
    /// <remarks>
    /// A name may be a bare number. The game's own name templates are positional — "Blessed $1$" —
    /// and a pattern that insisted on a letter first could never fill one, which is why an empire
    /// built that way showed its template rather than its name.
    /// </remarks>
    [GeneratedRegex(@"\$(@?[A-Za-z0-9_][A-Za-z0-9_.]*)(?:\|([^$]*))?\$")]
    private static partial Regex VariableReference();

    [GeneratedRegex(@"\['([A-Za-z0-9_]+)'(?:\s*,\s*([^\]]*))?\]")]
    private static partial Regex ConceptLink();

    /// <summary>
    /// A value only a running game could supply, such as the name of a faction that does not exist
    /// while an empire is being designed. Removed rather than shown.
    /// </summary>
    [GeneratedRegex(@"\[[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+\]")]
    private static partial Regex ScriptedToken();
}
