using Sem.GameData;

namespace Sem.Rules;

/// <summary>A species and the world it came from, all of a piece.</summary>
/// <param name="Name">The species name.</param>
/// <param name="Plural">Its plural.</param>
/// <param name="HomePlanet">A name for the homeworld.</param>
/// <param name="HomeSystem">A name for the home system.</param>
/// <param name="NameList">The name list that goes with it.</param>
// A suggestion is returned as the database holds it. Copying it into a narrower shape dropped the
// localisation keys the game stores a chosen name by, and the copy said nothing the original did
// not.

/// <summary>
/// Invents names the way the game's own randomise buttons do.
/// </summary>
/// <remarks>
/// The game has four of these rather than one shuffle, and they draw on different things: a species
/// comes from a list of ready-made ones, a ruler from the empire's name list, a planet from that
/// list's own planets. Following that shape is what keeps the results consistent — a species picked
/// this way arrives with a homeworld and a name list that suit it.
/// </remarks>
public sealed partial class NameGenerator(GameDatabase database, Random? random = null)
{
    private readonly GameDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly Random _random = random ?? Random.Shared;

    /// <summary>Reads the conditions that decide which name shapes an empire may be given.</summary>
    private readonly RequirementEvaluator _evaluator = new();

    /// <summary>
    /// The word lists a name shape can draw on, by name.
    /// </summary>
    /// <remarks>
    /// Built once. It is read on every keystroke in the designer, and a hundred and ninety-four
    /// entries is not free to gather each time.
    ///
    /// The last declaration of a name wins, as an override does everywhere else in the game's data:
    /// <c>primitive_names</c> is declared twice, which is enough to make a dictionary refuse the
    /// whole set if it is built expecting distinct keys.
    /// </remarks>
    private IReadOnlyDictionary<string, EmpireNamePartsList> Lists
    {
        get
        {
            if (_lists is not null)
            {
                return _lists;
            }

            var lists = new Dictionary<string, EmpireNamePartsList>(StringComparer.Ordinal);

            foreach (var list in _database.EmpireNameParts)
            {
                lists[list.Key] = list;
            }

            return _lists = lists;
        }
    }

    private IReadOnlyDictionary<string, EmpireNamePartsList>? _lists;

    /// <summary>
    /// Suggests a species, its plural, its homeworld and its home system together.
    /// </summary>
    /// <param name="speciesClass">The class to suit, such as <c>MAM</c>.</param>
    public SpeciesNameSuggestion? Species(string? speciesClass)
    {
        var candidates = _database.SpeciesNames
            .Where(s => speciesClass is null || string.Equals(s.SpeciesClass, speciesClass, StringComparison.Ordinal))
            .ToList();

        // A class the game ships no ready-made species for still gets a name, from anywhere.
        if (candidates.Count == 0)
        {
            candidates = [.. _database.SpeciesNames];
        }

        return Pick(candidates);
    }

    /// <summary>
    /// Invents a ruler's name from a name list.
    /// </summary>
    /// <remarks>
    /// A name is either complete in itself or a first joined to a second, and where a list offers
    /// both the game chooses evenly between them. Gendered lists are used when they have anything in
    /// them, and the ungendered list stands in when they do not.
    /// </remarks>
    public string? Ruler(string? nameList, bool female = false)
    {
        if (Resolve(nameList)?.CharacterNames is not { } names || names.IsEmpty)
        {
            return null;
        }

        var full = names.FullNames.For(female);
        var firsts = names.FirstNames.For(female);
        var seconds = names.SecondNames.For(female);

        var canCombine = firsts.Count > 0;
        var useFull = full.Count > 0 && (!canCombine || _random.Next(2) == 0);

        var gender = female ? "female" : "male";

        if (useFull || !canCombine)
        {
            return LeaderName.Variant(Pick(full), gender) is { Length: > 0 } one ? one : null;
        }

        // A list may give first names and no family names, in which case the first name is the name.
        // Put together rather than set side by side: a family name is as often a frame written round
        // the given one — "$1$ Aburia" — as a word to follow it.
        return seconds.Count > 0
            ? LeaderName.Compose(Pick(firsts), Pick(seconds), gender)
            : LeaderName.Variant(Pick(firsts), gender) is { Length: > 0 } only ? only : null;
    }

