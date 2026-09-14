using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// One civic or origin, flattened into everything a page needs to draw and narrow it.
/// </summary>
/// <remarks>
/// <para>
/// The same shape <see cref="EmpireRow"/> is for an empire, and for the same reason: the wiki asks
/// twenty questions of three hundred and fifty-eight entries on every keystroke in the search box,
/// and answering each of them from the database and the localisation is a great deal of work to
/// repeat for a list that has not changed.
/// </para>
/// <para>
/// Everything here is already in <c>gamedb.json</c>. None of it needed extracting, which is the
/// point: a property added to the definition would bump the schema and send every desktop player
/// back through thirty-five thousand files to learn something the file they have already says.
/// </para>
/// </remarks>
public sealed record CivicRow
{
    /// <summary>The key the game and the design file both use.</summary>
    public required string Key { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>The prose the game shows under the name, which may be nothing.</summary>
    public required string Description { get; init; }

    /// <summary>Whether it is an origin rather than a civic.</summary>
    public required bool IsOrigin { get; init; }

    /// <summary>Its artwork, where the game has any.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// The larger picture an origin carries beside its icon.
    /// </summary>
    /// <remarks>
    /// Every origin has one and no civic does, which is most of why the two have pages of their own:
    /// a scene of the world the empire wakes up on is the thing an origin is presented by.
    /// </remarks>
    public string? Picture { get; init; }

    /// <summary>What it does, for the effects list to draw.</summary>
    public required EffectSet Effects { get; init; }

    /// <summary>Whether a player could ever be offered it, and what shuts them out if not.</summary>
    public required CivicReach Reach { get; init; }

    /// <summary>The content packs it is behind, as the game names them.</summary>
    public required IReadOnlyList<string> Packs { get; init; }

    /// <summary>Whether every pack it needs is one the reader has.</summary>
    public required bool Owned { get; init; }

    /// <summary>
    /// The five conditions the game states about it, each said in a sentence.
    /// </summary>
    /// <remarks>
    /// All five rather than the two that gate picking it. They answer different questions and the
    /// game keeps them apart: what you must own, what your empire must already be for it to appear,
    /// what makes it legal once it does, and whether a government reform can add it or take it away.
    /// A reader mid-game asking "can I still get this" is asking about the fourth.
    /// </remarks>
    public required IReadOnlyList<CivicCondition> Conditions { get; init; }

    /// <summary>
    /// Everything it is narrowed by that comes out of its conditions, as choices.
    /// </summary>
    /// <remarks>
    /// Read once here rather than walked per keystroke. Authorities, ethics, species archetypes and
    /// the civics it wants are all <c>SelectionRequirement</c>s in the same trees, so one walk
    /// answers four headings.
    /// </remarks>
    public required IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>> Wants { get; init; }

    /// <summary>The modifiers it touches, as choices named the way the effects list names them.</summary>
    public required IReadOnlyList<EmpireChoice> Bonuses { get; init; }

    /// <summary>
    /// Everything about it a typed word should match.
    /// </summary>
    /// <remarks>
    /// The name, the prose, and the key. The key because somebody who found <c>civic_fanatic_purifiers</c>
    /// in a save file or a mod has a word in mind that no screen ever shows them.
    /// </remarks>
    public required string Text { get; init; }
}

/// <summary>One of the conditions the game states about a civic, said in a sentence.</summary>
/// <param name="Heading">What the condition is about, in the reader's terms rather than the game's.</param>
/// <param name="Sentence">The condition itself, or nothing where it says nothing.</param>
public sealed record CivicCondition(string Heading, string? Sentence);

/// <summary>
/// Everything the game has to say about civics and origins, read once.
/// </summary>
/// <remarks>
/// <para>
/// The shelf as a whole rather than a row at a time, because two of the answers on a row are
/// answers about the shelf: whether a player could ever reach one depends on which others they can
/// reach, and what the filter offers under a heading is the union of what the rows hold.
/// </para>
/// <para>
/// Built once and held, for the reason <see cref="EmpireOptions"/> gives about itself: three
/// hundred and fifty-eight entries through five condition trees, a localisation lookup each, is not
/// work to repeat on every keystroke in a search box.
/// </para>
/// </remarks>
/// <param name="session">The session, for the game data and the writers that turn it into prose.</param>
public sealed class CivicShelf(DesignSession session)
{
    /// <summary>Every civic, in the order a reader would look for them.</summary>
    public IReadOnlyList<CivicRow> Civics => _civics ??= [.. All.Where(r => !r.IsOrigin)];

    /// <summary>Every origin, likewise.</summary>
    public IReadOnlyList<CivicRow> Origins => _origins ??= [.. All.Where(r => r.IsOrigin)];

    private IReadOnlyList<CivicRow>? _civics;
    private IReadOnlyList<CivicRow>? _origins;
    private IReadOnlyList<CivicRow>? _all;

