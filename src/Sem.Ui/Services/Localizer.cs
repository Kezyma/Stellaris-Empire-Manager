using System.Text;
using System.Text.RegularExpressions;

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
    Func<string, string>? assetUrl = null)
{
    /// <summary>How deep a chain of variables standing for other entries is followed.</summary>
    private const int MaxSubstitutionDepth = 8;

    private readonly IReadOnlyDictionary<string, string> _entries = entries ?? new Dictionary<string, string>();

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
            return _entries.TryGetValue(name, out var replacement)
                ? Substitute(replacement, depth + 1)
                : match.Value;
        });
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

    /// <summary>Replaces variables standing for other entries with those entries' text.</summary>
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_.]*)(?:\|[^$]*)?\$")]
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
