using System.Collections.Frozen;
using System.Reflection;
using System.Text;

namespace Sem.Ui.Services;

/// <summary>
/// The vocabulary a packed design is written against: every trait, civic, room, portrait and
/// generated name the game offers, in a fixed order.
/// </summary>
/// <remarks>
/// <para>
/// A link stores a choice as its position in one of these lists, which is what makes a link short:
/// <c>trait_rapid_breeders</c> costs twenty-one characters written out and nine bits as a position.
/// Measured over the eighteen empires in the corpus, the vocabulary is a quarter of what a design
/// says and the format's own punctuation is another half, so between them they are most of a link.
/// </para>
/// <para>
/// The order is therefore frozen, and the file says so at the top. Appending is safe and is what
/// happens when the game adds a trait; moving or removing an entry silently repoints every link
/// ever shared, which is why <c>tools/build-link-dictionary.py</c> merges rather than regenerates.
/// </para>
/// <para>
/// It travels in the assembly rather than in the game data. That looks like duplication — every
/// one of these keys is also in <c>gamedb.json</c>, which the app downloads anyway — and it is the
/// whole point: <c>gamedb.json</c> is re-extracted whenever the game changes, and a table that
/// moves when the game moves is a table that cannot decode last year's link. A key the game has
/// since dropped still has to come back, so its letters have to be here rather than looked up.
/// </para>
/// <para>
/// Around a hundred kilobytes of text, twenty-nine compressed, against a five-megabyte database.
/// </para>
/// </remarks>
internal static class LinkDictionary
{
    /// <summary>
    /// The lists, in wire order. An index into this array is what a link stores alongside the
    /// index into the list itself, so this order is frozen too.
    /// </summary>
    internal static readonly string[] Groups =
    [
        "species_trait", "leader_trait", "civic", "origin", "ethic", "authority", "government",
        "planet_class", "room", "name_list", "portrait", "graphical_culture", "advisor_voice",
        "initializer", "flag_category", "flag_colour", "leader_class", "ship_set", "arkship",
        "species_class", "flag_set", "species_stem", "name_word", "name_key", "flag_file", "field",
        "token", "char_prefix", "char_suffix", "shape",
    ];

    /// <summary>Where the field names live, which the key of a node is looked up in.</summary>
    internal const int FieldGroup = 25;

    /// <summary>Where the species stems live, which stand in for four keys apiece.</summary>
    internal const int SpeciesStemGroup = 21;

    /// <summary>
    /// The first half of a name picked from the ruler-name dropdown, which is how most rulers are
    /// named. Writing this group is what says a suffix follows.
    /// </summary>
    internal const int CharPrefixGroup = 27;

    /// <summary>The second half of one, held apart so 10,076 keys cost 7,736 entries.</summary>
    internal const int CharSuffixGroup = 28;

    /// <summary>
    /// The four shapes a species name key takes, in the order a two-bit suffix names them.
    /// </summary>
    /// <remarks>
    /// A design that names a generated species writes all four — <c>SPEC_Oxanalytor</c>, its
    /// plural, its home planet and its home system. One stem and two bits says any of them, which
    /// turns two thousand keys into five hundred entries.
    /// </remarks>
    internal static readonly string[] StemSuffixes = ["", "_pl", "_planet", "_system"];

    private static readonly Lazy<Loaded> Table = new(Load, isThreadSafe: true);

    private sealed record Loaded(
        string[][] Entries,
        FrozenDictionary<string, (int Group, int Index)> Lookup,
        FrozenDictionary<string, string[]> Shapes);

    /// <summary>How many entries a group holds, which is what sets the width of an index.</summary>
    internal static int Count(int group) => Table.Value.Entries[group].Length;

    /// <summary>The text at a position, or nothing when the link names one this version lacks.</summary>
    internal static string? At(int group, int index)
    {
        if (group < 0 || group >= Table.Value.Entries.Length)
        {
            return null;
        }

        var entries = Table.Value.Entries[group];

        return index >= 0 && index < entries.Length ? entries[index] : null;
    }

    /// <summary>Where a piece of text sits, or nothing when the table has never heard of it.</summary>
    /// <remarks>
    /// Nothing is the ordinary answer for an empire the player named themselves, and for anything a
    /// mod adds. Both are written out as themselves instead, which costs length and not correctness.
    /// </remarks>
    internal static bool TryFind(string text, out int group, out int index)
    {
        if (Table.Value.Lookup.TryGetValue(text, out var found))
        {
            (group, index) = found;
            return true;
        }

        (group, index) = (-1, -1);
        return false;
    }

