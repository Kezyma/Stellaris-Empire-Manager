using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// Reading a name out of a design, which is a small language of its own.
/// </summary>
/// <remarks>
/// A design stores a name either as text somebody typed or as a key with variables under it, and
/// those nest four deep in the player's own file. Four of the templates are the engine's own and
/// have no entry in the game's text at all, so they are composed here rather than looked up.
/// </remarks>
public sealed partial class Localizer
{
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
///
    /// <para>
    /// <c>gender</c> is used where the caller knows it: some name parts are written in two forms and
    /// the game picks between them. Nothing else in a name depends on it.
    /// </para>
    /// </remarks>
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
}
