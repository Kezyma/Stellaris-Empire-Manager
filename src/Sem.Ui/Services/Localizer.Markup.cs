using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// Turning the game's own markup into something a page can show.
/// </summary>
/// <remarks>
/// Colour runs bounded by section signs, icon placeholders between pound signs, variables in
/// dollar signs standing for other entries, and calls into script that have to be answered with
/// the branch that applies to an empire nobody is playing yet. Four passes, each able to recurse.
/// </remarks>
public sealed partial class Localizer
{
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