    /// <summary>
    /// Splits a species name key into the stem the table holds and which of its four forms it is.
    /// </summary>
    internal static bool TryFindSpecies(string text, out int index, out int suffix)
    {
        // Longest suffix first, or "_pl" would match inside nothing but "_planet" would be read as
        // a stem ending in "_pl" followed by "anet".
        for (var candidate = StemSuffixes.Length - 1; candidate >= 1; candidate--)
        {
            var ending = StemSuffixes[candidate];

            if (text.EndsWith(ending, StringComparison.Ordinal)
                && TryFind(text[..^ending.Length], out var group, out index)
                && group == SpeciesStemGroup)
            {
                suffix = candidate;
                return true;
            }
        }

        if (TryFind(text, out var plain, out index) && plain == SpeciesStemGroup)
        {
            suffix = 0;
            return true;
        }

        (index, suffix) = (-1, 0);
        return false;
    }

    /// <summary>
    /// Splits a dropdown-picked name into the list it came from and the name within it.
    /// </summary>
    /// <remarks>
    /// <c>AVI3_CHR_Feathers_of</c> is one of ten thousand such keys and twenty-one characters
    /// written out. As a pair of positions it is twenty bits, and the table it is looked up in is a
    /// third the size it would be holding the keys whole.
    /// </remarks>
    internal static bool TryFindCharacter(string text, out int prefix, out int suffix)
    {
        foreach (var mark in CharacterMarks)
        {
            var at = text.IndexOf(mark, StringComparison.Ordinal);

            if (at < 0)
            {
                continue;
            }

            var cut = at + mark.Length;

            if (TryFind(text[..cut], out var head, out prefix) && head == CharPrefixGroup
                && TryFind(text[cut..], out var tail, out suffix) && tail == CharSuffixGroup)
            {
                return true;
            }
        }

        (prefix, suffix) = (-1, -1);
        return false;
    }

    private static readonly string[] CharacterMarks = ["_CHR_", "_CHA_", "_SHP_"];

    /// <summary>The name the design's own block is looked up under, since it has no fixed one.</summary>
    internal const string RootShape = "<empire>";

    /// <summary>What a node with this key may hold, or nothing where that is not written down.</summary>
    /// <remarks>
    /// A design is ninety-three nodes and nearly every one is one of a handful of things its parent
    /// can hold, so naming it among those costs two or three bits where naming it among all
    /// fifty-nine fields costs seven. A parent that is not here, or a child that is not in its
    /// list, falls back to the field table and then to its own letters.
    /// </remarks>
    internal static string[]? Shape(string? parent) =>
        parent is not null && Table.Value.Shapes.TryGetValue(parent, out var children) ? children : null;

    private static Loaded Load()
    {
        var lists = new List<string>[Groups.Length];

        for (var i = 0; i < lists.Length; i++)
        {
            lists[i] = [];
        }

        using var stream = typeof(LinkDictionary).GetTypeInfo().Assembly
            .GetManifestResourceStream("Sem.Ui.Services.LinkDictionary.v2.txt")
            ?? throw new InvalidOperationException("The link dictionary is missing from the assembly.");

        using var reader = new StreamReader(stream, Encoding.UTF8);

        var group = -1;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line[0] == '[')
            {
                group = Array.IndexOf(Groups, line[1..^1]);
                continue;
            }

            if (group >= 0)
            {
                lists[group].Add(line);
            }
        }

        var entries = lists.Select(l => l.ToArray()).ToArray();

        // First writing wins, so a word that is both a name part and something else keeps the
        // group it was found in first. Which group a piece of text belongs to is written into the
        // link, so the encoder and the decoder only have to agree, not to be canonical.
        var lookup = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

        for (var g = 0; g < entries.Length; g++)
        {
            for (var i = 0; i < entries[g].Length; i++)
            {
                lookup.TryAdd(entries[g][i], (g, i));
            }
        }

        // "parent>child|child|child", which is the one group whose lines are not themselves the
        // vocabulary but a statement about it.
        var shapes = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var line in entries[Array.IndexOf(Groups, "shape")])
        {
            var split = line.IndexOf('>', StringComparison.Ordinal);

            if (split > 0)
            {
                shapes[line[..split]] = line[(split + 1)..].Split('|');
            }
        }

        return new Loaded(
            entries,
            lookup.ToFrozenDictionary(StringComparer.Ordinal),
            shapes.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
