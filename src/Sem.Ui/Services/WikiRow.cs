using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// One thing the game defines, flattened into everything a page needs to draw and narrow it.
/// </summary>
/// <remarks>
/// <para>
/// A civic, an origin, an ethic, an authority - and in time an anomaly or an astral rift. They are
/// very different records and they are read the same way, because a reader asks the same things of
/// each: what is it, what does it do, who may have it, and what does it cost me in packs.
/// </para>
/// <para>
/// What differs between them is carried in <see cref="Facts"/> rather than in a field each. An ethic
/// has a cost and an opposite; an authority has elections and an heir; a civic has neither. Adding a
/// field per kind would give every row three-quarters empty and every page a reason to know about
/// the others.
/// </para>
/// <para>
/// Everything here is already in <c>gamedb.json</c>. None of it needed extracting, which is the
/// point: a property added to a definition would bump the schema and send every desktop player back
/// through thirty-five thousand files to learn something the file they have already says.
/// </para>
/// </remarks>
public sealed record WikiRow
{
    /// <summary>The key the game uses, which is also what a filter remembers.</summary>
    public required string Key { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>The prose the game shows under the name, which may be nothing.</summary>
    public required string Description { get; init; }

    /// <summary>Where that prose lives, so a chip can open the same panel a picker would.</summary>
    public required string DescriptionKey { get; init; }

    /// <summary>Its artwork, where the game has any.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// The larger scene it carries beside its icon.
    /// </summary>
    /// <remarks>
    /// Origins only, and sixty-six of the seventy-seven. It is most of how the game presents an
    /// origin - the world the empire wakes up on - and nothing else in the wiki has one.
    /// </remarks>
    public string? Picture { get; init; }

    /// <summary>What it does, for the effects list to draw.</summary>
    public required EffectSet Effects { get; init; }

    /// <summary>Whether a player could ever be offered it.</summary>
    public required bool Playable { get; init; }

    /// <summary>Two words for a badge saying it is out of reach, or nothing where it is not.</summary>
    public string? ClosedShort { get; init; }

    /// <summary>The whole of why, for the tip behind the badge.</summary>
    public string? ClosedWhy { get; init; }

    /// <summary>
    /// The content packs it depends on, each with whether it must be owned or must not be.
    /// </summary>
    /// <remarks>
    /// Both directions, because the game writes both. Corporate Dominion is the civic for an
    /// oligarchy that <em>cannot</em> be a megacorp, and its whole condition is
    /// <c>NOT = { has_dlc = Megacorp }</c> - so read one way round it wore a badge telling you to
    /// buy the one pack that takes it away.
    /// </remarks>
    public required IReadOnlyList<WikiPack> Packs { get; init; }

    /// <summary>Whether what the reader owns satisfies every one of those gates.</summary>
    public required bool Owned { get; init; }

    /// <summary>
    /// The same packs as choices, for the heading that narrows by them.
    /// </summary>
    /// <remarks>
    /// Built once here rather than mapped where it is read. The filter asks every row for this on
    /// every keystroke in the search box, and a list built inside that call is hundreds of
    /// allocations per letter typed for a list that never changes.
    /// </remarks>
    public required IReadOnlyList<EmpireChoice> PackChoices { get; init; }

    /// <summary>
    /// What an empire has to be for this, in whichever senses the game distinguishes for its kind.
    /// </summary>
    /// <remarks>
    /// A civic keeps two apart - what makes it appear at all against what then makes it legal - an
    /// authority states only the second, and an ethic states none. Whatever a kind does not state
    /// simply is not in the list, and the column for it is not drawn.
    /// </remarks>
    public required IReadOnlyList<WikiCondition> Conditions { get; init; }

    /// <summary>
    /// What is true of this one that is not true of every kind: a cost, an opposite, an election.
    /// </summary>
    /// <remarks>
    /// The headings are the same for every row of a kind and different between kinds, so the table
    /// takes its columns from them and the card draws them as a list.
    /// </remarks>
    public required IReadOnlyList<WikiFact> Facts { get; init; }

    /// <summary>Everything it asks an empire to be, by kind of selection, as choices.</summary>
    public required IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>> Wants { get; init; }

    /// <summary>The modifiers it touches, as choices named the way the effects list names them.</summary>
    public required IReadOnlyList<EmpireChoice> Bonuses { get; init; }

    /// <summary>
    /// Everything about it a typed word should match.
    /// </summary>
    /// <remarks>
    /// The name, the prose, and the key. The key because somebody who found
    /// <c>civic_fanatic_purifiers</c> in a save file or a mod has a word in mind that no screen ever
    /// shows them.
    /// </remarks>
    public required string Text { get; init; }

    /// <summary>What it asks an empire to be under one heading, or nothing where it asks nothing.</summary>
    /// <param name="category">Which kind of selection.</param>
    /// <returns>The choices it wants, which may be none.</returns>
    public IReadOnlyList<EmpireChoice> Wanting(SelectionCategory category) =>
        Wants.TryGetValue(category, out var wanted) ? wanted : [];

    /// <summary>What one of its facts says, or nothing where it has no such fact.</summary>
    /// <param name="heading">Which fact.</param>
    /// <returns>The fact, or null.</returns>
    public WikiFact? Fact(string heading) =>
        Facts.FirstOrDefault(f => string.Equals(f.Heading, heading, StringComparison.Ordinal));
}

/// <summary>
/// One of the conditions the game states about something.
/// </summary>
/// <param name="Heading">What the condition is about, in the reader's terms rather than the game's.</param>
/// <param name="Outline">
/// The condition itself, as nested parts to indent - or nothing where the game states none.
/// </param>
/// <param name="Otherwise">What to say in its place where it states none.</param>
public sealed record WikiCondition(string Heading, ConditionOutline? Outline, string Otherwise)
{
    /// <summary>Whether the game states a condition at all, so a page can draw the two apart.</summary>
    public bool Stated => Outline is not null;
}

/// <summary>
/// A content pack something depends on, ready to draw.
/// </summary>
/// <param name="Key">The pack as the game names it, which is what a filter remembers.</param>
/// <param name="Name">What the pack is called on screen.</param>
/// <param name="Icon">Its badge, the one the pack bar wears.</param>
/// <param name="Wanted">True where it must be owned, false where owning it rules this out.</param>
/// <param name="Held">Whether the reader owns it, which is not the same as whether that suits.</param>
public sealed record WikiPack(string Key, string Name, string? Icon, bool Wanted, bool Held)
{
    /// <summary>Whether this one gate is satisfied by what the reader owns.</summary>
    public bool Satisfied => Held == Wanted;
}

/// <summary>
/// Something true of one kind of thing and not of the others.
/// </summary>
/// <remarks>
/// Either a set of chips or a word, never both. An ethic's opposite is a thing with a name and a
/// picture and belongs in the same chip it wears everywhere else; an ethic's cost is a number.
/// </remarks>
/// <param name="Heading">What it answers, which is also the table's column.</param>
/// <param name="Chips">The things it names, where it names things.</param>
/// <param name="Text">What it says, where it says a word.</param>
public sealed record WikiFact(string Heading, IReadOnlyList<EmpireChoice> Chips, string? Text)
{
    /// <summary>A fact that names things.</summary>
    /// <param name="heading">What it answers.</param>
    /// <param name="chips">The things.</param>
    /// <returns>The fact.</returns>
    public static WikiFact Of(string heading, IReadOnlyList<EmpireChoice> chips) => new(heading, chips, null);

    /// <summary>A fact that says a word.</summary>
    /// <param name="heading">What it answers.</param>
    /// <param name="text">The word.</param>
    /// <returns>The fact.</returns>
    public static WikiFact Said(string heading, string text) => new(heading, [], text);

    /// <summary>Whether it says anything at all, so an empty one can be left undrawn.</summary>
    public bool Any => Chips.Count > 0 || Text is { Length: > 0 };
}
