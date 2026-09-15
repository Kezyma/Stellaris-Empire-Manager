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

/// <summary>One drawn ship: which class it is, and where its picture went.</summary>
/// <param name="ShipClass">The ship size's key, such as <c>battleship</c>.</param>
/// <param name="Image">Where the picture lives within the extracted assets.</param>
public sealed record ShipsetShip(string ShipClass, string Image);

/// <summary>What one appearance set flies.</summary>
/// <remarks>
/// A set that models no ships of its own flies its fallback's, so two sets can name the same
/// pictures. That is the game's arrangement rather than a duplicate: Solarpunk has no hulls anywhere
/// and is flown with fungoid ones.
/// </remarks>
/// <param name="Set">The graphical culture's key.</param>
public sealed record ShipsetFleet(string Set)
{
    /// <summary>Its ships, in the order the game declares the classes.</summary>
    public IReadOnlyList<ShipsetShip> Ships { get; init; } = [];
}

/// <summary>
/// Every ship each appearance set flies, drawn.
/// </summary>
/// <remarks>
/// <para>
/// Kept out of the database for the usual reason: the designer's picker wants one picture per set
/// and has a field for it, and a hundred and forty renders across eighteen classes is a page's
/// business rather than an empire's. Adding it to a definition every empire reads would cost a
/// schema bump, which sends every desktop player back through thirty-five thousand files.
/// </para>
/// <para>
/// The text is here for the same reason it is on the leader traits: <c>loc/en.json</c> is pruned to
/// what the database reaches, and a ship class is not in the database, so "Corvette" is not in
/// there and never will be.
/// </para>
/// </remarks>
public sealed record ShipsetPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "shipsets";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>What each set flies.</summary>
    public IReadOnlyList<ShipsetFleet> Fleets { get; init; } = [];

    /// <summary>The names the ship classes are written in.</summary>
    public IReadOnlyDictionary<string, string> Text { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// How one AI personality plays, beyond which empires are drawn it.
/// </summary>
/// <remarks>
/// <para>
/// The game documents every one of these at the top of its own file, in a comment block longer than
/// most of the personalities under it - what aggressiveness does to a war declaration, what
/// trade willingness means at 1.0, what each behaviour flag decides. None of it was read, so the
/// page said a personality's name, its odds and nothing about how it behaves.
/// </para>
/// <para>
/// Kept in the wiki's own file for the usual reason: an empire being designed has no AI, so none of
/// this belongs on a record every visitor to the designer fetches.
/// </para>
/// </remarks>
/// <param name="Key">The personality, as the game names it.</param>
public sealed record PersonalityDetail(string Key)
{
    /// <summary>
    /// What it will do, as the flags it answers yes to.
    /// </summary>
    /// <remarks>
    /// Only the yesses. The game writes both - fifty of the fifty-one say whether they conquer - and
    /// a chip saying "will not enslave" beside twelve others is a list of everything a personality
    /// is not. What is left out is what it will not do.
    /// </remarks>
    public IReadOnlyList<string> Behaviours { get; init; } = [];

    /// <summary>How it carries itself: aggressiveness, bravery, what it spends on.</summary>
    public IReadOnlyDictionary<string, double> Attitude { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>What it will sign, as the number added to its chance of accepting each.</summary>
    public IReadOnlyDictionary<string, double> Diplomacy { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>What it builds its ships out of, as the share of each it aims for.</summary>
    public IReadOnlyDictionary<string, double> Fleet { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>And what it arms them with, which the game names and localises.</summary>
    public string? Weapons { get; init; }
}

/// <summary>
/// Every AI personality, as a page about them needs them.
/// </summary>
/// <remarks>
/// The text travels with the records because <c>loc/en.json</c> is pruned to what the database
/// reaches, and the weapon types these name are reached by nothing in it.
/// </remarks>
public sealed record PersonalityPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "personalities";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The personalities, in the order the game declares them.</summary>
    public IReadOnlyList<PersonalityDetail> Personalities { get; init; } = [];

    /// <summary>The names the weapon types are written in.</summary>
    public IReadOnlyDictionary<string, string> Text { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// One sentence the game writes about why a pop leans toward an ethic, or away from it.
/// </summary>
/// <remarks>
/// Carried as the game's own words rather than as the trigger beside them, which is the one place
/// this family departs from the shape the rest of the extractor uses. The two say the same thing and
/// only one of them says it in English: the trigger is <c>is_at_war = yes</c> and the sentence is
/// "Empire is at war", already written, already localised, already carrying the sign and the icon.
/// Compiling the trigger as well would put "at war yes" beside it and call that an improvement.
/// </remarks>
/// <param name="DescriptionKey">Where the sentence is written.</param>
/// <param name="Draws">
/// Whether it pulls pops toward the ethic rather than away from it, which the game says in the
/// colour it writes the sentence in - green and a plus, or red and a minus.
/// </param>
public sealed record EthicDrift(string DescriptionKey, bool Draws);

/// <summary>What an ethic does about pops, which is most of what an ethic is for.</summary>
/// <param name="Key">The ethic's own key.</param>
public sealed record EthicDetail(string Key)
{
    /// <summary>Every sentence the game writes about drifting toward it or away.</summary>
    public IReadOnlyList<EthicDrift> Drift { get; init; } = [];

    /// <summary>
    /// Whether a pop can come to hold it at all.
    /// </summary>
    /// <remarks>
    /// The game's <c>use_for_pops</c>. All eight fanatic ethics say no: a pop drifts to the ordinary
    /// form and the empire's own ethics decide the rest, which is why none of the eight carries a
    /// single drift sentence.
    /// </remarks>
    public bool DriftsInto { get; init; } = true;
}

/// <summary>
/// Every ethic, as a page about them needs them.
/// </summary>
/// <remarks>
/// A hundred and thirty-one sentences the game wrote for its own ethics-drift tooltip and shows
/// nowhere else a reader can study. Nothing in the database reaches one, so the pack carries the
/// text as well as the keys.
/// </remarks>
public sealed record EthicPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "ethics";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The ethics, in the order the game declares them.</summary>
    public IReadOnlyList<EthicDetail> Ethics { get; init; } = [];

    /// <summary>The sentences, which the pruner has never had a reason to keep.</summary>
    public IReadOnlyDictionary<string, string> Text { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// How an authority runs an empire's politics.
/// </summary>
/// <remarks>
/// All of it internal to the empire and none of it anything a design can be refused for, which is
/// why the designer never needed it. It is most of what tells one authority from another: the page
/// said "democratic" and not for how long, and said nothing at all about the three that can never
/// be changed again.
/// </remarks>
/// <param name="Key">The authority's own key.</param>
public sealed record AuthorityDetail(string Key)
{
    /// <summary>How long a ruler holds office, where the office has a term.</summary>
    public int? ElectionTermYears { get; init; }

    /// <summary>How a ruler is chosen: democratic, oligarchic, or none at all.</summary>
    public string? ElectionType { get; init; }

    /// <summary>
    /// Whether a reform can ever change it.
    /// </summary>
    /// <remarks>
    /// No on the hive mind, the machine intelligence and the ancient machine intelligence. Picking
    /// one of those is the single most permanent decision in empire creation and nothing said so.
    /// </remarks>
    public bool CanReform { get; init; } = true;

    /// <summary>Whether the empire has internal factions at all.</summary>
    public bool HasFactions { get; init; } = true;

    /// <summary>Whether its ruler pursues agendas.</summary>
    public bool HasAgendas { get; init; }

    /// <summary>Whether the throne passes to an heir.</summary>
    public bool HasHeir { get; init; }

    /// <summary>Whether a ruler may stand again.</summary>
    public bool ReElectionAllowed { get; init; }

    /// <summary>Whether a ruler is elected on a mandate.</summary>
    public bool UsesMandates { get; init; }

    /// <summary>Whether an election can be called early.</summary>
    public bool EmergencyElections { get; init; }

    /// <summary>How many candidates stand, where a number is given.</summary>
    public int? MaxElectionCandidates { get; init; }

    /// <summary>The game's own accent colour for it, as CSS.</summary>
    public string? Colour { get; init; }
}

/// <summary>Every authority, as a page about them needs them.</summary>
public sealed record AuthorityPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "authorities";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The authorities, in the order the game declares them.</summary>
    public IReadOnlyList<AuthorityDetail> Authorities { get; init; } = [];
}

/// <summary>
/// What a government does to an empire's names.
/// </summary>
/// <remarks>
/// Which is the whole of what the record had left to say. The page already spends four of its facts
/// on titles; these three finish the story, and one of them answers a question every player has
/// asked: why the empire was renamed when it reformed.
/// </remarks>
/// <param name="Key">The government's own key.</param>
public sealed record GovernmentDetail(string Key)
{
    /// <summary>Whether taking it renames the empire, which two-thirds of them do.</summary>
    public bool ForcesRename { get; init; }

    /// <summary>Whether its rulers are numbered - Zanaam II.</summary>
    public bool RegnalNames { get; init; }

    /// <summary>Whether its rulers inherit a house surname.</summary>
    public bool DynasticNames { get; init; }
}

/// <summary>Every government, as a page about them needs them.</summary>
public sealed record GovernmentPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "governments";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The governments, in the order the game declares them.</summary>
    public IReadOnlyList<GovernmentDetail> Governments { get; init; } = [];
}

/// <summary>
/// What a civic or an origin says beyond what an empire choosing one needs to know.
/// </summary>
/// <remarks>
/// One record for both, because the game writes them as one and tells them apart by a flag. An
/// origin answers the second half and a civic the first, and a field the other kind never states
/// simply stays at its default.
/// </remarks>
/// <param name="Key">Its own key.</param>
public sealed record CivicDetail(string Key)
{
    /// <summary>
    /// What it turns into when the empire reforms into the kind that has its own version.
    /// </summary>
    /// <remarks>
    /// Forty pairs, and the wiki had no edge between them: "what happens to Catalytic Processing
    /// when I become a megacorp" was a question the page could not answer about itself.
    /// </remarks>
    public string? BecomesInstead { get; init; }

    /// <summary>
    /// What an empire the game runs itself must be for it to take this one.
    /// </summary>
    /// <remarks>
    /// A condition of its own rather than a copy of <c>playable</c>, which is the point of the
    /// field: the game writes it as a block, and two civics gate the AI on a content pack the
    /// player is not gated on.
    /// </remarks>
    public Requirement AiPlayable { get; init; } = new AlwaysRequirement(true);

    /// <summary>Whether it stops the ordinary political factions forming.</summary>
    public bool SuppressesFactions { get; init; }

    /// <summary>Whether the game gives it a start screen of its own.</summary>
    public bool CustomStartScreen { get; init; }

    /// <summary>Whether an advanced AI empire may be given it. Origins only.</summary>
    public bool AdvancedStart { get; init; }

    /// <summary>Whether at most one empire in the galaxy can have it. Origins only.</summary>
    public bool OnlyOneInTheGalaxy { get; init; }

    /// <summary>Whether it keeps the generator from rolling individualist machine empires.</summary>
    public bool BlocksRandomMachineEmpires { get; init; }

    /// <summary>
    /// Whether the systems around the homeworld are steered toward worlds nobody can settle.
    /// </summary>
    /// <remarks>
    /// A flag rather than a list of classes, which is what the field's name suggests and is not
    /// what it is. Eight origins set it, and between them they are the lonely starts.
    /// </remarks>
    public bool NeighboursUninhabitable { get; init; }

    /// <summary>Whether the generator has a preferred class for them at all.</summary>
    public bool NeighboursPreferred { get; init; } = true;
}

/// <summary>
/// Every civic and origin, as the two pages about them need them.
/// </summary>
/// <remarks>
/// One file for both pages rather than one each, because it is one collection: the game writes a
/// civic and an origin in the same folder in the same shape, and splitting the file would be
/// inventing a division the data does not have.
/// </remarks>
public sealed record CivicPack : IWikiPack
{
    /// <summary>Where this pack lives, which is the whole of what names the file.</summary>
    public const string Domain = "civics";

    /// <summary>The shape these records are in, bumped when it changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <inheritdoc />
    public static int ExpectedSchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public required WikiPackStamp Stamp { get; init; }

    /// <summary>The civics and origins, in the order the game declares them.</summary>
    public IReadOnlyList<CivicDetail> Civics { get; init; } = [];
}

/// <summary>
/// What the government family says beyond what a design needs, gathered as the extractor reads it.
/// </summary>
/// <remarks>
/// Five collections and four files: the civics and the origins are one collection the game tells
/// apart by a flag, so the two pages about them read the same pack.
/// </remarks>
public sealed record GovernmentFamily
{
    /// <summary>What each ethic does about pops.</summary>
    public IReadOnlyList<EthicDetail> Ethics { get; init; } = [];

    /// <summary>How each authority runs an empire's politics.</summary>
    public IReadOnlyList<AuthorityDetail> Authorities { get; init; } = [];

    /// <summary>What each government does to an empire's names.</summary>
    public IReadOnlyList<GovernmentDetail> Governments { get; init; } = [];

    /// <summary>What each civic and origin says beyond what choosing one needs.</summary>
    public IReadOnlyList<CivicDetail> Civics { get; init; } = [];
}
