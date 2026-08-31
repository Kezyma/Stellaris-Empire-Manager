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
            return fallback ?? Prettify(key);
        }

        return StripMarkup(ResolveScripted(ResolveConcepts(Substitute(value, 0))));
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
    /// <param name="gender">
    /// The gender of whoever is being named, where the caller knows it. Some name parts are written
    /// in two forms and the game picks between them; nothing else in a name depends on it.
    /// </param>
    public string Name(LocRef? name, string? fallback = null, string? gender = null)
    {
        if (name is null || name.IsEmpty)
        {
            return fallback ?? string.Empty;
        }

        // Applied over the whole answer as well as inside it, because a name that is one plain key
        // rather than two parts can still resolve to text written in two forms.
        return LeaderName.Variant(Name(name, 0, gender), gender) is { Length: > 0 } text
            ? text
            : fallback ?? string.Empty;
    }

    /// <summary>
    /// A ruler's name as it should be read, whichever way the design holds it.
    /// </summary>
    /// <remarks>
    /// One place rather than four. A design usually keeps the whole name under <c>full_names</c>,
    /// but one copied out of a running game keeps it in two parts instead, and half the screens that
    /// showed a ruler had quietly forgotten the second case. The gender is passed because some name
    /// parts are written in two forms.
    /// </remarks>
    public string RulerName(RulerDesign? ruler, string? fallback = null)
    {
        if (ruler is null)
        {
            return fallback ?? string.Empty;
        }

        var names = ruler.Name;

        if (Name(names.FullNames, null, ruler.Gender) is { Length: > 0 } whole)
        {
            return whole;
        }

        var parts = new[] { names.FirstName, names.SecondName }
            .Select(part => Name(part, null, ruler.Gender))
            .Where(part => part.Length > 0)
            .ToList();

        return parts.Count > 0
            ? LeaderName.Compose(parts[0], parts.Count > 1 ? parts[1] : null, ruler.Gender)
            : fallback ?? string.Empty;
    }

    private string Name(LocRef name, int depth, string? gender = null)
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
                return Words(name, depth, "1");

            // Both leader forms carry a given name and a family name, and both mean the whole name:
            // %LEADER_1% over Lucius and Salazar is Lucius Salazar. Reading only the first of them,
            // which is what happened before, dropped twelve rulers' surnames.
            case LeaderOnePart:
            case LeaderTwoParts:
                return LeaderName.Compose(Part(name, "1", depth), Part(name, "2", depth), gender);

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

    /// <summary>
    /// One part of a leader's name, still carrying its hole and its forms.
    /// </summary>
    /// <remarks>
    /// A part is not a name and must not be tidied into one. Half of these are frames — a family
    /// name written <c>"$1$ Aburia"</c> to be wrapped round a given name — and the ordinary path
    /// deletes a hole nothing filled, which would throw away the very thing the parts are joined by.
    /// A part that carries variables of its own is not a leaf and goes back through the ordinary
    /// path, which no name list does today but nothing prevents.
    /// </remarks>
    private string Part(LocRef name, string key, int depth)
    {
        if (name.Variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Value is not { } value)
        {
            return string.Empty;
        }

        if (value.IsLiteral || value.Variables.Count > 0 || depth + 1 >= MaxSubstitutionDepth)
        {
            return Name(value, depth + 1);
        }

        return _entries.TryGetValue(value.Key, out var template)
            ? StripMarkup(Substitute(template, 0))
            : Prettify(value.Key);
    }

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
        value = ResolveScripted(ResolveConcepts(value));

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
    ///
    /// A link may also name what it points at by scope — <c>['building:building_ranger_lodge']</c> —
    /// and then it is the thing after the colon that has the words. Insisting on a bare name left
    /// sixty-nine civic and origin tooltips listing their buildings as raw script.
    /// </remarks>
    private string ResolveConcepts(string value, int depth = 0)
    {
        if (!value.Contains('[', StringComparison.Ordinal) || depth >= MaxSubstitutionDepth)
        {
            return value;
        }

        return ConceptLink().Replace(value, match =>
        {
            if (match.Groups[2].Success && match.Groups[2].Value.Trim() is { Length: > 0 } shown)
            {
                return shown;
            }

            var key = match.Groups[1].Value;

            // What a link resolves to is text like any other and may name further entries of its
            // own, so it goes back through the same passes. Returned as it stood, which is what
            // happened before, a concept whose text is a single variable arrived after substitution
            // had already run and stayed a variable: "District Specializations" read as
            // $planet_zones$ in seventy of the game's civic tooltips.
            return _entries.TryGetValue(key, out var text)
                ? ResolveConcepts(Substitute(text, depth + 1), depth + 1)
                : Prettify(key);
        });
    }

    /// <summary>
    /// A variable standing for another entry, or — with a leading <c>@</c> — for a number the script
    /// declared. What follows the bar is a format, and is captured so numbers can honour it.
    /// </summary>
    /// <remarks>
    /// A name may be a bare number. The game's own name templates are positional — "Blessed $1$" —
    /// and a pattern that insisted on a letter first could never fill one, which is why an empire
    /// built that way showed its template rather than its name.
    ///
    /// A hyphen is part of a key too. FUN3_CHR_uvi-Livve is one, and without it a prescripted
    /// ruler's name came out half resolved — "Obanva $FUN3_CHR_uvi-Livve$" — with the first of two
    /// references filled and the second printed as itself.
    /// </remarks>
    [GeneratedRegex(@"\$(@?[A-Za-z0-9_][A-Za-z0-9_.\-]*)(?:\|([^$]*))?\$")]
    private static partial Regex VariableReference();

    /// <summary>
    /// A link to the game's glossary, in every spacing its own text uses.
    /// </summary>
    /// <remarks>
    /// The whitespace is allowed because the game's files are not consistent: nearly every link is
    /// written <c>['concept_pop']</c>, but the riftworld origin has <c>[ 'concept_astral_rift',…]</c>
    /// and the fruitful one has <c>['concept_seed_bombing' ]</c>. Those are two typos in the game's
    /// text, and they were the last two tooltips in the designer still showing raw script.
    /// </remarks>
    [GeneratedRegex(@"\[\s*'(?:[a-z_]+:)?([A-Za-z0-9_]+)'\s*(?:,\s*([^\]]*?))?\s*\]")]
    private static partial Regex ConceptLink();

    /// <summary>
    /// Replaces each call into the game's script with what it falls back to.
    /// </summary>
    /// <remarks>
    /// A scripted phrase is a list of conditions and a default, and every condition asks about a
    /// game in progress — so at design time the default is the answer, and it is the same answer
    /// the game would give. One with no default, or one this installation does not declare, is
    /// removed as it was before: showing the call itself would be worse than showing nothing.
    /// </remarks>
    private string ResolveScripted(string value, int depth = 0)
    {
        if (depth >= MaxSubstitutionDepth)
        {
            // A phrase that resolves to itself would otherwise never finish.
            return ScriptedToken().Replace(value, string.Empty);
        }

        return ScriptedToken().Replace(
            value,
            match => Answer(match.Groups[1].Value) is { Length: > 0 } text
                ? ResolveScripted(ResolveConcepts(Substitute(text, 0)), depth + 1)
                : string.Empty);
    }

    /// <summary>
    /// What one call into script falls back to.
    /// </summary>
    /// <remarks>
    /// Two kinds. Most are <c>defined_text</c> entries, named without a scope, and the table of them
    /// says what each answers. The rest are calls on a scope, and the scope is nearly always a job:
    /// <c>[bureaucrat.GetNamePlural]</c> is the plural name of the bureaucrat job. Those were being
    /// dropped, because only the method after the last dot was ever looked at and no table has a
    /// <c>GetNamePlural</c> in it — so Dimensional Worship read "Physics Research produced per 100"
    /// with nothing to say what was producing it.
    /// </remarks>
    private string? Answer(string path)
    {
        var scope = path.Split('.');
        var method = scope[^1];

        if (_scriptedText.GetValueOrDefault(method) is { Length: > 0 } key &&
            _entries.TryGetValue(key, out var defined))
        {
            return defined;
        }

        return scope.Length > 1 ? OfJob(scope[0], method) : null;
    }

    /// <summary>
    /// A job's own name or picture, where the scope names a job.
    /// </summary>
    /// <remarks>
    /// The game swaps these per empire — a bureaucrat is a coordinator in a gestalt — and the swap
    /// is itself a scripted phrase resolved above, which lands here having already chosen. What is
    /// left is the plain job, which the localisation names by a key built from its own.
    ///
    /// The icon is answered with the game's own markup rather than a picture, so that it goes
    /// through the same substitution as every other inline icon and comes out right in text and in
    /// HTML alike.
    /// </remarks>
    private string? OfJob(string job, string method) => method switch
    {
        "GetName" => _entries.TryGetValue($"job_{job}", out var name) ? name : null,
        "GetNamePlural" => _entries.TryGetValue($"job_{job}_plural", out var plural) ? plural : null,
        "GetIcon" => _textIcons.ContainsKey($"job_{job}") ? $"£job_{job}£" : null,
        _ => null,
    };

    /// <summary>
    /// A value only a running game could supply, such as the name of a faction that does not exist
    /// while an empire is being designed.
    /// </summary>
    /// <remarks>
    /// The scope in front is optional, and insisting on one was a real fault: the game writes both
    /// <c>[Root.GetName]</c> and a bare <c>[GetPriest]</c>, and only the first was ever matched. The
    /// bare form is the commoner of the two in the text a designer shows — four traits announced
    /// themselves as <c>[triggered_imperial_name]</c> — even though every one of them was in the
    /// table of answers all along.
    ///
    /// The whole path is captured rather than the method at the end of it. Throwing the scope away
    /// discarded the only thing that said which job <c>[bureaucrat.GetNamePlural]</c> was about.
    /// </remarks>
    [GeneratedRegex(@"\[([A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*)\]")]
    private static partial Regex ScriptedToken();
}