    /// <summary>Both, which is what the reachability has to be worked out across.</summary>
    private IReadOnlyList<CivicRow> All => _all ??= Read();

    private IReadOnlyList<CivicRow> Read()
    {
        var database = session.Data.Database;
        var reach = CivicReach.Across(database.Civics);

        return
        [
            .. database.Civics
                .Select(c => Row(c, reach[c.Key]))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    private CivicRow Row(CivicDefinition civic, CivicReach reach)
    {
        var name = session.Localizer.Text(civic.NameKey, Localizer.Prettify(civic.Key));

        // The same convention OptionChip reads by, and all three hundred and fifty-eight are in the
        // extracted text under it. Not a property on the definition: adding one would be tidier and
        // would cost a schema bump, which every desktop player pays for by re-reading the game.
        var description = session.Localizer.Text($"{civic.Key}_desc", string.Empty);

        var packs = ContentPacks.Named(civic.Playable);

        return new CivicRow
        {
            Key = civic.Key,
            Name = name,
            Description = description,
            IsOrigin = civic.IsOrigin,
            Icon = civic.Icon,
            Picture = civic.Picture,
            Effects = civic.Effects,
            Reach = reach,
            Packs = packs,
            Owned = packs.All(session.OwnedDlc.Contains),
            Conditions = Conditions(civic),
            Wants = Wants(civic),
            Bonuses = Bonuses(civic),
            Text = $"{name} {description} {civic.Key}",
        };
    }

    /// <summary>
    /// The five trees, each said in a sentence and each under its own heading.
    /// </summary>
    /// <remarks>
    /// The headings are the definition's own doc comments turned into words a player would use. The
    /// game keeps the five apart and means something different by each, so running them together
    /// would lose the distinction that makes four of them worth reading: what you must own, what
    /// your empire must already be, what makes it legal, and the two halves of whether a reform can
    /// change its mind later.
    /// </remarks>
    private IReadOnlyList<CivicCondition> Conditions(CivicDefinition civic) =>
    [
        new("Needs", session.Conditions.Describe(civic.Playable)),
        new("Offered to", session.Conditions.Describe(civic.Potential)),
        new("Allowed when", session.Conditions.Describe(civic.Possible)),
        new("Can be added later", session.Conditions.Describe(civic.CanAddLater)),
        new("Can be dropped later", session.Conditions.Describe(civic.CanRemoveLater)),
    ];

    /// <summary>
    /// What the conditions ask an empire to be, as choices the filter can offer.
    /// </summary>
    /// <remarks>
    /// Potential and Possible together: the first decides whether it is drawn and the second whether
    /// it may be taken, and a reader asking "which civics want a militarist empire" does not care
    /// which of the two says so. What is asked against is left out - see <see cref="Selections"/>.
    /// </remarks>
    private IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>> Wants(CivicDefinition civic) =>
        new[] { civic.Potential, civic.Possible }
            .SelectMany(Selections.Required)
            .Where(s => Offered.Contains(s.Category))
            .DistinctBy(s => (s.Category, s.Key))
            .GroupBy(s => s.Category)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<EmpireChoice> (g) =>
                [
                    .. g.Select(s => new EmpireChoice(s.Key, Named(s), null, null))
                        .OrderBy(c => c.Name, StringComparer.CurrentCulture),
                ]);

    /// <summary>
    /// The four headings worth narrowing by, out of the eleven kinds of selection there are.
    /// </summary>
    /// <remarks>
    /// The rest are either answered by a heading of their own already - the content packs - or are
    /// questions about a game in progress rather than about a design, which is what a wiki reader
    /// has in front of them.
    /// </remarks>
    private static readonly IReadOnlySet<SelectionCategory> Offered = new HashSet<SelectionCategory>
    {
        SelectionCategory.Authority,
        SelectionCategory.Ethics,
        SelectionCategory.SpeciesArchetype,
        SelectionCategory.Civics,
    };

    private string Named(SelectionRequirement selection) =>
        session.Localizer.Text(selection.Key, Localizer.Prettify(selection.Key));

    /// <summary>
    /// Every modifier it touches, conditional ones included, named the way the effects list names
    /// them.
    /// </summary>
    /// <remarks>
    /// The conditional ones are in on purpose. A civic whose whole value is a swap for gestalt
    /// empires would otherwise say it grants nothing, and the trait budget already learned this the
    /// hard way: reading only the always-on modifiers "lost every bonus the game states inside a
    /// swap".
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Bonuses(CivicDefinition civic) =>
    [
        .. civic.Effects.Modifiers.Keys
            .Concat(civic.Effects.Conditional.SelectMany(c => c.Modifiers.Keys))
            .Distinct(StringComparer.Ordinal)
            .Select(key => new EmpireChoice(key, session.Modifiers.Label(key), null, null))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];
}
