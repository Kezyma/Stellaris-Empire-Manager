namespace Sem.GameData;

/// <summary>
/// One ascension perk: something a game grants over time, and a plan may say it means to take.
/// </summary>
/// <remarks>
/// The game has no name for a perk apart from its key - the key is the localisation entry - and no
/// icon field either; the picture is a sprite named after it, which is not always named after the
/// file it draws.
/// </remarks>
public sealed record AscensionPerkDefinition(string Key)
{
    /// <summary>Whether it appears in the list at all, which is mostly a check on owning content.</summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether it may be taken given everything else. A perk failing this is shown but blocked,
    /// with the game's own explanation of why.
    /// </summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>The game's own grouping, which is how the ascension paths are known.</summary>
    public string? Category { get; init; }

    /// <summary>What it does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Where its picture was written, if it has one.</summary>
    public string? Icon { get; init; }

    /// <summary>What this is called and said to be for particular kinds of empire.</summary>
    public IReadOnlyList<OptionVariant> Variants { get; init; } = [];

    /// <summary>The key is the name, which is unusual and is the game's doing.</summary>
    public string NameKey => Key;

    /// <summary>And the description hangs off it.</summary>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>
/// One tradition inside a tree: a pick, or the bonus for opening or finishing it.
/// </summary>
/// <remarks>
/// Read because a tree is only worth taking for what is inside it, and a plan naming a tree should
/// be able to say what that is. The description is keyed on the tradition plus <c>_delayed</c>,
/// which is the game's own convention and is stated in its README beside the files.
/// </remarks>
public sealed record TraditionDefinition(string Key)
{
    /// <summary>The tree it belongs to.</summary>
    public string? Tree { get; init; }

    /// <summary>What it does, and when.</summary>
    /// <remarks>
    /// The base block and the swaps both. A swap replaces the tradition for an empire of another
    /// shape rather than restating it - Prosperity gives station output normally and three quite
    /// different things to a nomad - so each is read as the alternative it is.
    /// </remarks>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>
    /// Whether this one may be taken.
    /// </summary>
    /// <remarks>
    /// Read for the sake of the adoption bonuses, which is where the game says what unlocks a tree:
    /// nothing on the tree itself asks for the ascension perk, and <c>tr_cybernetics_adopt</c> does
    /// - "the flesh is weak, and the technology, unless your origin already put you there". A tree
    /// whose adoption cannot be taken is a tree that cannot be opened.
    /// </remarks>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>Where its picture was written, if it has one.</summary>
    public string? Icon { get; init; }

    /// <summary>What this is called and said to be for particular kinds of empire.</summary>
    public IReadOnlyList<OptionVariant> Variants { get; init; } = [];

    /// <summary>The key is the name.</summary>
    public string NameKey => Key;

    /// <summary>
    /// What the game wrote about it, where it wrote anything.
    /// </summary>
    /// <remarks>
    /// It used to be the key with <c>_delayed</c> after it, which is the game's own convention and
    /// is stated in the README beside the files - but only a hundred and sixty-one of the two
    /// hundred and thirty-four traditions have such an entry. Nineteen have a plain <c>_desc</c> and
    /// fifty-four, the adoption and completion bonuses mostly, have nothing at all. The other
    /// seventy-three were drawn as the key tidied up, so a reader opening the Adaptability tree was
    /// told its opening bonus is "Tr Adaptability Adopt Delayed".
    ///
    /// Settled during extraction, which is where the text is, rather than guessed at twice.
    /// </remarks>
    public string? DescriptionKey { get; init; }
}

/// <summary>
/// One tradition tree, which is what a plan names rather than the five picks inside it.
/// </summary>
/// <remarks>
/// The game's own exclusions between trees are written against the tradition that opens one - the
/// adoption bonus - rather than against the tree, so anything asking whether a tree is open has to
/// know both names.
/// </remarks>
public sealed record TraditionTreeDefinition(string Key)
{
    /// <summary>Whether an empire of this shape is offered the tree at all.</summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>The tradition taken to open it, which is how the game asks whether it is open.</summary>
    public string? AdoptionBonus { get; init; }

    /// <summary>And the one taken to finish it.</summary>
    public string? FinishBonus { get; init; }

    /// <summary>The picks inside, kept for counting and for whatever wants them later.</summary>
    public IReadOnlyList<string> Traditions { get; init; } = [];

    /// <summary>Where its picture was written, if it has one.</summary>
    public string? Icon { get; init; }

    /// <summary>The key is the name, as it is for a perk.</summary>
    public string NameKey => Key;

    /// <summary>And the description hangs off it.</summary>
    public string DescriptionKey => $"{Key}_desc";
}