    /// <summary>Suggests a planet name from a name list.</summary>
    public string? Planet(string? nameList) => Pick(Resolve(nameList)?.PlanetNames ?? []);

    /// <summary>
    /// Suggests an empire name, out of the game's own generator.
    /// </summary>
    public EmpireNameSuggestion? Empire(DesignContext context, EmpireNameSources sources) =>
        Pick(EmpireNames(context, sources));

    /// <summary>
    /// Every name the game could give this empire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be ours rather than the game's: five or six words per authority, written by
    /// hand, so an empire the game had named "Empire of Pakshalika" reopened to a list of five
    /// names that did not include its own. The generator is real and it is readable — two hundred
    /// and seventy-one shapes, each gated on the sort of empire it suits, filled from a hundred and
    /// ninety-three weighted word lists.
    /// </para>
    /// <para>
    /// Every shape whose condition holds is offered, in full, most typical words first. The shapes
    /// multiply — a moral democracy has one of thirteen descriptors against twenty nouns, and the
    /// corpus confirms three-word names are built exactly that way — so the list runs long. It is
    /// not capped: the randomiser picks one of these at a time and its weights bunch, which is why
    /// it repeats long before it has exhausted them, but every one of them is a name the game could
    /// give this empire and the box filters as you type.
    /// </para>
    /// </remarks>
    public IReadOnlyList<EmpireNameSuggestion> EmpireNames(DesignContext context, EmpireNameSources sources)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sources);

        var lists = Lists;
        var suggestions = new List<EmpireNameSuggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var format in _database.EmpireNameFormats)
        {
            if (!_evaluator.IsSatisfied(format.When, context))
            {
                continue;
            }

            // The prefix form as well as the main one: "Empire of Sol" and "Sol Empire" are both
            // names the game would give, and a player looking for theirs may have either.
            foreach (var template in new[] { format.Format, format.PrefixFormat })
            {
                if (template is not { Length: > 0 })
                {
                    continue;
                }

                foreach (var built in Build(template, sources, lists))
                {
                    if (seen.Add(built.Text))
                    {
                        suggestions.Add(built);
                    }
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Every adjective the game could give this empire, from the same shapes.
    /// </summary>
    /// <remarks>
    /// The formats carry their own, which is nearly always the species adjective and occasionally
    /// something else. Offered in place of the three guesses the designer used to make.
    /// </remarks>
    public IReadOnlyList<string> EmpireAdjectives(DesignContext context, EmpireNameSources sources)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sources);

        var lists = Lists;

        return
        [
            .. _database.EmpireNameFormats
                .Where(f => f.Adjective is { Length: > 0 } && _evaluator.IsSatisfied(f.When, context))
                .SelectMany(f => Build(f.Adjective!, sources, lists))
                .Select(s => s.Text)
                .Distinct(StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Fills one template out into every name it can make.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two shapes exist. <c>{AofB{&lt;list&gt; [This.GetSpeciesAdj]}}</c> names a localisation format
    /// and its blanks; anything else is a run of words to be joined with spaces. The braces inside
    /// the second sort group without meaning anything here, so they are simply dropped.
    /// </para>
    /// <para>
    /// Twenty of the game's own entries are written <c>{AofB &lt;list&gt; [call]}}</c> — a brace
    /// short — which leaves the format's name sitting among the words. A leading bare word is read
    /// as the format it plainly is rather than printed as though it were part of the name.
    /// </para>
    /// </remarks>
    private static IEnumerable<EmpireNameSuggestion> Build(
        string template,
        EmpireNameSources sources,
        IReadOnlyDictionary<string, EmpireNamePartsList> lists)
    {
        var match = FormatPattern().Match(template);
        var body = match.Success ? match.Groups[2].Value : template;

        var tokens = body.Replace('{', ' ').Replace('}', ' ').Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var key = match.Success ? match.Groups[1].Value : null;

        if (key is null && tokens is [{ } first, _, ..] && first[0] is not ('<' or '['))
        {
            key = first;
            tokens = tokens[1..];
        }

        // Each token stands for one or more words; a token that stands for none — an empty list, or
        // a name the empire has not been given — takes the whole shape with it.
        var choices = new List<IReadOnlyList<string>>();

        foreach (var token in tokens)
        {
            var words = Words(token, sources, lists);

            if (words.Count == 0)
            {
                yield break;
            }

            choices.Add(words);
        }

        foreach (var parts in Combinations(choices))
        {
            // The readable form is only for telling two suggestions apart here; what a player is
            // shown is built by the interface, which has the localiser these words need.
            var text = string.Join(
                ' ',
                parts.Select(p => p == Sem.Designs.LocRef.AdjectiveTemplate ? sources.SpeciesAdjective : p));

            yield return new EmpireNameSuggestion(text, key, parts);
        }
    }

    /// <summary>What one token of a template can stand for.</summary>
    private static IReadOnlyList<string> Words(
        string token,
        EmpireNameSources sources,
        IReadOnlyDictionary<string, EmpireNamePartsList> lists)
    {
        if (token.StartsWith('<') && token.EndsWith('>'))
        {
            return lists.TryGetValue(token[1..^1], out var list)
                ? [.. list.Parts.OrderByDescending(p => p.Weight).Select(p => p.Word)]
                : [];
        }

        if (token.StartsWith('[') && token.EndsWith(']'))
        {
            var call = token[1..^1];

            // The species adjective keeps its placeholder rather than becoming the word it stands
            // for. That is how the game stores such a name — the design carries %ADJECTIVE% and the
            // species it is made from — so a name built here can be written back the same way, and
            // reads correctly if the species is later renamed.
            if (call == "This.GetSpeciesAdj")
            {
                return sources.SpeciesAdjective is { Length: > 0 } ? [Sem.Designs.LocRef.AdjectiveTemplate] : [];
            }

            return sources.Resolve(call) is { Length: > 0 } value ? [value] : [];
        }

        return [token];
    }

    /// <summary>
    /// Every way of taking one word from each position.
    /// </summary>
    /// <remarks>
    /// Depth first, so the most typical word of every list comes first and the caps above keep the
    /// names the game would most often have chosen.
    /// </remarks>
    private static IEnumerable<IReadOnlyList<string>> Combinations(IReadOnlyList<IReadOnlyList<string>> choices)
    {
        if (choices.Count == 0)
        {
            yield break;
        }

        var indices = new int[choices.Count];

        while (true)
        {
            yield return [.. choices.Select((words, at) => words[indices[at]])];

            var position = choices.Count - 1;

            while (position >= 0 && ++indices[position] >= choices[position].Count)
            {
                indices[position] = 0;
                position--;
            }

            if (position < 0)
            {
                yield break;
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\{(\w+)\{(.*)\}\}$")]
    private static partial System.Text.RegularExpressions.Regex FormatPattern();

    /// <summary>
    /// Turns a species name into an adjective the way the game's naming rules do.
    /// </summary>
    /// <remarks>
    /// The game keeps no adjective alongside a species name; it rewrites the ending, longest match
    /// first, so that Jhabbanid becomes Jhabbanan and Alari becomes Alarian. A name matching nothing
    /// keeps its own form, which is what the game falls back to as well.
    /// </remarks>
    public static string Adjective(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        (string Ending, string Replacement)[] rules =
        [
            ("us", "an"), ("is", "an"), ("es", "an"), ("ss", "an"),
            ("id", "an"), ("ed", "an"), ("ad", "an"), ("od", "an"),
            ("ud", "an"), ("yd", "an"),
            ("i", "ian"), ("r", "ran"), ("a", "an"), ("e", "an"),
        ];

        foreach (var (ending, replacement) in rules)
        {
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(name.AsSpan(0, name.Length - ending.Length), replacement);
            }
        }

        return name;
    }

    /// <summary>
    /// Which name list a species should be named from.
    /// </summary>
    /// <remarks>
    /// A list may point at a different one for this purpose. The three human lists do, so that
    /// randomising a species for the United Nations of Earth offers ordinary human names rather than
    /// that empire's own conventions.
    /// </remarks>
    public string? SpeciesNameSourceFor(string? nameList) =>
        Resolve(nameList) is { RandomNameSource: { Length: > 0 } source } ? source : nameList;

    private NameListDefinition? Resolve(string? key) =>
        key is { Length: > 0 }
            ? _database.NameLists.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal))
            : null;

    private T? Pick<T>(IReadOnlyList<T> items) =>
        items.Count == 0 ? default : items[_random.Next(items.Count)];
}
