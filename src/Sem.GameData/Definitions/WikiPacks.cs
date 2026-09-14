namespace Sem.GameData;

/// <summary>
/// What a wiki pack was built from, so a stale one can be refused rather than misread.
/// </summary>
/// <remarks>
/// A schema number of its own, per domain. The wiki will want a file for each of a great many
/// things - planets, shipsets, traditions, anomalies, events - and one number shared across all of
/// them would mean a change to any one re-versioning every other.
/// </remarks>
/// <param name="ExtractorVersion">Which build of the extractor wrote it.</param>
/// <param name="SchemaVersion">Which shape this domain's records are in.</param>
public sealed record WikiPackStamp(string ExtractorVersion, int SchemaVersion);

/// <summary>
/// A file of wiki data, fetched when a page needs it and not before.
/// </summary>
/// <remarks>
/// <para>
/// The empire designer's data is one file that every visitor fetches before anything can be drawn.
/// Most of what the wiki wants is of no use to a designer at all - the game's leader traits are
/// about seven hundred records nothing in an empire can hold - so it is kept out of that file and
/// put in one of its own, per domain, fetched on demand.
/// </para>
/// <para>
/// This is the same arrangement <c>wardrobe.json</c> already uses, generalised. Its own note on
/// <c>IGameDataSource</c> puts the principle better than a restatement would: kept apart from the
/// rest and fetched only when something asks, because reading it in a browser is not free and
/// nobody who never opens the page should pay for it.
/// </para>
/// </remarks>
public interface IWikiPack
{
    /// <summary>What built it.</summary>
    WikiPackStamp Stamp { get; }

    /// <summary>
    /// Which shape this kind of pack is expected to be in, so a stale file can be told from a good
    /// one without the loader knowing what any particular domain holds.
    /// </summary>
    static abstract int ExpectedSchemaVersion { get; }
}

/// <summary>
/// One of the game's leader traits, as a page about them needs it.
/// </summary>
/// <remarks>
/// <para>
/// Its own record rather than <see cref="TraitDefinition"/>, because a leader trait is a different
/// shape: it has no archetypes, no species classes, no planet or portrait restrictions, and it does
/// have four things a species trait has no notion of - which classes may hold it, what sort it is,
/// which tier of its own chain it sits at, and which trait it replaces on the way up.
/// </para>
/// <para>
/// Kept here rather than folded into the database, for the rule the architecture notes give: a
/// property added to a definition every empire reads costs a schema bump, and a schema bump sends
/// every desktop player back through thirty-five thousand files.
/// </para>
/// </remarks>
/// <param name="Key">The trait's own key, such as <c>leader_trait_carefree</c>.</param>
public sealed record LeaderTraitDefinition(string Key)
{
    /// <summary>
    /// Whether an empire may be designed holding this one.
    /// </summary>
    /// <remarks>
    /// True for thirty-four of them. They are not a separate kind of thing - each declares a
    /// leader class like any other leader trait and carries <c>starting_ruler_trait</c> on top - so
    /// they are carried here as well as in the database, where the ruler's picker reads them. Twenty
    /// -four are additionally marked <c>initial = no</c>, which means "not offered unconditionally"
    /// rather than "never offered": the game gates them on an origin and offers them once it is
    /// picked. So all thirty-four answer yes here, and the origin is a fact beside it.
    /// </remarks>
    public bool CanStart { get; init; }

    /// <summary>Which leader classes may hold it: commander, official, scientist.</summary>
    /// <remarks>
    /// A hundred and thirty-four of the game's traits write this as a bare word rather than a list,
    /// so both forms are read. Empty means any class.
    /// </remarks>
    public IReadOnlyList<string> LeaderClasses { get; init; } = [];

    /// <summary>What sort it is: <c>veteran</c>, <c>negative</c>, <c>destiny</c> or <c>subclass</c>.</summary>
    public string? Sort { get; init; }

    /// <summary>How rare it is, as its own icon recipe describes it.</summary>
    public string? Rarity { get; init; }

    /// <summary>
    /// Which tier of its chain this is, counting from one.
    /// </summary>
    /// <remarks>
    /// Zero where the trait names none. This matters more than it looks: two hundred and thirty-four
    /// of the game's leader traits have no name in any language it ships, being the second and third
    /// tiers a leader earns while a game is running, and the only way to give one a heading is to
    /// follow <see cref="Replaces"/> back to the tier that does have a name.
    /// </remarks>
    public int Tier { get; init; }

    /// <summary>The traits this one supersedes, which is how a tier chain is written down.</summary>
    public IReadOnlyList<string> Replaces { get; init; } = [];

    /// <summary>Traits that cannot be held alongside this one.</summary>
    public IReadOnlyList<string> Opposites { get; init; } = [];

    /// <summary>Which downloadable content pack it needs, if any.</summary>
    public string? RequiredDlc { get; init; }

    /// <summary>What it does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to its icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>
/// Every leader trait the game defines, and the text they are written in.
/// </summary>
/// <remarks>
/// The text travels with the records because <c>loc/en.json</c> cannot carry it. That file is pruned
/// to what the database reaches - forty-one thousand entries kept out of the game's hundred and
/// fifty thousand - and a leader trait is not in the database, so its name is not in there and never
/// will be. Widening the pruner would put seven hundred names in front of every visitor to pay for;
/// carrying them here means only a reader who opens the page does.
/// </remarks>
public sealed record LeaderTraitPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "leader-traits";

    /// <summary>
    /// The shape these records are in, bumped when it changes.
    /// </summary>
    /// <remarks>
    /// Two: the traits an empire may start with joined the file, and brought <c>CanStart</c> with
    /// them. Read at one, every trait would answer "cannot start" by default and the column would
    /// be a quiet lie, so a file of the older shape is refused rather than believed.
    /// </remarks>
    public const int CurrentSchemaVersion = 2;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The traits, in the order the game declares them.</summary>
    public IReadOnlyList<LeaderTraitDefinition> Traits { get; init; } = [];

    /// <summary>The names and descriptions these traits are written in.</summary>
    public IReadOnlyDictionary<string, string> Text { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
