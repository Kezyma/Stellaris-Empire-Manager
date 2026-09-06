namespace Sem.Ui.Services;

/// <summary>
/// What a name written in a plan is allowed to mean, for one empire.
/// </summary>
/// <remarks>
/// <para>
/// Scoped to one empire on purpose, and it is what makes reading a plan back by name safe at all.
/// Several of the game's own things share a display name in every language it ships: the two
/// cybernetics trees are both "Cybernetics", and the four Galactic Wonders perks are all "Galactic
/// Wonders". Looked up in a flat list those are ambiguous. Looked up among what one empire may
/// actually take they are not, because every such family is mutually exclusive by content pack or by
/// what kind of empire it is.
/// </para>
/// <para>
/// So a name that cannot be resolved here is not a name this empire could have meant, and the right
/// answer is nothing rather than a guess.
/// </para>
/// </remarks>
public sealed class PlanVocabulary
{
    private readonly Func<IEnumerable<(string Key, string Name)>> _offeredTrees;
    private readonly Func<IEnumerable<(string Key, string Name)>> _offeredPerks;
    private readonly Func<IEnumerable<(string Key, string Name)>> _offeredCivics;

    private Dictionary<string, string>? _trees;
    private Dictionary<string, string>? _perks;
    private Dictionary<string, string>? _civics;

    /// <summary>
    /// Builds the vocabulary from what the pickers would offer this empire.
    /// </summary>
    /// <remarks>
    /// Asked for rather than handed over, and indexed on the first name looked up. One of these
    /// belongs to every empire, and the list a table draws holds fifty - but working out what an
    /// empire may take means evaluating three hundred options against it, and almost no empire has
    /// a plan at all. A biography with nothing in it never asks a question, so it never pays.
    /// </remarks>
    /// <param name="trees">The tradition trees on offer, as key and display name.</param>
    /// <param name="perks">The ascension perks on offer, as key and display name.</param>
    /// <param name="civics">The civics a government reform could reach, on the same terms.</param>
    public PlanVocabulary(
        Func<IEnumerable<(string Key, string Name)>> trees,
        Func<IEnumerable<(string Key, string Name)>> perks,
        Func<IEnumerable<(string Key, string Name)>> civics)
    {
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentNullException.ThrowIfNull(perks);
        ArgumentNullException.ThrowIfNull(civics);

        _offeredTrees = trees;
        _offeredPerks = perks;
        _offeredCivics = civics;
    }

    /// <summary>
    /// The same, from lists already in hand.
    /// </summary>
    /// <remarks>
    /// For a caller that has the names and nothing to defer - which is every test, and the empty
    /// one below. The lazy form exists for the empires in a list, where working out what each may
    /// take is the expensive part and almost none of them will be asked.
    /// </remarks>
    public PlanVocabulary(
        IEnumerable<(string Key, string Name)> trees,
        IEnumerable<(string Key, string Name)> perks,
        IEnumerable<(string Key, string Name)> civics)
        : this(() => trees, () => perks, () => civics)
    {
    }

    /// <summary>Nothing on offer, which is what an empire has before any of it is extracted.</summary>
    public static PlanVocabulary Empty { get; } = new(() => [], () => [], () => []);

    /// <summary>The tradition tree that name means, or nothing if it means none of them.</summary>
    public string? Tree(string name) => Look(_trees ??= Index(_offeredTrees()), name);

    /// <summary>The ascension perk that name means, or nothing if it means none of them.</summary>
    public string? Perk(string name) => Look(_perks ??= Index(_offeredPerks()), name);

    /// <summary>The civic that name means, or nothing if it means none of them.</summary>
    public string? Civic(string name) => Look(_civics ??= Index(_offeredCivics()), name);

    /// <summary>
    /// Names to keys, keeping the first where two things somehow still share a name.
    /// </summary>
    /// <remarks>
    /// Compared by the current culture and ignoring case, because a name is display text in the
    /// player's own language and somebody who typed it in lower case meant it. The dictionary is
    /// built once per reading rather than per name, since a plan names up to sixteen things and the
    /// offer behind it can run to fifty.
    /// </remarks>
    private static Dictionary<string, string> Index(IEnumerable<(string Key, string Name)> items)
    {
        var index = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var (key, name) in items)
        {
            if (name is { Length: > 0 })
            {
                index.TryAdd(name.Trim(), key);
            }
        }

        return index;
    }

    private static string? Look(Dictionary<string, string> index, string name) =>
        name is { Length: > 0 } && index.TryGetValue(name.Trim(), out var key) ? key : null;
}
