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
public sealed partial class Localizer(IReadOnlyDictionary<string, string> entries)
{
    /// <summary>How deep a chain of variables standing for other entries is followed.</summary>
    private const int MaxSubstitutionDepth = 8;

    private readonly IReadOnlyDictionary<string, string> _entries = entries ?? new Dictionary<string, string>();

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

        return StripMarkup(Substitute(value, 0));
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
    private static string ToHtml(string value)
    {
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
                // Icon placeholders are dropped rather than shown; the designer supplies its own.
                while (i + 1 < value.Length && value[i + 1] != '£')
                {
                    i++;
                }

                i++;
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

    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_.]*)(?:\|[^$]*)?\$")]
    private static partial Regex VariableReference();
}
