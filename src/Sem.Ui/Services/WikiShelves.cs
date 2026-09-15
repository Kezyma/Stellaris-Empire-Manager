using Sem.GameData;
using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>Which shelf of the game a wiki page is showing.</summary>
public enum WikiKind
{
    /// <summary>The civics, which is the largest shelf and the way in.</summary>
    Civics,

    /// <summary>The origins, which are the same records with a flag set and a scene each.</summary>
    Origins,

    /// <summary>The ethics, which cost points and come in opposing pairs.</summary>
    Ethics,

    /// <summary>The authorities, which say how an empire is governed.</summary>
    Authorities,

    /// <summary>The species classes, which are a set of faces rather than one.</summary>
    Species,

    /// <summary>The traits a founding species can be given.</summary>
    SpeciesTraits,

    /// <summary>The worlds an empire can wake up on, and the rest it will find.</summary>
    Planets,

    /// <summary>The ships and cities an empire is drawn with.</summary>
    Shipsets,

    /// <summary>What the game plays an empire as when it is somebody else's neighbour.</summary>
    Personalities,

    /// <summary>What an empire ends up called, and what it calls whoever rules it.</summary>
    Governments,

    /// <summary>The perks an empire spends its ascension on.</summary>
    AscensionPerks,

    /// <summary>
    /// The traits a leader can hold, which the database does not carry.
    /// </summary>
    /// <remarks>
    /// The first shelf built from a file of the wiki's own rather than from the empire designer's
    /// data. Seven hundred records nothing in an empire can hold, fetched when somebody opens the
    /// page and not before.
    /// </remarks>
    LeaderTraits,
}

/// <summary>
/// One shelf, ready to draw: what it is called, what is on it, and what it can be narrowed by.
/// </summary>
/// <param name="Title">What the page is called.</param>
/// <param name="Noun">The plural, for the counts and the spoken labels.</param>
/// <param name="One">The singular, for the sentences that need one.</param>
/// <param name="Rows">Everything on it.</param>
/// <param name="Facets">The headings it can be narrowed by.</param>
public sealed record WikiShelf(
    string Title,
    string Noun,
    string One,
    IReadOnlyList<WikiRow> Rows,
    IReadOnlyList<Facet<WikiRow>> Facets)
{
    /// <summary>
    /// The text this shelf is written in, where that is not the app's own.
    /// </summary>
    /// <remarks>
    /// Null for every shelf read from the database, which is written in the text every page already
    /// has. A shelf read from one of the wiki's own files is not: a leader trait's name and prose
    /// are in that file and nowhere else, because the app's localisation is pruned to what the
    /// database reaches. The page cascades this so the chips and the prose resolve through it.
    /// </remarks>
    public Localizer? Reader { get; init; }

    /// <summary>
    /// The keys this shelf answers for that the database does not carry.
    /// </summary>
    /// <remarks>
    /// Empty for every shelf read from the database, where a key can simply be looked up. A shelf
    /// read from one of the wiki's own files has to say so, or a chip naming one of its entries has
    /// nowhere to go - which is how the Replaces and Rules out chips came to be the only ones on a
    /// wiki page that were not links.
    /// </remarks>
    public IReadOnlySet<string> Entries { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// What the pictures on this shelf are of, for the column they are drawn in.
    /// </summary>
    /// <remarks>
    /// An origin carries a scene of the world its empire wakes up on, which is where the word came
    /// from and is still the default. A world's is its own sky and a shipset's is one of its ships,
    /// and calling either a scene is the column heading lying about what is under it.
    /// </remarks>
    public string Picture { get; init; } = "Scene";

    /// <summary>
    /// What shape those pictures are, which decides how they are framed.
    /// </summary>
    /// <remarks>
    /// A scene is the game's own origin art and is meant to be cropped to the frame the game crops it
    /// to - so that is the default and every shelf but one keeps it. A ship is a render of ours,
    /// three hundred and twenty by two hundred on a transparent ground, and cropping one to an
    /// origin's frame cut fifteen per cent off the bottom of every hull and both ends of every tile.
    /// </remarks>
    public string Shape { get; init; } = "scene";
}

/// <summary>
/// Everything the game has to say about the things the wiki shows, read once.
/// </summary>
/// <remarks>
/// <para>
/// A shelf at a time and each one held, because reading one is a pass over a game collection with a
/// localisation lookup and up to five condition trees for every entry - and the page asks for it on
/// every keystroke in the search box.
/// </para>
/// <para>
/// One class for all four kinds rather than one each, because three-quarters of the work is the same
/// for every kind: the name, the prose, the artwork, the packs, the modifiers and the search text
/// are read the same way whatever the record is. What differs is which conditions the game states
/// about it and which facts are worth a column, and those are the two methods each kind supplies.
/// </para>
/// </remarks>
/// <param name="session">The session, for the game data and the writers that turn it into prose.</param>
public sealed class WikiShelves(DesignSession session)
{
    private readonly Dictionary<WikiKind, WikiShelf> _shelves = [];

    private readonly ConditionReader _reader = new(session.Localizer, session.Data.Database);

    private GameDatabase Database => session.Data.Database;

    /// <summary>One shelf, built the first time it is asked for.</summary>
    /// <param name="kind">Which shelf.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Of(WikiKind kind)
    {
        if (_shelves.TryGetValue(kind, out var held))
        {
            return held;
        }

        var shelf = Read(kind);
        _shelves[kind] = shelf;
        return shelf;
    }

    /// <summary>
    /// The ethics, with what the game says about a pop drifting toward one.
    /// </summary>
    /// <remarks>
    /// A hundred and thirty-one sentences the game wrote for its own ethics-drift tooltip. Nothing
    /// in the designer's data reaches one, because nothing a design does depends on them.
    /// </remarks>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Ethics(EthicPack? pack) =>
        new("Ethics", "ethics", "ethic", EthicRows(pack), WikiFacet.Ethics);

    /// <summary>The authorities, with how each runs an empire's politics.</summary>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Authorities(AuthorityPack? pack) =>
        new(
            "Authorities", "authorities", "authority",
            AuthorityRows(Detail(pack?.Authorities, a => a.Key)),
            WikiFacet.Authorities)
        {
            // A reader of its own even though the pack carries no text, because what an empire's
            // politics amount to is written here rather than by the game - see Governing.
            Reader = Reading(new Dictionary<string, string>(StringComparer.Ordinal)),
        };

    /// <summary>The governments, with what each does to an empire's names.</summary>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Governments(GovernmentPack? pack) =>
        new(
            "Governments", "governments", "government",
            GovernmentRows(Detail(pack?.Governments, g => g.Key)),
            WikiFacet.Governments);

    /// <summary>
    /// The civics, or the origins, with what each says beyond what choosing it needs.
    /// </summary>
    /// <remarks>
    /// One pack for both pages, because a civic and an origin are one collection the game tells
    /// apart by a flag.
    /// </remarks>
    /// <param name="kind">Which of the two pages is asking.</param>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Civics(WikiKind kind, CivicPack? pack) =>
        kind == WikiKind.Origins
            ? new WikiShelf(
                "Origins", "origins", "origin",
                Civics(origins: true, Detail(pack?.Civics, c => c.Key)), WikiFacet.Civics)
            : new WikiShelf(
                "Civics", "civics", "civic",
                Civics(origins: false, Detail(pack?.Civics, c => c.Key)), WikiFacet.Civics);

    /// <summary>A pack's records by key, or nothing where the pack has not arrived.</summary>
    /// <typeparam name="T">What the pack holds.</typeparam>
    /// <param name="records">The records.</param>
    /// <param name="key">How to name one.</param>
    /// <returns>The lookup, which is empty until the page has its file.</returns>
    private static IReadOnlyDictionary<string, T> Detail<T>(
        IReadOnlyList<T>? records,
        Func<T, string> key) =>
        records is null
            ? new Dictionary<string, T>(StringComparer.Ordinal)
            : records.ToDictionary(key, StringComparer.Ordinal);

    /// <summary>
    /// The species traits, with everything a picker never needed to know.
    /// </summary>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf SpeciesTraits(SpeciesTraitPack? pack) =>
        new(
            "Species Traits", "species traits", "species trait",
            SpeciesTraitRows(pack), WikiFacet.SpeciesTraits)
        {
            Reader = pack is null ? null : Reading(pack.Text),
        };

    /// <summary>The species classes, with what a pre-sapient one becomes.</summary>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Species(SpeciesClassPack? pack) =>
        new(
            "Species", "species classes", "species class",
            SpeciesRows(Detail(pack?.Classes, c => c.Key)), WikiFacet.Species);

    /// <summary>
    /// The worlds, with what living on one actually does.
    /// </summary>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Planets(WorldPack? pack) =>
        new("Planets", "planets", "planet", PlanetRows(pack), WikiFacet.Planets)
        {
            Picture = "Sky",
            Reader = pack is null ? null : Reading(pack.Text),
        };

    /// <summary>
    /// A shelf before its page has fetched anything, which is the same shelf with no pack.
    /// </summary>
    /// <remarks>
    /// Every arm forwards to the method the page itself calls. Written out a second time - which is
    /// what it was - each shelf's title, plural, singular and list of headings existed twice and
    /// nothing held the two copies together: the authorities had grown a reader of their own on one
    /// side and not the other, so the same shelf built here drew its Politics tags with no meanings
    /// behind them.
    ///
    /// Which matters more than tidiness, because this is the path the tests take.
    /// </remarks>
    /// <param name="kind">Which shelf.</param>
    /// <returns>The shelf, with whatever a pack would have added left out.</returns>
    private WikiShelf Read(WikiKind kind) => kind switch
    {
        WikiKind.Origins => Civics(WikiKind.Origins, pack: null),
        WikiKind.Ethics => Ethics(pack: null),
        WikiKind.Authorities => Authorities(pack: null),
        WikiKind.Species => Species(pack: null),
        WikiKind.SpeciesTraits => SpeciesTraits(pack: null),
        WikiKind.Planets => Planets(pack: null),
        WikiKind.Governments => Governments(pack: null),
        WikiKind.Shipsets => Shipsets(pack: null),
        WikiKind.Personalities => Personalities(pack: null),
        WikiKind.LeaderTraits => LeaderTraits(pack: null),

        WikiKind.AscensionPerks => new WikiShelf(
            "Ascension Perks", "ascension perks", "ascension perk",
            AscensionPerks(), WikiFacet.AscensionPerks),

        _ => Civics(WikiKind.Civics, pack: null),
    };

    /// <summary>
    /// The civics, or the origins, which are the same records with a flag set.
    /// </summary>
    /// <remarks>
    /// Reachability is worked out across all three hundred and fifty-eight at once even when only
    /// half of them are wanted, because it has to be: a civic can be out of reach only because
    /// another one is, and three origins are a ring that each ask for one of the others.
    /// </remarks>
    private IReadOnlyList<WikiRow> Civics(bool origins, IReadOnlyDictionary<string, CivicDetail> detail)
    {
        var reach = CivicReach.Across(Database.Civics);

        return
        [
            .. Database.Civics
                .Where(c => c.IsOrigin == origins)
                .Select(c => Civic(c, reach[c.Key], detail.GetValueOrDefault(c.Key)))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// One civic, with the two trees the game states about an empire read as one list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game keeps <c>potential</c> and <c>possible</c> apart and means something by it: failing
    /// the first hides the civic from the list, failing the second shows it greyed out with the
    /// game's own explanation. Measured, they even divide neatly - potential asks about the empire's
    /// shape, mostly its authority and ethics, and possible is almost all mutual exclusion with other
    /// civics and origins.
    /// </para>
    /// <para>
    /// It is a distinction about how the game refuses you rather than about whether it does, and a
    /// wiki is not reproducing the game's picker. Both answer the one question a reader came with -
    /// what does my empire have to be - so they are one list, and two narrow columns that were often
    /// half empty become one that is not. The origins show why: twelve of them state the first and
    /// fifty-seven the second, with only eight stating both.
    /// </para>
    /// <para>
    /// They merge without a seam. Both are AND groups, so the outline flattens them into one list
    /// rather than nesting, and the twenty-five entries that name the same thing in both trees say
    /// it once - see the outline's own deduplication.
    /// </para>
    /// </remarks>
    private WikiRow Civic(CivicDefinition civic, CivicReach reach, CivicDetail? detail)
    {
        var shut = Shut(reach);

        return Row(
            civic.Key,
            civic.Effects,
            civic.Playable,
            reach.EverOffered,
            shut?.Short,
            shut?.Why,
            [
                new WikiCondition(
                    "Requirements",
                    _reader.Read(new AllRequirement([civic.Potential, civic.Possible])),
                    "Any empire"),
            ],
            CivicFacts(civic, detail),
            Wants(civic.Potential, civic.Possible)) with
        {
            Icon = civic.Icon,
            Picture = civic.Picture,
        };
    }

    /// <summary>
    /// What the game calls this option when it is speaking to somebody else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game rewords an option for the empire in front of it, and we have carried those words
    /// all along and shown them to nobody: a wilderness empire's Devouring Swarm is Devouring
    /// Wilderness, a lithoid's is Terravore, and a hive mind reading this page was reading prose
    /// written for an empire it is not. Forty-eight of them across the civics, the origins and the
    /// perks actually say something different.
    /// </para>
    /// <para>
    /// Two headings, because they are two different answers. Eight change the name, which is the
    /// thing a reader would otherwise never connect to this page at all; the rest keep the name and
    /// rewrite the prose, and for those what matters is the prose, which is behind the chip.
    /// </para>
    /// <para>
    /// A swap that declares the same name and the same description as the option itself is left
    /// out. The game writes several - a pair for the same option differing only in which of them
    /// fires - and drawn they were a chip saying the page already said it.
    /// </para>
    /// </remarks>
    /// <param name="variants">The swaps, which for most options are none.</param>
    /// <param name="nameKey">Where the option's own name is.</param>
    /// <param name="descriptionKey">Where its own prose is.</param>
    /// <returns>Up to two facts, each dropped by the row where it says nothing.</returns>
    private IReadOnlyList<WikiFact> Wordings(
        IReadOnlyList<OptionVariant> variants,
        string nameKey,
        string? descriptionKey)
    {
        var name = session.Localizer.Text(nameKey, string.Empty);
        var prose = session.Localizer.Text(descriptionKey, string.Empty);

        List<EmpireChoice> renamed = [];
        List<EmpireChoice> reworded = [];

        foreach (var variant in variants)
        {
            if (Audience(variant.When) is not { Length: > 0 } audience)
            {
                continue;
            }

            var said = session.Localizer.Text(variant.NameKey, string.Empty);
            var told = session.Localizer.Text(variant.DescriptionKey, string.Empty);

            // Both halves compared as the words they come out as rather than as keys, because the
            // game writes a swap whose name key is a redirect to the option's own - the same words
            // under a second name - and by key those look like a change.
            var callsIt = said is { Length: > 0 } && !string.Equals(said, name, StringComparison.Ordinal);
            var tellsIt = told is { Length: > 0 } && !string.Equals(told, prose, StringComparison.Ordinal);

            if (callsIt)
            {
                renamed.Add(Wording($"{audience}: {said}", variant.DescriptionKey ?? descriptionKey));
            }
            else if (tellsIt)
            {
                reworded.Add(Wording(audience, variant.DescriptionKey));
            }
        }

        return
        [
            WikiFact.Of("Also called", [.. renamed.DistinctBy(c => c.Name, StringComparer.Ordinal)]),
            WikiFact.Of("Reworded for", [.. reworded.DistinctBy(c => c.Name, StringComparer.Ordinal)]),
        ];
    }

    /// <summary>One of those, whose panel holds the wording itself.</summary>
    /// <param name="said">What the chip is labelled.</param>
    /// <param name="descriptionKey">Where the wording is, so the panel can show it.</param>
    /// <returns>The chip.</returns>
    private static EmpireChoice Wording(string said, string? descriptionKey) =>
        new(said, said, null, null) { Description = descriptionKey };

    /// <summary>
    /// Who the game is speaking to when it rewords an option.
    /// </summary>
    /// <remarks>
    /// The constants are dropped before the condition is read, which is the one liberty this takes.
    /// A swap's trigger carries scaffolding the extractor settles for its own purposes - twelve of
    /// them guard a scope with <c>exists</c> and three ask after a flag - and none of that is an
    /// answer to "who is this written for". The rules a reader must meet are in the Requirements
    /// column, where nothing is dropped; this is a label.
    /// </remarks>
    /// <param name="when">The swap's trigger.</param>
    /// <returns>The label, or nothing where the trigger says nothing a reader could use.</returns>
    private string? Audience(Requirement when) =>
        session.Conditions.Describe(Bare(when)) is { Length: > 0 } said
            ? char.ToUpperInvariant(said[0]) + said[1..]
            : null;

    /// <summary>The same condition with the settled parts taken out.</summary>
    /// <param name="requirement">The condition.</param>
    /// <returns>What is left of it.</returns>
    private static Requirement Bare(Requirement requirement) => requirement switch
    {
        AllRequirement all => new AllRequirement([.. Kept(all.Items)]),
        AnyRequirement any => new AnyRequirement([.. Kept(any.Items)]),
        NotRequirement not => new NotRequirement(Bare(not.Item)),
        _ => requirement,
    };

    /// <summary>The parts of a group that are not constants, each bared in turn.</summary>
    /// <param name="items">The parts.</param>
    /// <returns>What is left of them.</returns>
    private static IEnumerable<Requirement> Kept(IReadOnlyList<Requirement> items) =>
        items.Where(i => i is not AlwaysRequirement).Select(Bare);

    /// <summary>
    /// What is worth saying about a civic or an origin beyond what it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This list was empty. Everything in it was already extracted and reached no pixel: whether a
    /// reform can take a civic on or give it up, which is stated by a hundred and forty-two of them;
    /// the traits an origin nails onto the founding species, which is thirty-two; the system it locks
    /// the empire into, which is fourteen. The page showed a name, a picture, the prose and the
    /// conditions, and nothing else the record held.
    /// </para>
    /// <para>
    /// One method for both shelves, because they are one record. An origin states nothing about
    /// reform and a civic nothing about a starting system, and a fact with nothing in it is dropped
    /// before it reaches a column - so each page draws only the headings its own entries answer.
    /// </para>
    /// </remarks>
    /// <param name="civic">The civic or origin.</param>
    /// <param name="detail">What the page fetched about it, or null before that arrives.</param>
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> CivicFacts(CivicDefinition civic, CivicDetail? detail) =>
    [
        WikiFact.Tagged("Reform", Reform(civic)),
        WikiFact.Of("Forces", Traits(civic.ForcedTraits)),
        WikiFact.Of("Grants", Traits(civic.SoftTraits)),
        WikiFact.Of("Starts on", Worlds(civic.StartingColony)),
        WikiFact.Of("Suited to", Worlds(civic.HabitabilityPreference)),
        WikiFact.Of("Starting system", Systems(civic.Initializers)),
        WikiFact.Of("Opens", Worlds(civic.AddedPlanetClasses)),
        WikiFact.Of("Closes", Worlds(civic.RemovedPlanetClasses)),

        WikiFact.Said(
            "Second species",
            civic.RequiresSecondarySpecies ? "Required" : null),

        WikiFact.Of("Second species has", Traits(civic.SecondarySpeciesTraits)),

        // And what somebody else is shown in place of all that. A hive mind reading this page was
        // reading wording written for an empire it is not.
        .. Wordings(civic.Variants, civic.NameKey, $"{civic.Key}_desc"),

        // What it turns into when the empire reforms into the kind with its own version of it -
        // forty pairs, and the wiki had no edge between them at all.
        WikiFact.Of("Becomes", Civics(detail?.BecomesInstead)),

        WikiFact.Said("AI empires", AiGate(civic, detail)),
        WikiFact.Said("Factions", detail is { SuppressesFactions: true } ? "Suppressed" : null),

        // The rest is an origin's half of the record. A civic states none of it, and a fact with
        // nothing in it never reaches a column - so the two pages draw different headings from one
        // list, which is the same arrangement the reform and starting-system facts already use.
        WikiFact.Said("Advanced start", detail is { AdvancedStart: true } ? "Allowed" : null),
        WikiFact.Said(
            "In the galaxy",
            detail is { OnlyOneInTheGalaxy: true } ? "One empire only" : null),
        WikiFact.Said(
            "Start screen",
            detail is { CustomStartScreen: true } ? "Its own" : null),
        WikiFact.Said(
            "Machine empires",
            detail is { BlocksRandomMachineEmpires: true } ? "Not generated beside it" : null),

        // What the galaxy puts next door, which between them are the lonely starts.
        WikiFact.Said(
            "Neighbours",
            detail switch
            {
                { NeighboursUninhabitable: true } => "Nothing settleable",
                { NeighboursPreferred: false } => "Nothing in particular",
                _ => null,
            }),
    ];

    /// <summary>
    /// What an empire the game runs itself has to be to take this one.
    /// </summary>
    /// <remarks>
    /// Said only where it differs from what a player must be, which is the whole reason the game
    /// writes the field: two civics let a player take what an AI without the content pack cannot,
    /// and on every other one this repeats the Requirements column.
    /// </remarks>
    /// <param name="civic">The civic or origin, for what a player must be.</param>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The words, or nothing.</returns>
    private string? AiGate(CivicDefinition civic, CivicDetail? detail)
    {
        if (detail?.AiPlayable is not { } gate)
        {
            return null;
        }

        // Compared as the sentences they come out as rather than as trees, because two trees
        // compiled from two blocks are never the same object and a record's list does not compare
        // by its contents. Both go through one writer, so equal conditions read identically.
        var said = session.Conditions.Describe(gate);

        return said == session.Conditions.Describe(civic.Playable)
            ? null
            : said is { Length: > 0 } ? said : "Never take it";
    }

    /// <summary>
    /// Whether a government reform can take this on or give it up.
    /// </summary>
    /// <remarks>
    /// The game's own <c>modification</c> field, which its comment describes as "set to no to prevent
    /// adding or removing this after creation of the empire". Ninety-six civics refuse outright and
    /// thirty-three make it conditional; the rest say nothing and mean yes, and those draw nothing
    /// here rather than repeating the default a hundred and fifty times.
    /// </remarks>
    /// <param name="civic">The civic or origin.</param>
    /// <returns>What it allows, or null where it allows everything.</returns>
    private static IReadOnlyList<string> Reform(CivicDefinition civic)
    {
        var added = civic.CanAddLater;
        var dropped = civic.CanRemoveLater;

        // Nothing to say where a reform can do as it likes with it, which is most of them.
        if (added is AlwaysRequirement { Value: true } && dropped is AlwaysRequirement { Value: true })
        {
            return [];
        }

        // Two labels rather than one sentence, because they are two independent answers: whether a
        // reform can take it on, and whether a reform can give it up. Written as one they had to be
        // spelled out four ways - "Start only, and permanent" - and a reader comparing two civics
        // was comparing two sentences instead of two sets of the same three words.
        List<string> said = [];

        if (Never(added))
        {
            said.Add("Start only");
        }

        if (Never(dropped))
        {
            said.Add("Permanent");
        }

        return said.Count > 0 ? said : ["Conditional"];
    }

    /// <summary>Whether a condition refuses outright rather than asking something.</summary>
    /// <param name="requirement">The condition.</param>
    /// <returns>True where nothing can satisfy it.</returns>
    private static bool Never(Requirement? requirement) =>
        requirement is AlwaysRequirement { Value: false };

    /// <summary>
    /// The ethics, which state no conditions at all.
    /// </summary>
    /// <remarks>
    /// Seventeen records with no <c>playable</c> and no <c>possible</c> between them, so there is
    /// nothing to gate them and every one is within reach of anybody. What an ethic costs, what it
    /// rules out and which form of itself it has are the questions instead, and they are facts
    /// rather than conditions.
    /// </remarks>
    private IReadOnlyList<WikiRow> EthicRows(EthicPack? pack)
    {
        var detail = Detail(pack?.Ethics, e => e.Key);

        // The drift sentences are in the pack rather than in the app's own text, so the chips have
        // to be named through a reader that has both - the same arrangement the leader traits use,
        // and the reason a chip built with the ordinary localiser came out empty and was dropped.
        var reader = pack is null ? session.Localizer : Reading(pack.Text);

        return
        [
            .. Database.Ethics
                .Select(e => Ethic(e, detail.GetValueOrDefault(e.Key), reader))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    private WikiRow Ethic(EthicDefinition ethic, EthicDetail? detail, Localizer reader) =>
        Row(
            ethic.Key,
            ethic.Effects,
            playable: null,
            reachable: true,
            null,
            null,
            [],
            EthicFacts(ethic, detail, reader),
            null) with
        {
            Icon = ethic.Icon,
        };

    /// <summary>
    /// What is worth saying about an ethic beyond what it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cost first, because three points buy the whole of an empire's ethics and an ethic that
    /// takes two of them is most of the decision. The number alone: the budget is the same for
    /// every row, so saying it on each of them is a column repeating itself seventeen times.
    /// </para>
    /// <para>
    /// Then the two relationships the game gives them. Every ethic but gestalt has another strength
    /// of itself, and every one sits on one side of a pair - so the opposite is everything in its
    /// own category on the far side of the middle. Gestalt is alone in its category and rules out
    /// all sixteen of the others, which is a sentence rather than a list.
    /// </para>
    /// </remarks>
    private IReadOnlyList<WikiFact> EthicFacts(
        EthicDefinition ethic,
        EthicDetail? detail,
        Localizer reader) =>
    [
        WikiFact.Said("Cost", ethic.Cost.ToString(System.Globalization.CultureInfo.CurrentCulture)),

        WikiFact.Said(
            "Intensity",
            ethic.IsGestalt ? "Gestalt" : ethic.IsFanatic ? "Fanatic" : "Ordinary"),

        // Whether a pop can come to hold it at all, which the eight fanatics cannot: a pop drifts
        // to the ordinary form and the empire's own ethics decide the rest. Which is also why none
        // of the eight carries a single drift sentence.
        WikiFact.Said("Pops", detail is { DriftsInto: false } ? "Cannot drift into it" : null),

        WikiFact.Listed("Drift toward", Drift(detail, reader, draws: true)),
        WikiFact.Listed("Drift away", Drift(detail, reader, draws: false)),

        // One heading rather than "Stronger form" and "Milder form", which is what it was. The
        // direction reads better on a card and is a disaster in a table: the heading differs by row,
        // so the column list is the union of both and every ethic fills one of them and leaves the
        // other blank. The chip says which way round it is anyway - the fanatic form is the one with
        // "Fanatic" in its name.
        WikiFact.Of(
            "Other form",
            Ethics(ethic.IsFanatic ? ethic.RegularVariant : ethic.FanaticVariant)),

        ethic.IsGestalt
            ? WikiFact.Said("Rules out", "Every other ethic")
            : WikiFact.Of("Rules out", Ethics(Opposing(ethic))),
    ];

    /// <summary>
    /// The ethics on the other side of the one pair this one belongs to.
    /// </summary>
    /// <remarks>
    /// The game groups a pair under one category and places each ethic on a scale within it: the
    /// fanatic and ordinary forms of one pole sit below the middle and the other pole's above. So
    /// the opposites are its own category, on the far side - which is both forms of the other pole,
    /// and not the milder form of itself.
    /// </remarks>
    private IEnumerable<string> Opposing(EthicDefinition ethic)
    {
        const int Middle = 2;

        return Database.Ethics
            .Where(e => string.Equals(e.Category, ethic.Category, StringComparison.Ordinal))
            .Where(e => (e.CategoryValue > Middle) != (ethic.CategoryValue > Middle))
            .Select(e => e.Key);
    }

    /// <summary>
    /// The sentences the game writes about a pop leaning one way or the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two headings, because the game writes them in two colours and means two different things by
    /// them. A hundred and six draw pops toward the ethic and twenty-five push them away, and run
    /// together under one heading a reader would have to read the sign on each to tell which.
    /// </para>
    /// <para>
    /// A list rather than a row of chips. These are not names of things - there is nothing to link
    /// to, no picture to draw and nothing behind them to open - they are sentences, and a dozen
    /// sentences in pills is a paragraph wearing borders. The same shape a list of modifiers has,
    /// which is the same kind of thing.
    /// </para>
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <param name="draws">Which of the two headings is being built.</param>
    /// <returns>The sentences.</returns>
    private static IReadOnlyList<string> Drift(
        EthicDetail? detail,
        Localizer reader,
        bool draws) =>
    [
        .. (detail?.Drift ?? [])
            .Where(d => d.Draws == draws)
            .Select(d => Unsigned(reader.Text(d.DescriptionKey, string.Empty)))
            .Where(said => said is { Length: > 0 })
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>
    /// A drift sentence without the sign the game opens it with.
    /// </summary>
    /// <remarks>
    /// The game writes every one of them as "+ Empire is at war" or "- Pop has trait Weak", because
    /// in its own tooltip the two run together in one list and the sign is the only thing telling
    /// them apart. Here the heading does that, and the sign repeated down a column of a dozen chips
    /// is a column of plus signs.
    /// </remarks>
    /// <param name="said">The sentence, once the markup is off it.</param>
    /// <returns>What it says.</returns>
    private static string Unsigned(string said) =>
        said.Length > 1 && said[0] is '+' or '-' ? said[1..].TrimStart() : said;

    /// <summary>
    /// The authorities, whose reachability the rules layer already has an opinion about.
    /// </summary>
    /// <remarks>
    /// <c>AiOnly</c> rather than a condition, and that is <c>EmpireRules</c>'s own decision restated:
    /// two authorities declare a <c>potential</c> about the kind of country, the game's designer does
    /// not read it, and honouring it would hide Machine Intelligence from the player who is entitled
    /// to it. What actually keeps one out of the list is the flag.
    /// </remarks>
    /// <param name="detail">What each page fetched, which is empty until it arrives.</param>
    /// <returns>The rows.</returns>
    private IReadOnlyList<WikiRow> AuthorityRows(IReadOnlyDictionary<string, AuthorityDetail> detail) =>
    [
        .. Database.Authorities
            .Select(a => Authority(a, detail.GetValueOrDefault(a.Key)))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    private WikiRow Authority(AuthorityDefinition authority, AuthorityDetail? detail) =>
        Row(
            authority.Key,
            authority.Effects,
            authority.Playable,
            !authority.AiOnly,
            authority.AiOnly ? "Unplayable" : null,
            authority.AiOnly
                ? "The game keeps this for its own empires. Nothing in the empire designer offers it."
                : null,
            [new WikiCondition("Requirements", _reader.Read(authority.Possible), "Any empire")],
            AuthorityFacts(authority, detail),
            Wants(authority.Possible)) with
        {
            Icon = authority.Icon,
        };

    /// <summary>
    /// What is worth saying about an authority beyond what it does.
    /// </summary>
    /// <remarks>
    /// How rulers are chosen and whether there is an heir are the two things that differ between
    /// them and are not in the prose - and the traits an authority forces are a real cost, since a
    /// hive mind's founders are hive-minded whatever else the player wanted them to be.
    /// </remarks>
    private IReadOnlyList<WikiFact> AuthorityFacts(
        AuthorityDefinition authority,
        AuthorityDetail? detail) =>
    [
        WikiFact.Said("Elections", Localizer.Prettify(authority.ElectionType)),
        WikiFact.Said("Heir", authority.HasHeir ? "Yes" : "No"),
        WikiFact.Of("Forces", Traits(authority.ForcedTraits)),

        // How long the office is held, which the page had no way of saying: it said "democratic"
        // and left a reader to find out that the term is ten years and an oligarchy's is twenty.
        WikiFact.Said("Term", Years(detail?.ElectionTermYears)),

        WikiFact.Said(
            "Candidates",
            detail?.MaxElectionCandidates is { } many
                ? many.ToString(System.Globalization.CultureInfo.CurrentCulture)
                : null),

        // And the most permanent decision in empire creation, which nothing warned anybody about.
        // Three authorities refuse every reform: choose one and the empire is that for ever.
        WikiFact.Said("Reform", detail is { CanReform: false } ? "Never" : null),

        WikiFact.Of("Politics", Politics(detail)),
    ];

    /// <summary>
    /// What each part of an empire's internal politics is, in the game's own terms.
    /// </summary>
    /// <remarks>
    /// Ours, because the game names none of these as a thing: they are yes-and-no fields on an
    /// authority and the only text anywhere near them is the tooltip of the screen that shows the
    /// consequences. A chip that opens an empty panel is worse than no chip, and "Mandates" tells
    /// somebody who has not played an oligarchy nothing at all.
    /// </remarks>
    private static readonly Dictionary<string, string> Governing =
        new(StringComparer.Ordinal)
        {
            ["Factions"] = "Its citizens organise into political factions, which hold opinions "
                + "about how the empire is run and pay out influence when they approve.",
            ["Agendas"] = "Its ruler pursues an agenda, which the empire works toward for a while "
                + "and is rewarded for finishing.",
            ["Mandates"] = "Each elected ruler comes in on a promise, and keeping it pays.",
            ["Re-election"] = "A ruler whose term is up may stand again rather than stepping down.",
            ["Emergency elections"] = "An election can be called before the term is up.",
        };

    /// <summary>
    /// What a government does to the names of the people in charge.
    /// </summary>
    /// <remarks>
    /// Thirty-six number their rulers and twenty-six give them a house, and the two are not the
    /// same question - an empire can do both, or one, or neither.
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The labels.</returns>
    private static IReadOnlyList<string> Naming(GovernmentDetail? detail) =>
        detail is null
            ? []
            : [
                .. new (bool Has, string Said)[]
                    {
                        (detail.RegnalNames, "Numbered"),
                        (detail.DynasticNames, "Dynastic"),
                    }
                    .Where(p => p.Has)
                    .Select(p => p.Said),
            ];

    /// <summary>A term of office, said as the years it is.</summary>
    /// <param name="years">The term, where the office has one.</param>
    /// <returns>The words, or nothing.</returns>
    private static string? Years(int? years) =>
        years is { } many
            ? string.Create(
                System.Globalization.CultureInfo.CurrentCulture,
                $"{many} years")
            : null;

    /// <summary>
    /// What an empire under this authority has in the way of internal politics.
    /// </summary>
    /// <remarks>
    /// Said as the things it has rather than as a column of yes and no, because that is how a
    /// reader compares two of them: a hive mind's row is empty and an oligarchy's carries four.
    /// Whether an empire has factions at all is a large mechanical difference and was only ever
    /// implied by prose.
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The chips.</returns>
    private static IReadOnlyList<EmpireChoice> Politics(AuthorityDetail? detail) =>
        detail is null
            ? []
            : [
                .. new (bool Has, string Said)[]
                    {
                        (detail.HasFactions, "Factions"),
                        (detail.HasAgendas, "Agendas"),
                        (detail.UsesMandates, "Mandates"),
                        (detail.ReElectionAllowed, "Re-election"),
                        (detail.EmergencyElections, "Emergency elections"),
                    }
                    .Where(p => p.Has)
                    .Select(p => new EmpireChoice(p.Said, p.Said, null, null)
                    {
                        Description = Meaning(p.Said),
                    }),
            ];

    /// <summary>
    /// The species classes, which are the one shelf whose entries are a set of pictures.
    /// </summary>
    /// <remarks>
    /// Forty-two of them, twenty marked unplayable outright - <c>docs/hidden-content.md</c> has the
    /// count and the test that found it, an empire naming one simply not appearing in the game's
    /// list. They carry no effects and no packs; what a reader wants is the archetype, the trait
    /// every member is born with, and the faces.
    /// </remarks>
    private IReadOnlyList<WikiRow> SpeciesRows(IReadOnlyDictionary<string, SpeciesClassDetail> detail) =>
    [
        .. Database.SpeciesClasses
            .Select(c => SpeciesClass(c, detail.GetValueOrDefault(c.Key)))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>
    /// One species class, with its faces and what the game asks of an empire wearing them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unplayable means refused, not conditional. Read as "unplayable unless the condition says
    /// nothing", this marked thirty-three of the forty-two shut when nineteen are: Toxoid, Necroid,
    /// Aquatic, Lithoid, Plantoid, Thermophile, Mindwarden and Solarpunk are behind a content pack,
    /// which is a thing you can buy rather than a door that is closed.
    /// </para>
    /// <para>
    /// Nineteen is also the number <c>docs/hidden-content.md</c> records, reached by putting a design
    /// naming one in the game and finding the empire missing from its list. So the two agree.
    /// </para>
    /// <para>
    /// Refusal is not the only door though, and <see cref="Closed"/> holds the other two.
    /// </para>
    /// <para>
    /// The pack is not read here. <see cref="Row"/> is handed the same tree and works the packs out
    /// of it, which is how Toxoid comes to carry a Toxoids chip without this method mentioning one.
    /// </para>
    /// </remarks>
    private WikiRow SpeciesClass(SpeciesClassDefinition species, SpeciesClassDetail? detail)
    {
        var faces = SpeciesFaces.All(Database, species.Key);
        var (shut, why) = Closed(species, faces);

        return Row(
            species.Key,
            EffectSet.None,
            species.Playable,
            shut is null,
            shut,
            why,
            [new WikiCondition("Requirements", _reader.Read(species.Possible), "Any empire")],
            SpeciesFacts(species, faces, detail),
            Wants(species.Possible)) with
        {
            Icon = SpeciesFaces.Of(Database, species.Key),
            Gallery = [.. faces.Select(Face).OfType<EmpireChoice>()],
        };
    }

    /// <summary>
    /// Whether a condition turns every player away, whatever they own or choose.
    /// </summary>
    /// <remarks>
    /// Only an outright refusal counts. A content pack is a fact about the person rather than about
    /// the game, and every other kind of condition is a choice nobody has made yet - so the one
    /// thing that shuts a door is the game saying so.
    /// </remarks>
    private static bool Refused(Requirement? playable) =>
        playable is AlwaysRequirement { Value: false };

    /// <summary>
    /// Why a species class cannot be chosen, where it cannot, in two words and then in full.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same three tests the designer's own picker applies, in the same order, because a page
    /// saying a class is playable while the picker never offers it is worse than saying nothing.
    /// Only the first was read here, so seven classes with no faces at all were called playable.
    /// </para>
    /// <para>
    /// Refused outright is the game saying <c>always = no</c>, which nineteen carry. Appearance only
    /// is a class with no archetype - artwork a species can wear rather than something a species can
    /// be made of. And a class with no portraits is not a choice either: a species has to look like
    /// something, and Spinovore and Solarpunk have an archetype and no faces anywhere.
    /// </para>
    /// </remarks>
    /// <param name="species">The class.</param>
    /// <param name="faces">The portraits it can wear, which may be none.</param>
    /// <returns>The badge and the sentence, or a pair of nulls where it can be chosen.</returns>
    private static (string? Badge, string? Why) Closed(
        SpeciesClassDefinition species,
        IReadOnlyList<string> faces)
    {
        if (Refused(species.Playable))
        {
            return ("Unplayable", "The game's own designer does not offer this class, and an empire "
                + "naming one does not appear in its list.");
        }

        if (species.IsAppearanceOnly)
        {
            return ("Appearance only", "The game gives this class no archetype, so nothing can be "
                + "made of it. It is artwork another species wears - a set of ships, a look for a "
                + "city - rather than a species in its own right.");
        }

        return faces.Count == 0
            ? ("No portraits", "The game defines the class but gives it no faces anywhere, and a "
                + "species with no possible likeness is not a choice. The designer leaves it out "
                + "for the same reason.")
            : (null, null);
    }

    /// <summary>
    /// What is worth saying about a species class beyond the faces.
    /// </summary>
    /// <remarks>
    /// The archetype decides what the species is made of and half of what it may take; the forced
    /// trait is on every member of the class whatever else was chosen for them, which is a real cost
    /// and one the game states nowhere a player would look. The count of faces is a number worth
    /// sorting by: thirty for the humanoids and two for a pre-sapient.
    /// </remarks>
    private IReadOnlyList<WikiFact> SpeciesFacts(
        SpeciesClassDefinition species,
        IReadOnlyList<string> faces,
        SpeciesClassDetail? detail) =>
    [
        WikiFact.Of("Archetype", Archetypes(species.Archetype)),
        WikiFact.Of("Always has", Traits(species.ForcedTrait)),

        // Which ships it flies. Every one of the forty-two declares it, it has been extracted all
        // along, and it is the link between this page and the shipsets.
        WikiFact.Of("Flies", Shipsets(species.GraphicalCulture)),

        WikiFact.Of("Opens", Worlds(species.AddedPlanetClasses)),
        WikiFact.Of("Closes", Worlds(species.RemovedPlanetClasses)),
        WikiFact.Said("Portraits", faces.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)),

        // What a pre-sapient of this class becomes when somebody uplifts it, which is the entire
        // point of eleven of the forty-two rows and joined them to nothing.
        WikiFact.Of("Uplifts into", Classes(detail?.UpliftedInto)),

        WikiFact.Tagged(
            "Generation",
            detail is null
                ? []
                : [
                    .. new (bool Has, string Said)[]
                        {
                            (!detail.Randomised, "Never generated"),
                            (!detail.HasGenders, "No genders"),
                        }
                        .Where(m => m.Has)
                        .Select(m => m.Said),
                ]),
    ];

    /// <summary>
    /// Which of the game's shipset groups a set belongs to.
    /// </summary>
    /// <remarks>
    /// Biological or mechanical, as the game's own two records name them. A set that models no ships
    /// belongs to neither: mechanical is written as "anything but biological", so a set with no ship
    /// category at all would match it and be filed as a fleet it does not have.
    /// </remarks>
    /// <param name="shipCategory">What kind of ships the set builds, if any.</param>
    /// <returns>The group's name, or a word saying it has none.</returns>
    private string Grouping(string? shipCategory) =>
        shipCategory is { Length: > 0 }
            ? Database.ShipSets
                .Where(g => g.Includes(shipCategory))
                .Select(g => session.Localizer.Text(g.NameKey, Localizer.Prettify(g.Key)))
                .FirstOrDefault() ?? Localizer.Prettify(shipCategory)
            : "None of its own";

    /// <summary>Shipsets, which are named by their key shouted and say what they look like.</summary>
    /// <param name="keys">The graphical cultures.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Shipsets(params IEnumerable<string?> keys) =>
    [
        .. Real(keys).Select(k =>
        {
            var set = Database.GraphicalCulture(k);

            return new EmpireChoice(
                k,
                session.Localizer.Text(set?.NameKey, Localizer.Prettify(k)),
                set?.ShipPreview,
                null)
            {
                Description = set?.DescriptionKey,
            };
        }),
    ];

    /// <summary>One face, where the game has a picture of it.</summary>
    /// <remarks>
    /// A portrait with no picture is a group standing for others, and the others are in the list
    /// beside it - so drawing the group as well would be an empty tile among the faces it names.
    /// </remarks>
    private EmpireChoice? Face(string key)
    {
        var image = PortraitArtwork.For(Database, key, gender: null);

        if (image is not { Length: > 0 })
        {
            return null;
        }

        var portrait = Database.Portrait(key);

        return new EmpireChoice(
            key,
            session.Localizer.Text(portrait?.NameKey ?? key, Localizer.Prettify(key)),
            image,
            null);
    }

    // -------------------------------------------------------------------------------------------
    // The shelves the empire designer's own data already holds
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// The worlds, which say nothing about themselves.
    /// </summary>
    /// <remarks>
    /// Sixty-nine planet classes and not one description among them - there is no
    /// <c>pc_ocean_desc</c> anywhere. A chip borrows the habitability trait's prose, which is the
    /// right answer for one chip; a row does not, because the game writes fifteen distinct
    /// sentences across forty-eight preference traits and they all say climate preference is
    /// decided by evolution. Sixty-nine rows of the same paragraph is not prose, it is wallpaper.
    /// What a world actually has to say is in the numbers beside it.
    /// </remarks>
    private IReadOnlyList<WikiRow> PlanetRows(WorldPack? pack)
    {
        var detail = Detail(pack?.Worlds, w => w.Key);
        var reader = pack is null ? session.Localizer : Reading(pack.Text);

        // The same links read the other way round, which is the half a reader asks second: a Gaia
        // world is the target of forty-one of them and says so nowhere in its own record.
        var from = (pack?.Worlds ?? [])
            .SelectMany(w => w.Becomes.Select(t => (Source: w.Key, Link: t)))
            .GroupBy(pair => pair.Link.World, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Terraforming>)[.. g.Select(pair => pair.Link with { World = pair.Source })],
                StringComparer.Ordinal);

        return
        [
            .. Database.PlanetClasses
                .Select(w => Planet(
                    w, detail.GetValueOrDefault(w.Key), from.GetValueOrDefault(w.Key) ?? [], reader))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// One world, with the trait it grants standing in for its own prose and numbers.
    /// </summary>
    /// <remarks>
    /// Playable here means the game offers it to start on. Most of these are places an empire finds
    /// rather than places it wakes up on, and saying so is the whole difference between the two
    /// dozen a reader can pick from and the rest of the galaxy.
    /// </remarks>
    /// <param name="world">The planet class.</param>
    /// <param name="detail">What the page fetched about it, or null before that arrives.</param>
    /// <param name="from">The terraforming links that end here rather than start here.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its row.</returns>
    private WikiRow Planet(
        PlanetClassDefinition world,
        WorldDetail? detail,
        IReadOnlyList<Terraforming> from,
        Localizer reader)
    {
        var preference = session.Rules.HabitabilityTraitFor(world.Key);
        var opened = OpenedBy(world.Key);
        var offered = world.IsStartingWorld || opened.Count > 0;

        return Row(
            world.Key,
            // The world's own numbers where it states any, and the habitability trait's where it
            // does not. Fifteen classes declare a modifier block - a Gaia world gives ten per cent
            // to job output, to happiness and to growth - and until now the column beside them was
            // fed entirely by the trait, so a world's own bonuses appeared nowhere at all.
            detail?.Effects is { IsEmpty: false } own
                ? own
                : Database.Trait(preference)?.Effects ?? EffectSet.None,
            world.Potential,
            offered,
            offered ? null : "Not a homeworld",
            offered
                ? null
                : "Nothing offers this world to start on. An empire finds it during a game rather "
                    + "than waking up on it.",
            [new WikiCondition("Requirements", _reader.Read(world.Potential), "Any empire")],
            PlanetFacts(world, preference, opened, detail, from, reader),
            Wants(world.Potential)) with
        {
            Icon = world.Icon,

            // The whole view, not the sky. A world is drawn as its sky with bands of landscape in
            // front of it, so the sky alone is the clouds with nothing underneath - which is what
            // every planet card was showing. Composed by the same method the room scene uses, and
            // bare of a city, because this is the world rather than anybody's world.
            Layers = [.. WorldBackdrop.Layers(world, Skyline(world), level: 0)],
        };
    }

    /// <summary>
    /// Whose towers to paint on a world that is made of them.
    /// </summary>
    /// <remarks>
    /// The ecumenopolis, and nothing else in the folder: it declares a fixed city level of six and
    /// no landscape of its own, because the game paints the empire's own towers straight onto its
    /// sky and that is the surface. Drawn bare it was the clouds and nothing underneath, which is
    /// the fault the other worlds had and this one kept for a different reason.
    ///
    /// Borrowing a skyline is not drawing somebody's empire here - at the level the world fixes,
    /// the towers reach the horizon and are the world - so the first culture with city art stands
    /// in for all of them, and every other world stays bare.
    /// </remarks>
    /// <param name="world">The world.</param>
    /// <returns>The culture whose city to borrow, or null to draw the world as it comes.</returns>
    private GraphicalCultureDefinition? Skyline(PlanetClassDefinition world) =>
        world is { FixedCityLevel: not null, ShowsCity: true, Scenery.Count: 0 }
            ? Database.GraphicalCultures.FirstOrDefault(c => c.CityLayers.Count > 0)
            : null;

    /// <summary>
    /// What puts a world in the homeworld picker that would not otherwise be in it.
    /// </summary>
    /// <remarks>
    /// Nine worlds are starting worlds in the game's own files, and exactly one more is reachable:
    /// the volcanic world, which seven civics and origins and the Infernal species class each add.
    /// Reading only the flag called that world unreachable, which is the same mistake the species
    /// classes made - saying a thing is out of reach while the designer offers it.
    /// </remarks>
    /// <param name="world">The planet class.</param>
    /// <returns>The civics, origins and species classes that add it, which is usually none.</returns>
    private IReadOnlyList<EmpireChoice> OpenedBy(string world) =>
    [
        .. Civics(Database.Civics
            .Where(c => c.AddedPlanetClasses.Contains(world, StringComparer.Ordinal))
            .Select(c => c.Key)),

        .. Classes(Database.SpeciesClasses
            .Where(c => c.AddedPlanetClasses.Contains(world, StringComparer.Ordinal))
            .Select(c => c.Key)),
    ];

    /// <summary>
    /// What is worth saying about a world beyond the trait it grants.
    /// </summary>
    /// <remarks>
    /// The climate is the one that decides things: habitability is worked out within a climate
    /// group, so two wet worlds are close to each other and a long way from a dry one. Whether an
    /// empire builds a city on it tells the already-built worlds - habitats, hives, machine worlds -
    /// from the ones with ground to stand on.
    /// </remarks>
    /// <param name="world">The planet class.</param>
    /// <param name="preference">The habitability trait it grants, where it grants one.</param>
    /// <param name="opened">What adds it to the picker, where anything does.</param>
    /// <param name="detail">What the page fetched about it, or null before that arrives.</param>
    /// <param name="from">The terraforming links that end here rather than start here.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> PlanetFacts(
        PlanetClassDefinition world,
        string? preference,
        IReadOnlyList<EmpireChoice> opened,
        WorldDetail? detail,
        IReadOnlyList<Terraforming> from,
        Localizer reader) =>
    [
        // "None" rather than nothing for the thirty-eight outside the climate system, because a
        // fact nobody states on the first row is a column that ends up drawn last. It is also true:
        // a habitat has no climate, and habitability there is not worked out from one.
        WikiFact.Said(
            "Climate",
            world.Climate is { Length: > 0 } climate ? Localizer.Prettify(climate) : "None"),

        WikiFact.Said(
            "Start here",
            world.IsStartingWorld ? "Yes" : opened.Count > 0 ? "With a civic" : "No"),

        WikiFact.Of("Opened by", opened),
        WikiFact.Of("Preference", Traits(preference)),
        // Whether anybody lives here, which is the question after habitability and the one the page
        // could not answer. What stood here was show_city, and that is a fact about drawing the
        // backdrop rather than about the world: it says whether the game paints an empire's own
        // towers over the picture, which is why it read "Built on it" on a gas giant.
        WikiFact.Said("Colonisable", world.Colonizable ? "Yes" : "No"),

        WikiFact.Said("Ideal", detail is { Ideal: true } ? "Yes" : null),

        // Which is not the Preference above it. That one is what an empire founded here starts
        // with; this is what the game reaches for when it is picking a preference for a species
        // itself, and an ocean world answers the two questions differently.
        WikiFact.Of("Auto preference", Traits(detail?.AutoPreference ?? [])),

        // How big one turns up, which for the artificial worlds is a single size and for the rest a
        // range the galaxy rolls within.
        WikiFact.Said("Size", Between(detail?.SmallestSize, detail?.LargestSize)),
        WikiFact.Said("As a moon", Between(detail?.SmallestMoon, detail?.LargestMoon)),

        // The district set, which is a grouping the game gives no name of its own - Standard, Ring
        // World, Nomad are this app's prettified spelling of its key - and the district a colony
        // starts with, which the game does name. Two headings rather than one so that an internal
        // word is not sitting in the same list as a real one.
        WikiFact.Of("Districts", Chip(detail?.Districts, reader)),
        WikiFact.Of("Starts with", Chip(detail?.StartingDistrict, reader)),

        WikiFact.Said(
            "Housing",
            detail?.CarryCapacity is { } capacity ? Number(capacity) + " per free district" : null),

        // The one-word classifications, which the page could infer nothing equivalent to.
        WikiFact.Tagged("Kind", Kinds(detail)),

        // And what it can be turned into, from an entire folder nobody had opened - two hundred and
        // thirty links - which is the second question a reader asks after habitability. Then the
        // same links the other way, which is the third: a Gaia world is the end of fourteen of them
        // and its own record says so nowhere.
        WikiFact.Of("Becomes", Becomes(detail?.Becomes ?? [])),
        WikiFact.Of("Made from", Becomes(from)),

        // And what the empire has to have researched first, which the chips have no room for. One
        // line per distinct answer, naming the worlds it covers, because a world's links do not
        // agree: an ocean world reaches eight of its fourteen through Terrestrial Sculpting and the
        // other six from the start.
        WikiFact.Listed("Terraforming needs", Waits(detail?.Becomes ?? [], reader)),
    ];

    /// <summary>One district, named the way the game names it where it names it at all.</summary>
    /// <param name="district">The key, or null where the world states none.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>The chip, or nothing.</returns>
    private static IReadOnlyList<EmpireChoice> Chip(string? district, Localizer reader) =>
        district is { Length: > 0 } key
            ? [new EmpireChoice(key, reader.Text(key, Localizer.Prettify(key)), null, null)]
            : [];

    /// <summary>
    /// What a set of terraforming links waits on, one sentence for each distinct answer.
    /// </summary>
    /// <remarks>
    /// Read through the same writer every other condition on the site goes through, so a technology
    /// comes out as its name rather than as the key it is written under. Links that state nothing
    /// are left out entirely - saying "these eight need nothing" is not worth a line.
    /// </remarks>
    /// <param name="links">The links.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>The sentences.</returns>
    private IReadOnlyList<string> Waits(IReadOnlyList<Terraforming> links, Localizer reader)
    {
        var writer = new ConditionWriter(reader);

        return
        [
            .. links
                .Select(t => (Said: writer.Describe(t.Needs), t.World))
                .Where(pair => pair.Said is { Length: > 0 })
                .GroupBy(pair => pair.Said!, StringComparer.Ordinal)
                .Select(g => $"{Listing(g.Select(pair => pair.World))}: {g.Key}")
                .OrderBy(said => said, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>A range the game rolls within, or the one size it fixes.</summary>
    /// <param name="smallest">The low end.</param>
    /// <param name="largest">The high end.</param>
    /// <returns>The words, or nothing where the class states no size.</returns>
    private static string? Between(int? smallest, int? largest) => (smallest, largest) switch
    {
        (null, null) => null,
        ({ } only, null) => Number(only),
        (null, { } only) => Number(only),
        ({ } low, { } high) when low == high => Number(low),
        ({ } low, { } high) => $"{Number(low)} to {Number(high)}",
    };

    /// <summary>Several worlds named in a row, the way a sentence would name them.</summary>
    /// <param name="keys">The classes.</param>
    /// <returns>Their names, or the keys where the game names none.</returns>
    private string Listing(IEnumerable<string> keys) =>
        string.Join(", ", Worlds([.. keys]).Select(w => w.Name));

    /// <summary>The one-word classifications a world carries.</summary>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The labels.</returns>
    private static IReadOnlyList<string> Kinds(WorldDetail? detail) =>
        detail is null
            ? []
            : [
                .. new (bool Has, string Said)[]
                    {
                        (detail.Artificial, "Artificial"),
                        (detail.Ringworld, "Ring segment"),
                        (detail.Asteroid, "Asteroid"),
                        (detail.Habitat, "Habitat"),
                    }
                    .Where(k => k.Has)
                    .Select(k => k.Said),
            ];

    /// <summary>
    /// What this world can be terraformed into, each wearing how long the work takes.
    /// </summary>
    /// <remarks>
    /// The duration as the badge, in years rather than the days the game counts in, because a
    /// reader comparing two of these is comparing how long they wait. The cost is left where it is:
    /// every link states it through scripted inline calls, and a figure read out of one of those
    /// would be a number this app had assembled rather than one the game gives.
    /// </remarks>
    /// <param name="links">The links, in whichever direction the heading reads them.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Becomes(IReadOnlyList<Terraforming> links) =>
    [
        .. links
            .Select(t => Worlds(t.World).FirstOrDefault() is { } chip
                ? chip with { Badge = t.Days > 0 ? $"{Number(t.Days / 360)}y" : null }
                : null)
            .OfType<EmpireChoice>(),
    ];

    /// <summary>
    /// The shipsets, which the game names by showing you one.
    /// </summary>
    /// <remarks>
    /// Fifty-two sets and two names: <c>BIOGENESIS_01</c> is Spinovore and <c>BIOGENESIS_02</c> is
    /// Shellcraft, and every other set has no entry under any spelling. That is the game's own
    /// doing rather than a gap here - its picker spins the model and never writes the name - so the
    /// rest read as their key made readable, and the picture is the answer.
    /// </remarks>
    private IReadOnlyList<WikiRow> ShipsetRows(ShipsetPack? pack)
    {
        var fleets = (pack?.Fleets ?? []).ToDictionary(f => f.Set, StringComparer.Ordinal);
        var detail = Detail(pack?.Sets, d => d.Key);

        var reader = pack is null
            ? session.Localizer
            : Reading(pack.Text);

        return
        [
            .. Database.GraphicalCultures
                .Select(c => Shipset(
                    c, fleets.GetValueOrDefault(c.Key), detail.GetValueOrDefault(c.Key), reader))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// The app's own text with a pack's merged over it.
    /// </summary>
    /// <remarks>
    /// A pack carries the words <c>loc/en.json</c> cannot, which is pruned to what the database
    /// reaches - so a ship class is not in there and neither is a leader trait. Merged rather than
    /// read alone because a pack's own entries can refer to the app's.
    /// </remarks>
    /// <param name="carried">The pack's text.</param>
    /// <returns>A reader that answers for both.</returns>
    private Localizer Reading(IReadOnlyDictionary<string, string> carried)
    {
        var text = new Dictionary<string, string>(session.Data.Localisation, StringComparer.Ordinal);

        foreach (var (key, value) in carried)
        {
            text[key] = value;
        }

        // And what a personality's numbers and switches mean, which is ours rather than the game's -
        // see Scores and Behaviours. Put here so a chip can find it the way it finds everything
        // else, under a key of our own.
        foreach (var (field, said) in Scores.Concat(Behaviours))
        {
            text[Meaning(field)] = said.Means;
        }

        // And what an authority's politics amount to, which the game names nowhere - see Governing.
        foreach (var (field, said) in Governing)
        {
            text[Meaning(field)] = said;
        }

        return new Localizer(
            text,
            Database.TextIcons,
            session.Data.AssetUrl,
            Database.ScriptedValues,
            Database.ScriptedText);
    }

    /// <summary>
    /// The shipsets, with every ship each one flies.
    /// </summary>
    /// <remarks>
    /// The gallery is in the wiki's own file rather than the database. A hundred and forty renders
    /// across eighteen classes is a page's business; the designer's picker wants one picture and has
    /// a field for it, and widening that definition would cost every desktop player a re-read of
    /// thirty-five thousand files.
    /// </remarks>
    /// <param name="pack">The fleet, where the page has fetched it.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Shipsets(ShipsetPack? pack) =>
        new("Shipsets", "shipsets", "shipset", ShipsetRows(pack), WikiFacet.Shipsets)
        {
            Picture = "Ship",
            Shape = "ship",
            Reader = pack is null ? null : Reading(pack.Text),
        };

    /// <summary>One shipset, with a ship of its own drawn during extraction.</summary>
    /// <param name="culture">The graphical culture.</param>
    /// <param name="fleet">Every ship it flies, where the page has fetched them.</param>
    /// <param name="detail">What the set says about itself, where the page has fetched it.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its row.</returns>
    private WikiRow Shipset(
        GraphicalCultureDefinition culture,
        ShipsetFleet? fleet,
        ShipsetDetail? detail,
        Localizer reader)
    {
        var offered = culture.Selectable is not AlwaysRequirement { Value: false };

        return Row(
            culture.Key,
            EffectSet.None,
            culture.Selectable,
            offered,
            offered ? null : "Unplayable",
            offered
                ? null
                : "Nothing an empire does reaches this set. The designer will not offer it and the "
                  + "galaxy will not roll it either - the game's two gates agree - so it is worn "
                  + "only by the empires an event places.",
            [new WikiCondition("Requirements", _reader.Read(culture.Selectable), "Any empire")],
            ShipsetFacts(culture, detail),
            Wants(culture.Selectable),
            nameKey: culture.NameKey,
            proseKey: culture.DescriptionKey) with
        {
            Picture = culture.ShipPreview,

            // Every ship it flies, under the one drawn large. Named by class rather than by set,
            // because two sets sharing a picture share it by flying the same hulls.
            Gallery =
            [
                .. (fleet?.Ships ?? [])
                    .Select(ship => new EmpireChoice(
                        ship.ShipClass,
                        reader.Text(ship.ShipClass, Localizer.Prettify(ship.ShipClass)),
                        ship.Image,
                        null)),
            ],
        };
    }

    /// <summary>
    /// What tells one set from another besides the look of it.
    /// </summary>
    /// <remarks>
    /// Whether it flies ships of its own is the real division, and it is not the one a reader would
    /// guess: Solarpunk and Wilderness dress cities and declare no ships at all, so the game flies
    /// them in whatever their fallback builds. Which is why the fallback is named beside it.
    /// </remarks>
    /// <param name="culture">The graphical culture.</param>
    /// <param name="detail">What the set says about itself, where the page has fetched it.</param>
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> ShipsetFacts(
        GraphicalCultureDefinition culture,
        ShipsetDetail? detail) =>
    [
        // The game's own heading for the group, which it keeps in common/ship_sets purely "to
        // categorize the list of ship graphics cultures in the ship set browser" - its words. We
        // extract both and the designer's picker uses them; this page was re-deriving the same split
        // by hand from the string "bio_ship" and calling it something else.
        WikiFact.Said("Fleet", Grouping(culture.ShipCategory)),
        WikiFact.Said("Cities", culture.HasCityArt ? "Yes" : "No"),

        // Whether the hulls are painted in the empire's own colours or come as the artist drew them,
        // which is half the reason a reader is looking at this page at all. Twenty-five say yes and
        // the rest keep their own livery whatever the flag says.
        WikiFact.Said("Empire colours", detail is null ? null : detail.TakesColour ? "Yes" : "No"),
        WikiFact.Said(
            "Falls back to",
            culture.Fallback is { Length: > 0 } back ? Localizer.Prettify(back) : string.Empty),
    ];

    /// <summary>
    /// The personalities, which are what the game plays an empire as rather than what it is.
    /// </summary>
    /// <remarks>
    /// Nobody picks one. An empire that turns up in somebody else's galaxy is handed one, drawn
    /// from every personality its shape allows - so the question a reader has is which empires can
    /// be played as this, and the condition is the whole of the answer.
    /// </remarks>
    private IReadOnlyList<WikiRow> PersonalityRows(PersonalityPack? pack)
    {
        var detail = (pack?.Personalities ?? []).ToDictionary(d => d.Key, StringComparer.Ordinal);
        var reader = pack is null ? session.Localizer : Reading(pack.Text);

        return
        [
            .. Database.Personalities
                .Select(p => Personality(p, detail.GetValueOrDefault(p.Key), reader))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// The personalities, with how each of them plays.
    /// </summary>
    /// <remarks>
    /// The database carries which empires are drawn a personality and with what odds, because the
    /// designer shows that. How it behaves once drawn - what it will conquer, enslave and sign, what
    /// it arms its ships with - is in the wiki's own file, since an empire being designed has no AI
    /// to carry any of it.
    /// </remarks>
    /// <param name="pack">The detail, where the page has fetched it.</param>
    /// <returns>The shelf.</returns>
    public WikiShelf Personalities(PersonalityPack? pack) =>
        new("AI Personalities", "personalities", "personality",
            PersonalityRows(pack), WikiFacet.Personalities)
        {
            Reader = pack is null ? null : Reading(pack.Text),
        };

    /// <summary>One personality, named under the prefix the game keeps them under.</summary>
    /// <param name="personality">The personality.</param>
    /// <param name="detail">How it plays, where the page has fetched it.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its row.</returns>
    private WikiRow Personality(
        PersonalityDefinition personality,
        PersonalityDetail? detail,
        Localizer reader)
    {
        // Settled rather than read off the top of the tree. Two of these write the refusal as the
        // whole of allow and were marked; the other eighteen write it as one clause beside the
        // ethics they want - "is_country_type = fallen_empire", and then which fallen empire -
        // and were being offered as though a design could be played as one.
        var never = personality.Allow.Settled() is false;

        return Row(
            personality.Key,
            EffectSet.None,
            playable: null,
            reachable: !never,
            never ? "Never drawn" : null,
            never
                ? "The game keeps this one for a country it generates itself - a fallen empire, a "
                    + "pre-FTL world, a mirrored empire, the galactic defence force - and an empire "
                    + "you design is always an ordinary one. No design is ever played as it."
                : null,
            [new WikiCondition("Played by", _reader.Read(personality.Allow), "Any empire")],
            PersonalityFacts(personality, detail, reader),
            Wants(personality.Allow),
            nameKey: personality.NameKey,
            proseKey: personality.DescriptionKey);
    }

    /// <summary>
    /// How likely the draw is to land on this one.
    /// </summary>
    /// <remarks>
    /// Weight is additive here, which the game says itself at the top of its own file, and the
    /// additions on top of it are conditional - so this is what the personality starts with rather
    /// than what it ends at. Said as a number because a reader comparing two of them is comparing
    /// two numbers. How many additions there are is not a second fact: a column reading "3" says
    /// nothing a reader can do anything with.
    /// </remarks>
    /// <param name="personality">The personality.</param>
    /// <param name="detail">How it plays, where the page has fetched it.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> PersonalityFacts(
        PersonalityDefinition personality,
        PersonalityDetail? detail,
        Localizer reader) =>
    [
        WikiFact.Said("Weight", Number(personality.Weight)),

        // And what tips the draw toward it. Six of the fifty-one are pulled at by the empire's own
        // ethics, traits and civics - Evangelising Zealots gains two for a fanatic spiritualist and
        // one for Conformists, and loses one for Repugnant - which the record has carried all along
        // while the page printed a base weight and left the reader to guess what moved it.
        WikiFact.Of("More likely", Pulls(personality.Additions)),

        // What it will do, which is the question a reader arrives with: is this the neighbour that
        // takes planets, the one that enslaves, or the one that leaves you alone.
        WikiFact.Of("Behaviour", [.. (detail?.Behaviours ?? []).Select(Trait)]),

        WikiFact.Of("Attitude", Scored(detail?.Attitude)),
        WikiFact.Of("Will sign", Scored(detail?.Diplomacy)),
        WikiFact.Of("Ships", Scored(detail?.Fleet)),

        WikiFact.Said(
            "Weapons",
            detail?.Weapons is { Length: > 0 } arms ? reader.Text(arms, Localizer.Prettify(arms)) : null),
    ];

    /// <summary>
    /// What each of a personality's numbers is called, and what it decides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ours, because the game has none. These are script identifiers and nothing localises them, so
    /// the choice is between the key made readable - "Nap Acceptance", "Threat Others Modifier" -
    /// and saying it properly. The meanings are the game's own: every one is documented at length in
    /// the comment block its personality files open with.
    /// </para>
    /// <para>
    /// The suffixes go with the heading. Under "Will sign", every field ends in <c>_acceptance</c>
    /// and under "Ships" every one ends in <c>_ratio</c>, so keeping them said the same word seven
    /// times in a row.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, (string Name, string Means)> Scores =
        new(StringComparer.Ordinal)
        {
            ["aggressiveness"] = ("Aggressiveness", "How readily it declares war and insults its "
                + "neighbours, and how much of its fleet it commits when it does."),
            ["bravery"] = ("Bravery", "How willingly it picks a rival or a war target its own size "
                + "rather than one it can be sure of beating."),
            ["combat_bravery"] = ("Combat bravery", "How long it stays in a battle that is going "
                + "badly before it withdraws."),
            ["trade_willingness"] = ("Trade willingness", "How good a deal it wants. At 1.0 it will "
                + "take one that is even."),
            ["military_spending"] = ("Military spending", "The share of its minerals and energy that "
                + "goes to fleets and armies."),
            ["colony_spending"] = ("Colony spending", "The share that goes to settling new worlds."),
            ["threat_modifier"] = ("Threat", "How much threat it accrues when somebody else is "
                + "conquered."),
            ["threat_others_modifier"] = ("Threat to others", "How much threat everybody else "
                + "accrues when it is conquered."),
            ["friction_modifier"] = ("Border friction", "How much sharing a border with it sours "
                + "the relationship."),
            ["claims_modifier"] = ("Claims", "How badly it takes a claim made on its space."),
            ["advanced_start_chance"] = ("Advanced start", "How likely it is to be one of the "
                + "empires the galaxy starts ahead."),
            ["federation_acceptance"] = ("Federation", "Added to its chance of forming or joining one."),
            ["nap_acceptance"] = ("Non-aggression", "Added to its chance of signing a pact."),
            ["commercial_pact_acceptance"] = ("Commercial pact", "Added to its chance of signing one."),
            ["research_agreement_acceptance"] = ("Research agreement", "Added to its chance of "
                + "signing one."),
            ["migration_pact_acceptance"] = ("Migration pact", "Added to its chance of signing one."),
            ["defensive_pact_acceptance"] = ("Defensive pact", "Added to its chance of signing one."),
            ["loyalty_acceptance"] = ("Subject loyalty", "How much it cares whether its subjects are "
                + "loyal when weighing a deal with them."),
            ["armor_ratio"] = ("Armour", "The share of a ship's defences it wants in armour."),
            ["shields_ratio"] = ("Shields", "The share it wants in shields."),
            ["hull_ratio"] = ("Hull", "The share it wants in hull, where the technology allows."),
        };

    /// <summary>
    /// What each of a personality's behaviour switches is called, and what it decides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same arrangement as <see cref="Scores"/> and for the same reason: these are script words
    /// with no localisation anywhere, so a chip either says the key made readable or says what the
    /// switch means. The meanings are the game's own - eighteen of them are documented in the
    /// comment block the personality files open with, and four more in a note beside their only
    /// use.
    /// </para>
    /// <para>
    /// Four are documented nowhere: <c>attack_neutrals</c>, <c>displacer</c>, <c>wants_tribute</c>
    /// and <c>demands_clear_borders</c>. Those four are read from the name and say no more than the
    /// name does, which is marked here so nobody later mistakes them for the game's wording.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, (string Name, string Means)> Behaviours =
        new(StringComparer.Ordinal)
        {
            ["conqueror"] = ("Conqueror", "Will it take planets from other empires?"),
            ["subjugator"] = ("Subjugator", "Will it make other empires its vassals?"),
            ["liberator"] = ("Liberator", "Will it free the empires it conquers?"),
            ["opportunist"] = ("Opportunist", "Is it likelier to attack somebody already at war?"),
            ["uplifter"] = ("Uplifter", "Will it uplift and enlighten other species?"),
            ["infiltrator"] = ("Infiltrator", "Will it infiltrate pre-FTL worlds?"),
            ["dominator"] = ("Dominator", "Will it invade pre-FTL worlds?"),
            ["slaver"] = ("Slaver", "Will it enslave pops?"),
            ["purger"] = ("Purger", "Will it purge alien pops?"),
            ["robot_exploiter"] = ("Robot exploiter", "Will it use robots for menial labour?"),
            ["robot_liberator"] = ("Robot liberator", "Will it give robots rights?"),
            ["propagator"] = ("Propagator", "Will it turn aggressive only once it is boxed in?"),
            ["multispecies"] = ("Multispecies", "Will it give aliens rights?"),
            ["crisis_leader"] = ("Crisis leader", "Will it fight the crisis and call on others to "
                + "join it? A fallen empire's, which an emperor or a custodian does anyway."),
            ["crisis_fighter"] = ("Crisis fighter", "Will it consider fighting the crisis at all? "
                + "Without this it looks after itself and nobody else."),
            ["sneak_attacker"] = ("Sneak attacker", "Will it attack with cloaked fleets?"),
            ["isolationist"] = ("Isolationist", "It keeps its borders closed, always."),
            ["limited"] = ("Limited", "Several of the ordinary AI behaviours are held back for it."),
            ["holy_planets"] = ("Holy planets", "The spiritualist fallen empire's regard for the "
                + "worlds it holds sacred."),
            ["enigmatic"] = ("Enigmatic", "The machine fallen empires' own, which nothing else has."),
            ["custodian"] = ("Custodian", "An awakened machine fallen empire's: it settles nothing "
                + "further and its attitude is fixed."),
            ["berserker"] = ("Berserker", "The other of those: it settles nothing further and its "
                + "attitude is fixed."),

            // The four the game explains nowhere. Read from the name and saying no more than it.
            ["attack_neutrals"] = ("Attacks neutrals", "Whether it will attack an empire it has no "
                + "quarrel with."),
            ["displacer"] = ("Displacer", "Whether it drives alien pops off its worlds."),
            ["wants_tribute"] = ("Wants tribute", "Whether it would rather be paid than conquer."),
            ["demands_clear_borders"] = ("Demands clear borders", "Whether it insists other empires "
                + "stay out of its space."),
        };

    /// <summary>Where one of those sentences is kept, under a key of our own so nothing collides.</summary>
    /// <remarks>
    /// Not only a personality's any more: an authority's politics are named nowhere in the game
    /// either, and a chip reading "Mandates" over an empty panel told a reader nothing.
    /// </remarks>
    /// <param name="field">The field.</param>
    /// <returns>The key.</returns>
    private static string Meaning(string field) => $"sem_said_{field}_desc";

    /// <summary>
    /// A group of a personality's numbers, each chip wearing its own.
    /// </summary>
    /// <remarks>
    /// The number in front rather than in the name, which is what the chip's badge is for and what
    /// its own note says it was added for: a personality has no artwork in the game, and what tells
    /// one of these apart at a glance is the figure.
    /// </remarks>
    /// <param name="scores">The fields it states, which may be none.</param>
    /// <returns>The chips, in the order the group lists them.</returns>
    private static IReadOnlyList<EmpireChoice> Scored(IReadOnlyDictionary<string, double>? scores) =>
    [
        .. (scores ?? new Dictionary<string, double>(StringComparer.Ordinal))
            .Select(p => new EmpireChoice(
                p.Key,
                Scores.TryGetValue(p.Key, out var said) ? said.Name : Localizer.Prettify(p.Key),
                null,
                null)
            {
                Badge = Number(p.Value),
                BadgeLevel = p.Value,
                Description = Meaning(p.Key),
            }),
    ];

    /// <summary>
    /// What tips the draw toward a personality, and by how much.
    /// </summary>
    /// <remarks>
    /// Added rather than multiplied, which the game says at the top of its own file, so the badge
    /// carries a sign. A factor of nothing is left out: one personality names a civic and adds zero
    /// for it, which is the game keeping a hook it does not currently use.
    /// </remarks>
    /// <param name="factors">The weight additions, which for forty-five of them are none.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Pulls(IReadOnlyList<WeightFactor> factors) =>
        [.. factors.Where(f => f.Factor != 0).SelectMany(Pull)];

    /// <summary>
    /// One of those, drawn as the things it names.
    /// </summary>
    /// <remarks>
    /// A condition names one thing or several, and either way each is its own chip with its own
    /// picture, its own panel and a link to the page about it. Written as one sentence they were
    /// unreadable and in places actively misleading: "government Science Directorate or government
    /// Illuminated Autocracy" is two governments a reader might want to go and read about, and
    /// "election type democratic" looks like the name of an authority.
    ///
    /// Every chip in a factor wears the same number, because the factor is one addition however
    /// many things have to be true for it.
    /// </remarks>
    /// <param name="factor">The addition.</param>
    /// <returns>The chips, or one saying it in words where the condition names nothing.</returns>
    private IReadOnlyList<EmpireChoice> Pull(WeightFactor factor) =>
        Marked(factor, factor.Factor > 0 ? $"+{Number(factor.Factor)}" : Number(factor.Factor));

    /// <summary>
    /// A weight factor as chips, each wearing the figure the factor carries.
    /// </summary>
    /// <remarks>
    /// Shared by the two pages that show one. A personality's factors add and a government's
    /// multiply, so the badge differs and nothing else does - both are "what moves this number, and
    /// by how much".
    /// </remarks>
    /// <param name="factor">The factor.</param>
    /// <param name="badge">The figure, already written the way its page writes one.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Marked(WeightFactor factor, string badge)
    {
        if (Named(factor.When, denied: false) is { Count: > 0 } chips)
        {
            return [.. chips.Select(c => c with { Badge = badge, BadgeLevel = factor.Factor })];
        }

        return session.Conditions.Describe(factor.When) is { Length: > 0 } said
            ? [new EmpireChoice(said, said, null, null) { Badge = badge, BadgeLevel = factor.Factor }]
            : [];
    }

    /// <summary>
    /// Everything a condition names, as the chips those things are drawn as.
    /// </summary>
    /// <remarks>
    /// All of it or none of it. A condition with one part this cannot name is said in words
    /// instead, whole, rather than drawn as the parts it happens to recognise and quietly missing
    /// the rest.
    ///
    /// The connective is not kept, which is a loss worth naming. For the personalities it costs
    /// nothing - every compound there is a set of things an empire could not hold at once, one
    /// government or one degree of an ethic - but a government's factors name civics, and an empire
    /// can hold two of those at once. There the shared badge is what says the chips belong to one
    /// figure, and a bracketed tree in a row of chips would cost more than it told anybody.
    /// </remarks>
    /// <param name="requirement">The condition.</param>
    /// <param name="denied">Whether an odd number of negations stands over it.</param>
    /// <returns>The chips, or none where any part of it has no name.</returns>
    private IReadOnlyList<EmpireChoice> Named(Requirement requirement, bool denied)
    {
        switch (requirement)
        {
            case SelectionRequirement selection
                when Selected(selection.Category, selection.Key) is { } chosen:
                return [Refused(chosen, denied)];

            case FieldRequirement field when Stated(field) is { } stated:
                return [Refused(stated, denied)];

            case NotRequirement not:
                return Named(not.Item, !denied);

            case AllRequirement all:
                return Each(all.Items, denied);

            case AnyRequirement any:
                return Each(any.Items, denied);

            default:
                return [];
        }
    }

    /// <summary>Every part of a group, or nothing at all where one of them has no name.</summary>
    /// <param name="items">The parts.</param>
    /// <param name="denied">Whether an odd number of negations stands over them.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Each(IReadOnlyList<Requirement> items, bool denied)
    {
        List<EmpireChoice> chips = [];

        foreach (var item in items)
        {
            if (Named(item, denied) is not { Count: > 0 } named)
            {
                return [];
            }

            chips.AddRange(named);
        }

        return chips;
    }

    /// <summary>
    /// The same chip, said as the thing the empire must not be.
    /// </summary>
    /// <remarks>
    /// In the name rather than as a mark of its own, so it survives everywhere a chip goes - the
    /// row, the stack's panel, the tooltip - and so the picture and the link stay: a reader told
    /// that being repugnant costs a personality a point may well want to go and read about
    /// Repugnant.
    /// </remarks>
    /// <param name="chip">The chip.</param>
    /// <param name="denied">Whether it is the thing wanted or the thing refused.</param>
    /// <returns>The chip, named either way.</returns>
    private static EmpireChoice Refused(EmpireChoice chip, bool denied) =>
        denied ? chip with { Name = $"Not {chip.Name}" } : chip;

    /// <summary>One chosen thing, drawn the way its own kind is drawn.</summary>
    /// <param name="category">What kind of thing it is.</param>
    /// <param name="key">Which one.</param>
    /// <returns>The chip, or nothing for a kind that has no chip builder.</returns>
    private EmpireChoice? Selected(SelectionCategory category, string key) => category switch
    {
        SelectionCategory.Ethics => Ethics(key).FirstOrDefault(),
        SelectionCategory.Traits => Traits(key).FirstOrDefault(),
        SelectionCategory.Civics or SelectionCategory.Origin => Civics(key).FirstOrDefault(),
        SelectionCategory.SpeciesClass => Classes(key).FirstOrDefault(),
        SelectionCategory.SpeciesArchetype => Archetypes(key).FirstOrDefault(),
        SelectionCategory.Authority => Authorities(key).FirstOrDefault(),
        _ => null,
    };

    /// <summary>
    /// One plain field, drawn as whatever it names.
    /// </summary>
    /// <remarks>
    /// Most of these name a record the wiki has a page for, so the chip is the ordinary one and
    /// carries a link to it. The election type names nothing: it is a property of the authority
    /// rather than a thing in its own right, and said as the game spells it the word "democratic"
    /// sits on a chip where it reads as the authority of that name.
    /// </remarks>
    /// <param name="field">The condition.</param>
    /// <returns>The chip, or nothing for a field with nothing to point at.</returns>
    private EmpireChoice? Stated(FieldRequirement field) => field.Field switch
    {
        "government" => Governments(field.Value).FirstOrDefault(),
        "authority" => Authorities(field.Value).FirstOrDefault(),
        "origin" => Civics(field.Value).FirstOrDefault(),
        "species_class" => Classes(field.Value).FirstOrDefault(),
        "species_archetype" => Archetypes(field.Value).FirstOrDefault(),
        "planet_class" => Worlds(field.Value).FirstOrDefault(),
        "graphical_culture" => Shipsets(field.Value).FirstOrDefault(),
        "election_type" => Elections(field.Value),
        _ => null,
    };

    /// <summary>
    /// How an empire chooses whoever is in charge, said rather than spelled.
    /// </summary>
    /// <remarks>
    /// Three values in the whole game. Named ours, because the game writes them as bare script
    /// words with nothing behind them - there is no <c>election_type_democratic</c> anywhere in the
    /// localisation - and because the bare words are the problem: "democratic" beside a list of
    /// ethics reads as the authority called that, which is a different thing an empire also has.
    /// </remarks>
    /// <param name="value">What the condition asks for.</param>
    /// <returns>The chip, or nothing for a word this does not know.</returns>
    private static EmpireChoice? Elections(string value) => value switch
    {
        "democratic" => Saying(value, "Holds elections"),
        "oligarchic" => Saying(value, "Elected from a shortlist"),
        "none" => Saying(value, "No elections"),
        _ => null,
    };

    /// <summary>A chip that is a statement rather than a thing, under a key of our own.</summary>
    /// <param name="value">What the game called it.</param>
    /// <param name="said">What it means.</param>
    /// <returns>The chip.</returns>
    private static EmpireChoice Saying(string value, string said) =>
        new($"sem_election_{value}", said, null, null);

    /// <summary>
    /// One behaviour, with the game's own account of what it decides.
    /// </summary>
    /// <remarks>
    /// These are the AI's yes-or-no switches and carry no numbers of their own: a personality
    /// states them in a <c>behaviour</c> block and its figures separately, which is why this group
    /// is a row of names where every other one here is a row of badges. So the panel behind one had
    /// nothing to list - and nothing to say either, which made it "Robot Exploiter" over an empty
    /// box.
    /// </remarks>
    /// <param name="key">The flag.</param>
    /// <returns>The chip.</returns>
    private static EmpireChoice Trait(string key) =>
        new(
            key,
            Behaviours.TryGetValue(key, out var said) ? said.Name : Localizer.Prettify(key),
            null,
            null)
        {
            Description = Meaning(key),
        };

    /// <summary>
    /// The governments, which are what an empire ends up called.
    /// </summary>
    /// <remarks>
    /// A hundred and seventy of them and nobody picks one either: the game takes the
    /// highest-weighted whose conditions the design meets, which is how an empire becomes a Divine
    /// Empire rather than a Despotic Hegemony. The weight is therefore the second half of every
    /// answer and is a column rather than a footnote.
    /// </remarks>
    private IReadOnlyList<WikiRow> GovernmentRows(
        IReadOnlyDictionary<string, GovernmentDetail> detail) =>
    [
        .. Database.GovernmentTypes
            .Select(g => Government(g, detail.GetValueOrDefault(g.Key)))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>One government, with the titles it hands out.</summary>
    /// <param name="government">The government type.</param>
    /// <param name="detail">What the page fetched about it, or null before that arrives.</param>
    /// <returns>Its row.</returns>
    private WikiRow Government(GovernmentTypeDefinition government, GovernmentDetail? detail)
    {
        // Ninety-seven of the hundred and seventy are out of a design's reach, and they are out of
        // it for two quite different reasons - so they get two quite different sentences rather
        // than one badge reading "never used" over both.
        //
        // Settled rather than read off the top of the tree, for the reason the personalities give:
        // not one of these writes the refusal as the whole of its possible block. Every one writes
        // it as a clause beside the civics it wants, so asking only whether the top was a flat no
        // found none of them at all.
        var refused = government.Possible.Settled() is false;
        var onlyAi = refused && government.Possible
            .AndNested()
            .Any(r => r is AlwaysRequirement { Value: false, Because: "is_ai" });

        return Row(
            government.Key,
            EffectSet.None,
            playable: null,
            reachable: !refused,
            refused ? onlyAi ? "Only for the AI" : "Not at the start" : null,
            refused
                ? onlyAi
                    ? "An empire the game runs itself, and only ever that. Its own file says so at "
                        + "the top: none of the governments in it are available to player empires."
                    : "Reached during a game rather than at its start. It waits on something an "
                        + "event sets, which no empire has while it is being designed - so a design "
                        + "is never called this, and an empire that has played a while may be."
                : null,
            [new WikiCondition("Requirements", _reader.Read(government.Possible), "Any empire")],
            GovernmentFacts(government, detail),
            Wants(government.Possible));
    }

    /// <summary>
    /// What a government calls the people in charge.
    /// </summary>
    /// <remarks>
    /// Both forms of both titles, because the game writes both and they are not always a pair:
    /// Empress for Emperor and Matriarch for Patriarch are the same word changed, but a hundred and
    /// twenty-eight governments name a female ruler and only twenty-seven a female heir.
    /// </remarks>
    /// <param name="government">The government type.</param>
    /// <param name="detail">What the page fetched about it, or null before that arrives.</param>
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> GovernmentFacts(
        GovernmentTypeDefinition government,
        GovernmentDetail? detail) =>
    [
        // Why the empire got renamed when it reformed, which is a hundred and fifteen of them and
        // is the one thing about a government a player is most likely to have wondered about.
        WikiFact.Said("On reform", detail is { ForcesRename: true } ? "Renames the empire" : null),

        WikiFact.Tagged("Ruler names", Naming(detail)),

        WikiFact.Said("Ruler", Title(government.RulerTitleKey)),
        WikiFact.Said("Ruler (female)", Title(government.RulerTitleFemaleKey)),
        WikiFact.Said("Heir", Title(government.HeirTitleKey)),
        WikiFact.Said("Heir (female)", Title(government.HeirTitleFemaleKey)),
        WikiFact.Said("Weight", Number(government.Weight)),

        // And what multiplies it. The weight alone made two governments that rank differently for a
        // design look tied: thirteen of them double their own odds against a civic, which the record
        // has always carried and the page has never said. A militarist empire with Distinguished
        // Admiralty is twice as likely to be called a Star Empire.
        WikiFact.Of("More likely", Multipliers(government.Factors)),
    ];

    /// <summary>
    /// What raises a government's odds, and by how much.
    /// </summary>
    /// <remarks>
    /// The thing named as its own chip and the multiplier as the chip's badge, which is the shape
    /// the personality numbers already use - and every one of the fifteen names civics and nothing
    /// else, so all of them draw. Said as a sentence they read "civic Barbaric Despoilers and civic
    /// Death Cult", which is a line of script for two civics the wiki has pages about.
    /// </remarks>
    /// <param name="factors">The weight factors, which are usually none.</param>
    /// <returns>The chips, one per thing each factor names.</returns>
    private IReadOnlyList<EmpireChoice> Multipliers(IReadOnlyList<WeightFactor> factors) =>
        [.. factors.SelectMany(f => Marked(f, $"x{Number(f.Factor)}"))];

    /// <summary>One title as the game writes it, or nothing where it names none.</summary>
    /// <param name="key">The localisation key.</param>
    /// <returns>The title, or an empty string.</returns>
    private string Title(string? key) =>
        key is { Length: > 0 } ? session.Localizer.Text(key, Localizer.Prettify(key)) : string.Empty;

    /// <summary>
    /// A weight, written the way somebody comparing two of them would want to read it.
    /// </summary>
    /// <remarks>
    /// Whole numbers throughout - the game writes these as integers, and one government carries a
    /// hundred thousand to make sure it wins - so a decimal point here would be noise on every row.
    /// </remarks>
    /// <param name="weight">The weight.</param>
    /// <returns>The number.</returns>
    private static string Number(double weight) =>
        weight.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// The ascension perks, which are the one shelf here that states two conditions.
    /// </summary>
    /// <remarks>
    /// Read as one list, for the reason the civics give: the game hides a perk failing
    /// <c>potential</c> and blocks one failing <c>possible</c>, which is a distinction about how it
    /// refuses you rather than about whether it does.
    /// </remarks>
    private IReadOnlyList<WikiRow> AscensionPerks() =>
    [
        .. Database.AscensionPerks
            .Select(AscensionPerk)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>
    /// Which tier of the ascension a perk sits at, counting from one.
    /// </summary>
    /// <remarks>
    /// Read out of the condition rather than off the record, because the game keeps no tier field:
    /// it writes <c>num_ascension_perks &gt; 1</c> inside <c>possible</c> and means "this is a second
    /// tier perk". Twenty-four of the forty-nine say so. The comparison decides the arithmetic -
    /// "more than one" is tier two and "at least one" is tier one - and the highest wins where a perk
    /// states more than one, since they are all gates it must pass.
    /// </remarks>
    /// <param name="perk">The perk.</param>
    /// <returns>The tier, or null where it names none and so is open from the start.</returns>
    private static string? Tier(AscensionPerkDefinition perk)
    {
        var tiers = new AllRequirement([perk.Potential, perk.Possible])
            .AndNested()
            .OfType<CountRequirement>()
            .Where(c => c.Of == SelectionCategory.AscensionPerk)
            .Select(c => c.Comparison switch
            {
                CountComparison.Above => c.Value + 2,
                CountComparison.AtLeast => c.Value + 1,
                _ => 0,
            })
            .Where(t => t > 0)
            .ToList();

        return tiers.Count == 0
            ? null
            : tiers.Max().ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>One perk, with the path it belongs to.</summary>
    /// <param name="perk">The ascension perk.</param>
    /// <returns>Its row.</returns>
    private WikiRow AscensionPerk(AscensionPerkDefinition perk) =>
        Row(
            perk.Key,
            perk.Effects,
            perk.Potential,
            reachable: true,
            null,
            null,
            [
                new WikiCondition(
                    "Requirements",
                    _reader.Read(new AllRequirement([perk.Potential, perk.Possible])),
                    "Any empire"),
            ],
            // The path read rather than prettified. The game names these - "Ascensions",
            // "Ambitions" - and nothing asked for the key until this column existed, so the pruner
            // had thrown the words away and the column said "Ap Category Ascensions".
            [
                WikiFact.Said("Path", Title(perk.Category)),

                // How far in it sits. The game states this as a count of perks already taken rather
                // than as a number of its own, so it rendered as "Ascension Perk more than 2" inside
                // a bullet list - while the leader traits, which say tier plainly, have had it as a
                // fact and a facet all along.
                WikiFact.Said("Tier", Tier(perk)),

                .. Wordings(perk.Variants, perk.NameKey, perk.DescriptionKey),
            ],
            Wants(perk.Potential, perk.Possible)) with
        {
            Icon = perk.Icon,
        };

    /// <summary>
    /// The shared three-quarters: the name, the prose, the packs, the modifiers and the search text.
    /// </summary>
    /// <param name="key">The key the game uses.</param>
    /// <param name="effects">What it does.</param>
    /// <param name="playable">The condition that gates owning it, where its kind states one.</param>
    /// <param name="reachable">Whether a player could ever be offered it.</param>
    /// <param name="closedShort">Two words for a badge, where it is out of reach.</param>
    /// <param name="closedWhy">The whole of why.</param>
    /// <param name="conditions">What the game says an empire must be.</param>
    /// <param name="facts">What is true of its kind and not of the others.</param>
    /// <param name="wants">What its conditions ask for, or nothing where it states none.</param>
    /// <param name="nameKey">Where its name is written, where that is not its own key.</param>
    /// <param name="proseKey">Where its prose is written, where that is not its key and _desc.</param>
    /// <returns>The row, less the artwork its kind supplies.</returns>
    private WikiRow Row(
        string key,
        EffectSet effects,
        Requirement? playable,
        bool reachable,
        string? closedShort,
        string? closedWhy,
        IReadOnlyList<WikiCondition> conditions,
        IReadOnlyList<WikiFact> facts,
        IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>>? wants,
        string? nameKey = null,
        string? proseKey = null)
    {
        // The fallback is asked for twice on purpose. Text falls back when the key is missing, and
        // one civic's key is present and empty: the game ships civic_caravaneer_caravansary with a
        // blank name. Unnamed, it sorted to the front of the list and drew a card with an icon, a
        // badge and no title at all.
        //
        // The fallback is always the key prettified, whatever was asked for: a shipset's name lives
        // under its key shouted, and falling back to that would print "HUMANOID 01".
        var named = session.Localizer.Text(nameKey ?? key, Localizer.Prettify(key));
        var name = named is { Length: > 0 } ? named : Localizer.Prettify(key);

        // The convention every option chip already reads by, and most entries in the game are under
        // it. Not a property on the definition: adding one would be tidier and would cost a schema
        // bump, which every desktop player pays for by re-reading the game. The ones that keep their
        // prose elsewhere - a personality under a prefix, a world under the trait it grants - say so.
        var descriptionKey = proseKey ?? $"{key}_desc";
        var description = session.Localizer.Text(descriptionKey, string.Empty);

        var gates = ContentPacks.Gating(playable);
        var packs = gates.Select(Pack).ToList();

        return new WikiRow
        {
            Key = key,
            Name = name,
            Description = description,
            DescriptionKey = descriptionKey,
            Effects = effects,
            Playable = reachable,
            ClosedShort = closedShort,
            ClosedWhy = closedWhy,
            Packs = packs,
            Owned = ContentPacks.Satisfied(gates, session.OwnedDlc),
            PackChoices = [.. packs.Select(p => new EmpireChoice(p.Key, p.Name, p.Icon, null))],
            Conditions = [.. conditions.Where(c => c.Stated || c.Otherwise.Length > 0)],
            Facts = [.. facts.Where(f => f.Any)],
            Wants = wants ?? new Dictionary<SelectionCategory, IReadOnlyList<EmpireChoice>>(),
            Bonuses = Bonuses(effects),
            Text = $"{name} {description} {key}",
        };
    }

    /// <summary>One pack gate, with the badge the pack bar wears and whether the reader has it.</summary>
    private WikiPack Pack(PackGate gate)
    {
        var pack = Database.Dlc
            .FirstOrDefault(d => string.Equals(d.Name, gate.Name, StringComparison.Ordinal));

        return new WikiPack(
            gate.Name,
            session.Localizer.Text(pack?.NameKey, gate.Name),
            pack?.Icon,
            gate.Wanted,
            session.OwnedDlc.Contains(gate.Name));
    }

    /// <summary>
    /// What to say about one no player can reach, in two lengths.
    /// </summary>
    /// <remarks>
    /// The kinds of country are written out rather than printed as keys. The game names them
    /// <c>fallen_empire</c> and <c>caravaneer_fleet</c>, and a card saying "only caravaneer_fleet"
    /// is a row of script in the middle of a page of prose.
    /// </remarks>
    private static (string Short, string Why)? Shut(CivicReach reach)
    {
        if (reach.EverOffered)
        {
            return null;
        }

        if (reach.CountryTypes.Count == 0)
        {
            return ("Event only",
                "The game only ever grants this during a game. Nothing in the empire designer offers it.");
        }

        var kinds = string.Join(" or ", reach.CountryTypes.Select(Localizer.Prettify));

        return ("Unplayable", $"Only {kinds} is offered this, which a designed empire never is.");
    }

    /// <summary>
    /// What the conditions ask an empire to be, as choices the filter can offer.
    /// </summary>
    /// <remarks>
    /// What is asked against is left out - see <see cref="Selections"/> - because filing a ruled-out
    /// choice as a requirement puts every mutually exclusive pair on each other's lists.
    /// </remarks>
    private IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>> Wants(
        params Requirement?[] trees) =>
        trees
            .SelectMany(Selections.Required)
            .Where(s => Offered.Contains(s.Category))
            .DistinctBy(s => (s.Category, s.Key))
            .GroupBy(s => s.Category)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<EmpireChoice> (g) =>
                [
                    // The same chip the bullets draw, so a thing in a filter and the same thing in a
                    // requirement are recognisably one. Written out separately they were names with
                    // no artwork, which in a list of seventeen ethics is the difference between
                    // recognising one and reading them all.
                    .. g.Select(_reader.Chip).OrderBy(c => c.Name, StringComparer.CurrentCulture),
                ]);

    /// <summary>
    /// The kinds of selection worth narrowing by, out of the eleven there are.
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

    /// <summary>
    /// Every modifier it touches, the ones inside a swap included.
    /// </summary>
    /// <remarks>
    /// The conditional ones are in on purpose. Something whose whole value is a swap for gestalt
    /// empires would otherwise be filed as granting nothing, and the trait budget already learned
    /// this the hard way: reading only the always-on modifiers "lost every bonus the game states
    /// inside a swap".
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Bonuses(EffectSet effects) =>
    [
        .. effects.Modifiers.Keys
            .Concat(effects.Conditional.SelectMany(c => c.Modifiers.Keys))
            .Distinct(StringComparer.Ordinal)
            .Select(key => new EmpireChoice(key, session.Modifiers.Label(key), null, null))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>
    /// Every trait a founding species can be given, whether or not a player is offered it.
    /// </summary>
    /// <remarks>
    /// Read from the database, which already carries all three hundred and sixty-four: the
    /// designer's own picker needs them, so unlike the leader traits there is nothing to fetch.
    /// </remarks>
    private IReadOnlyList<WikiRow> SpeciesTraitRows(SpeciesTraitPack? pack)
    {
        var detail = Detail(pack?.Traits, t => t.Key);
        var reader = pack is null ? session.Localizer : Reading(pack.Text);
        var conditions = new ConditionReader(reader, Database);

        return
        [
            .. Database.Traits
                .Where(t => t.Kind == TraitKind.Species)
                .Select(t => SpeciesTrait(t, detail.GetValueOrDefault(t.Key), reader, conditions))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>
    /// One species trait.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A trait states its restrictions as flat lists rather than as a condition tree - there is no
    /// <c>Requirement</c> on one anywhere - so there is nothing for the condition reader to read and
    /// the requirement columns stay undrawn. What it may be taken with goes in the facts instead,
    /// as chips, which is the same answer read differently.
    /// </para>
    /// <para>
    /// Hidden is the honest "no player can take this"; not being initial is not. The twenty-four
    /// non-initial ones are gated on an origin and the game offers them once that origin is picked,
    /// which is what this app does too.
    /// </para>
    /// </remarks>
    private WikiRow SpeciesTrait(
        TraitDefinition trait,
        SpeciesTraitDetail? detail,
        Localizer reader,
        ConditionReader conditions) =>
        Row(
            trait.Key,
            trait.Effects,

            // Synthesised, because a trait states its pack as a name rather than as a condition.
            // Handing it over as one is what earns the pack chip and the owned mark for free.
            trait.RequiredDlc is { Length: > 0 } dlc ? new DlcRequirement(dlc) : null,
            !trait.Hidden,
            trait.Hidden ? "Unplayable" : null,
            trait.Hidden
                ? "The game keeps this one to itself. It is given out by an event or an origin and "
                    + "never offered in a list."
                : null,
            Gates(detail, conditions),
            SpeciesTraitFacts(trait, detail, reader),
            null) with
        {
            Icon = trait.Icon,
        };

    /// <summary>
    /// The leader traits, from the file the wiki fetched for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one shelf not read from the database. Handed in rather than looked up, because it
    /// arrives over the network some time after the page does - and where it could not be fetched
    /// at all the shelf is empty rather than absent, so the page says "nothing here" rather than
    /// spinning for ever.
    /// </para>
    /// <para>
    /// The text comes with it. A leader trait's name is not in the app's own localisation and never
    /// will be: that file is pruned to what the database reaches, and these are not in the database.
    /// </para>
    /// </remarks>
    /// <param name="pack">What was fetched, or null where nothing was.</param>
    /// <returns>The shelf, ready to draw.</returns>
    public WikiShelf LeaderTraits(LeaderTraitPack? pack)
    {
        var traits = pack?.Traits ?? [];

        // Read through a reader of its own rather than out of the pack's dictionary, because these
        // names are not plain words. The game writes a second tier as "$leader_trait_archaeologist$
        // II" - the name of the tier below, then a numeral - and it writes five of them as bracketed
        // commands only a running game can answer. Resolving all of that is what the localiser does,
        // and the pack's text is simply more entries for it to resolve against.
        var reader = Reading(pack?.Text ?? new Dictionary<string, string>(StringComparer.Ordinal));

        // By key, because a chip naming another trait has to find it: Replaces and Rules out both
        // carry keys from this same file, and looking each one up by walking the list would be
        // seven hundred scans for every one of seven hundred rows.
        var known = traits.ToDictionary(t => t.Key, StringComparer.Ordinal);

        // Over the pack's own text, so a chip inside one of these conditions is named the way the
        // page names everything else. The shelf's ordinary reader knows none of these keys.
        var conditions = new ConditionReader(reader, Database);

        return new WikiShelf(
            "Leader Traits", "leader traits", "leader trait",
            [
                .. Chains(traits, known.Keys.ToHashSet(StringComparer.Ordinal))
                    .Select(chain => Grouped(
                        [.. chain.Select(t => LeaderTrait(t, known, reader, conditions))]))
                    .OrderBy(r => r.Name, StringComparer.CurrentCulture),
            ],
            WikiFacet.LeaderTraits)
        {
            Reader = reader,
            Entries = known.Keys.ToHashSet(StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// The traits gathered into upgrade paths, each in the order a leader earns them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game states the path from the top down - a second tier names the first it replaces - so
    /// the chain is walked by joining every trait to what it replaces and reading each group back in
    /// tier order. Seven hundred and sixty-three traits come to four hundred and sixty-four paths,
    /// of which two hundred and fifty-nine are a single trait standing alone.
    /// </para>
    /// <para>
    /// Ordered within a path by the tier the game states, with the key as the tie-break so that two
    /// traits claiming the same tier do not swap places between renders.
    /// </para>
    /// </remarks>
    /// <param name="traits">Every trait the file carries.</param>
    /// <param name="carried">Their keys, so a replaced trait outside the file is not joined to.</param>
    /// <returns>The paths.</returns>
    private static IEnumerable<IReadOnlyList<LeaderTraitDefinition>> Chains(
        IReadOnlyList<LeaderTraitDefinition> traits,
        IReadOnlySet<string> carried)
    {
        // Whichever trait each one has been joined to so far, followed up to its own root. Plain
        // union-find: a path is at most three long and there are seven hundred of them, so the
        // clever forms of this would cost more to read than they save.
        var root = traits.ToDictionary(t => t.Key, t => t.Key, StringComparer.Ordinal);

        string Find(string key)
        {
            while (root[key] != key)
            {
                key = root[key] = root[root[key]];
            }

            return key;
        }

        foreach (var trait in traits)
        {
            foreach (var replaced in trait.Replaces.Where(carried.Contains))
            {
                var above = Find(trait.Key);
                var below = Find(replaced);

                if (above != below)
                {
                    root[above] = below;
                }
            }
        }

        return traits
            .GroupBy(t => Find(t.Key), StringComparer.Ordinal)
            .Select(chain => (IReadOnlyList<LeaderTraitDefinition>)
                [.. chain.OrderBy(t => t.Tier).ThenBy(t => t.Key, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// One entry standing for a whole upgrade path.
    /// </summary>
    /// <remarks>
    /// The first step's row, carrying the rest. Its facts, packs and search text are the union of
    /// every step's, because that is what a filter and a search are asked of: ticking "Tier 3" must
    /// find a path that has one, and typing a third tier's name must find the path it is a step of.
    /// A path of one is left exactly as it was, which is more than half of them.
    /// </remarks>
    /// <param name="steps">The path, in tier order.</param>
    /// <returns>The entry.</returns>
    private static WikiRow Grouped(IReadOnlyList<WikiRow> steps)
    {
        if (steps.Count == 1)
        {
            return steps[0];
        }

        return steps[0] with
        {
            Tiers = steps,

            // Under a separator no query can hold, so that a search cannot match across the join
            // between one step's text and the next's.
            Text = string.Join('\n', steps.Select(s => s.Text)),
            Facts = [.. Merged(steps.SelectMany(s => s.Facts))],
            Packs = [.. steps.SelectMany(s => s.Packs).DistinctBy(p => p.Key, StringComparer.Ordinal)],
            PackChoices =
                [.. steps.SelectMany(s => s.PackChoices).DistinctBy(c => c.Key, StringComparer.Ordinal)],
            Bonuses = [.. steps.SelectMany(s => s.Bonuses).DistinctBy(c => c.Key, StringComparer.Ordinal)],
            Owned = steps.All(s => s.Owned),
        };
    }

    /// <summary>
    /// One fact per heading, holding everything every step said under it.
    /// </summary>
    /// <remarks>
    /// A heading is asked once of an entry - by the filter, and by the table deciding its columns -
    /// so several steps answering it have to come back as one answer. Words are gathered in the
    /// order the steps state them, which for Tier is "1, 2, 3".
    /// </remarks>
    /// <param name="facts">Every step's facts.</param>
    /// <returns>One per heading.</returns>
    private static IEnumerable<WikiFact> Merged(IEnumerable<WikiFact> facts) =>
        facts
            .GroupBy(f => f.Heading, StringComparer.Ordinal)
            .Select(group => group.Any(f => f.Chips.Count > 0)
                ? WikiFact.Of(
                    group.Key,
                    [.. group.SelectMany(f => f.Chips).DistinctBy(c => c.Key, StringComparer.Ordinal)])
                : WikiFact.Said(
                    group.Key,
                    string.Join(
                        ", ",
                        group.Select(f => f.Text).OfType<string>().Distinct(StringComparer.Ordinal))));

    /// <summary>
    /// One leader trait.
    /// </summary>
    /// <remarks>
    /// Named through its own chain where it has no name of its own. Two hundred and thirty-four of
    /// these are the second and third tiers a leader earns while a game is running, and the game
    /// ships no text for them in any language - so the heading comes from the tier they replace,
    /// with the tier said beside it.
    /// </remarks>
    private WikiRow LeaderTrait(
        LeaderTraitDefinition trait,
        IReadOnlyDictionary<string, LeaderTraitDefinition> known,
        Localizer reader,
        ConditionReader conditions)
    {
        var named = Chained(trait, known, reader);
        var described = reader.Text(trait.DescriptionKey, string.Empty);

        return Row(
            trait.Key,
            trait.Effects,
            trait.RequiredDlc is { Length: > 0 } dlc ? new DlcRequirement(dlc) : null,
            reachable: true,
            null,
            null,

            // The whole of when a leader can actually be given it, which three hundred and one
            // traits write out. Drawn as an outline rather than said as a sentence: these are the
            // deepest conditions in the game outside an ascension perk, and run together they came
            // out as a paragraph nobody could parse.
            [new WikiCondition("Given when", conditions.Read(trait.CanBeGiven), "Nothing in particular")],

            LeaderTraitFacts(trait, known, reader),
            null) with
        {
            Icon = trait.Icon,
            Name = named,
            Description = described,
            Text = $"{named} {described} {trait.Key}",
        };
    }

    /// <summary>
    /// What to call a trait, following what it replaces where it has no name of its own.
    /// </summary>
    /// <remarks>
    /// The pack's own text first, then the app's - a handful of these chains end at a ruler trait,
    /// which is in the database and so is named in the ordinary localisation. Failing both, the key
    /// prettified, which reads as a name and is at least not a blank cell.
    /// </remarks>
    private static string Chained(
        LeaderTraitDefinition trait,
        IReadOnlyDictionary<string, LeaderTraitDefinition> known,
        Localizer reader,
        int depth = 0)
    {
        if (Plain(reader.Text(trait.NameKey, string.Empty)) is { } own)
        {
            return own;
        }

        // Four deep is more than any chain the game ships, and stops a pair that replace each other
        // from going round for ever.
        if (depth < 4)
        {
            foreach (var earlier in trait.Replaces)
            {
                if (known.GetValueOrDefault(earlier) is { } below &&
                    Chained(below, known, reader, depth + 1) is { Length: > 0 } inherited &&
                    inherited != Localizer.Prettify(below.Key))
                {
                    return inherited;
                }

                if (Plain(reader.Text(earlier, string.Empty)) is { } elsewhere)
                {
                    return elsewhere;
                }
            }
        }

        return Localizer.Prettify(trait.Key);
    }

    /// <summary>
    /// A name the app can actually show, or nothing.
    /// </summary>
    /// <remarks>
    /// Five of the game's leader traits are named by a bracketed command rather than by a word -
    /// <c>[GetChosenName]</c>, <c>[owner.GetRulerTitle]</c> - because what they are called depends
    /// on the leader holding them, which only a running game knows. Drawn as they stand they are
    /// not names, and being punctuation they sort to the very top of the page. Treated as absent,
    /// the tier chain and then the prettified key answer instead.
    /// </remarks>
    /// <param name="text">What the game says.</param>
    /// <returns>The name, or null where it is not one.</returns>
    private static string? Plain(string? text) =>
        text is { Length: > 0 } said && !said.TrimStart().StartsWith('[') ? said : null;

    /// <summary>What is worth saying about a leader trait beyond what it does.</summary>
    /// <param name="trait">The trait.</param>
    /// <param name="known">Every trait the file carries, for the chips that name one.</param>
    /// <param name="reader">The text, with the file's merged in.</param>
    /// <returns>The facts, in the order the columns want them.</returns>
    private IReadOnlyList<WikiFact> LeaderTraitFacts(
        LeaderTraitDefinition trait,
        IReadOnlyDictionary<string, LeaderTraitDefinition> known,
        Localizer reader) =>
    [
        // Said rather than a chip, because it is the answer to a yes-or-no question and a chip
        // saying "Yes" reads as a thing rather than as an answer. Blank for the rest, so the column
        // is a short list of the ones a player can have rather than seven hundred noes.
        WikiFact.Said("At start", trait.CanStart ? "Yes" : null),
        WikiFact.Of("Class", Leaders(trait.LeaderClasses)),
        // The game's own grouping - veteran, destiny, negative, subclass - and it writes them out in
        // its own text as "First Destiny Trait". Its field is called sort, which named the column
        // until somebody read the page: "Sort: Destiny" is a heading asking the reader to guess.
        // Kind is what the planets already call a one-word classification.
        WikiFact.Said("Kind", trait.Sort is { Length: > 0 } sort ? Localizer.Prettify(sort) : null),
        WikiFact.Said("Rarity", trait.Rarity is { Length: > 0 } rare ? Localizer.Prettify(rare) : null),

        // Said only where the game says it. A tier of zero is a trait that is not part of a chain
        // at all, and "0" in a column of 1s and 2s reads as a rank rather than as an absence.
        WikiFact.Said(
            "Tier",
            trait.Tier > 0 ? trait.Tier.ToString(System.Globalization.CultureInfo.CurrentCulture) : null),
        // No Replaces. It is what the grouping is: every chain the game states is whole in this
        // file, so a trait that replaces another is drawn as a later step of the same entry, and a
        // column repeating that beside it said the same thing twice.
        WikiFact.Of("Rules out", LeaderTraitChips(known, reader, trait.Opposites)),

        WikiFact.Said(
            "Cost",
            trait.Cost is { } price ? price.ToString(System.Globalization.CultureInfo.CurrentCulture) : null),

        // Whether the game will ever roll it at a level-up, and whether a leader can arrive holding
        // it. Two hundred and four say no to the first and two hundred and thirty-four to the
        // second, which between them is a quarter of the file a player will never simply be handed.
        WikiFact.Tagged("Comes up", Rolled(trait)),

        // Whether it counts as a councilor trait, which the game states in the same inline script
        // it states the icon, the rarity and the tier in - three of which were already read.
        WikiFact.Said("Council", Council(trait)),

        WikiFact.Of("Needs technology", Technologies(trait.Prerequisites, reader)),
        WikiFact.Of("Origin", Civics(trait.AllowedOrigins)),
        WikiFact.Of("Not for", Civics(trait.ForbiddenOrigins)),
        WikiFact.Of("Ethics", Ethics(trait.AllowedEthics)),

    ];

    /// <summary>How a leader comes to hold a trait, where the game rules one of the ways out.</summary>
    /// <param name="trait">The trait.</param>
    /// <returns>The labels.</returns>
    private static IReadOnlyList<string> Rolled(LeaderTraitDefinition trait) =>
    [
        .. new (bool Has, string Said)[]
            {
                (!trait.Initial, "Never at the start"),
                (!trait.Randomised, "Never rolled"),
                (trait.ImmortalLeaders, "Immortal leader"),
                (trait.ForcedCouncilor, "Counts as councilor"),
            }
            .Where(m => m.Has)
            .Select(m => m.Said),
    ];

    /// <summary>Whether a trait is a councilor's, in the game's own three words.</summary>
    /// <param name="trait">The trait.</param>
    /// <returns>The word, or nothing where the script names none.</returns>
    private static string? Council(LeaderTraitDefinition trait) => trait.Council switch
    {
        "yes" => "Yes",
        "no" => "No",
        { Length: > 0 } other => Localizer.Prettify(other),
        _ => null,
    };

    /// <summary>
    /// The technologies a leader trait waits on.
    /// </summary>
    /// <remarks>
    /// Eighteen name one - destroyers, cruisers, cloaking, battleships, sapient AI - and the game
    /// titles every key, which is what stage two's work on the ascension perks already proved.
    /// </remarks>
    /// <param name="keys">The technologies.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>The chips.</returns>
    private static IReadOnlyList<EmpireChoice> Technologies(
        IReadOnlyList<string> keys,
        Localizer reader) =>
    [
        .. keys
            .Where(k => k is { Length: > 0 })
            .Select(k => new EmpireChoice(k, reader.Text(k, Localizer.Prettify(k)), null, null)),
    ];

    /// <summary>What is worth saying about a species trait beyond what it does.</summary>
    private IReadOnlyList<WikiFact> SpeciesTraitFacts(
        TraitDefinition trait,
        SpeciesTraitDetail? detail,
        Localizer reader) =>
    [
        // Said as a number rather than as chips, and sortable, because the whole of picking traits
        // is spending a budget: two points for Intelligent, and a drawback to pay for it.
        WikiFact.Said("Cost", trait.Cost.ToString(System.Globalization.CultureInfo.CurrentCulture)),
        // Which ascension unlocks it, in the game's own word: robotic, cyborg, overtuned, psionic.
        // A hundred and ninety traits say so, the extractor has always read it, and the page has
        // never drawn it - so there was no way to ask for the cyborg traits.
        WikiFact.Said("Category", Localizer.Prettify(trait.Category ?? string.Empty)),

        WikiFact.Of("Archetype", Archetypes(trait.AllowedArchetypes)),
        WikiFact.Of("Only for", Classes(trait.AllowedSpeciesClasses)),
        WikiFact.Of("Rules out", Traits(trait.Opposites)),
        WikiFact.Of("Homeworld", Worlds(trait.AllowedPlanetClasses)),
        WikiFact.Of("Origin", Civics(trait.AllowedOrigins)),

        // And the origin that rules it out, which is the one flat list on the record that reached
        // no column. One trait says it - Sedentary, which Void Dwellers will not have - and a
        // heading that one row answers is still a heading that row was missing.
        WikiFact.Of("Not for", Civics(trait.ForbiddenOrigins)),
        WikiFact.Of("Not with", Ethics(trait.ForbiddenEthics)),
        WikiFact.Of("Needs civic", Civics(trait.AllowedCivics)),

        // The words the game groups it under, which carry no text of their own and exist purely to
        // be filtered by - which is exactly what a facet is. Two hundred and eighty-eight traits
        // carry them and there was no way to ask the page for the negative ones.
        WikiFact.Tagged("Tags", Grouped(detail)),

        WikiFact.Said("Worth", Traded(detail?.SlaveCost)),

        // What its pops pay for and produce, which for several traits is the whole of what they do:
        // the effects reader matches modifier blocks, and a resources block is not one, so
        // Scintillating Skin and Gaseous Byproducts reached the page with nothing at all.
        WikiFact.Of("Pays", Resources(detail, reader, upkeep: true)),
        WikiFact.Of("Produces", Resources(detail, reader, upkeep: false)),

        WikiFact.Of("Bound to", Worlds(detail?.BoundToWorlds ?? [])),

        // The five the game states as plain flags, said together because each is a headline and
        // none of them is worth a column of its own.
        WikiFact.Tagged("Species", Marks(detail)),

    ];

    /// <summary>The words a trait is grouped under, made readable.</summary>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The labels.</returns>
    private static IReadOnlyList<string> Grouped(SpeciesTraitDetail? detail) =>
        [.. (detail?.Tags ?? []).Select(Localizer.Prettify)];

    /// <summary>What a pop carrying it fetches on the slave market.</summary>
    /// <param name="cost">The figure, where the trait states one.</param>
    /// <returns>The words, or nothing.</returns>
    private static string? Traded(int? cost) =>
        cost is { } trade ? Number(trade) : null;

    /// <summary>
    /// The five plain flags a trait can raise, said as the things they are.
    /// </summary>
    /// <remarks>
    /// Together under one heading rather than five columns of mostly nothing: between them only
    /// forty-six traits raise any of them, and a reader wants to know which ones do rather than to
    /// compare five columns of blanks.
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <returns>The labels.</returns>
    private static IReadOnlyList<string> Marks(SpeciesTraitDetail? detail) =>
        detail is null
            ? []
            : [
                .. new (bool Has, string Said)[]
                    {
                        (detail.Advanced, "Advanced"),
                        (!detail.Sapient, "Pre-sapient"),
                        (detail.Infertile, "Infertile"),
                        (detail.ImmortalLeaders, "Immortal leaders"),
                        (detail.ImprovesLeaders, "Improves leaders"),
                    }
                    .Where(m => m.Has)
                    .Select(m => m.Said),
            ];

    /// <summary>
    /// What a trait makes its pops pay for, or produce.
    /// </summary>
    /// <remarks>
    /// Named rather than measured, for the reason the record gives: forty-three of the forty-four
    /// blocks carry a trigger and five scale by a scripted multiplier, so a figure beside one would
    /// be a number the trait does not give. Which resource, and which way, is what was missing.
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <param name="upkeep">Which of the two headings is being built.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Resources(
        SpeciesTraitDetail? detail,
        Localizer reader,
        bool upkeep) =>
    [
        .. (detail?.Resources ?? [])
            .Where(r => r.Upkeep == upkeep)
            .Select(r => new EmpireChoice(
                r.Resource,
                reader.Text(r.Resource, Localizer.Prettify(r.Resource)),
                null,
                null)),
    ];

    /// <summary>
    /// The conditions a species trait states about being added to a species, or taken off it.
    /// </summary>
    /// <remarks>
    /// Drawn the way every other condition in the wiki is drawn - a bulleted tree of ticks and
    /// crosses against named things - rather than run together into a sentence. Most of them are a
    /// bare yes or no, and a flat yes draws nothing at all: an outline says what stands in the way,
    /// and nothing standing in the way is what the heading's own fallback is for.
    /// </remarks>
    /// <param name="detail">What the page fetched, or null before it arrives.</param>
    /// <param name="reader">A reader over the text the pack brought with it.</param>
    /// <returns>The three columns.</returns>
    private static IReadOnlyList<WikiCondition> Gates(
        SpeciesTraitDetail? detail,
        ConditionReader reader) =>
    [
        new WikiCondition("Added later", reader.Read(detail?.CanAddLater), "Always"),
        new WikiCondition("Removed later", reader.Read(detail?.CanRemoveLater), "Always"),

        // Thirty-two traits let a species of the wrong class hold them anyway, which is the escape
        // hatch from the "Only for" column beside it - a column the page had been drawing as though
        // it were absolute.
        new WikiCondition("Other classes", reader.Read(detail?.ClassOverride), "Only its own"),
    ];

    /// <summary>
    /// One chip, told what kind of thing it is naming.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces a single method that took a bare key and guessed by trying each lookup in turn. A
    /// key it could not place got no picture, no effects and an empty panel on hover - which was
    /// most of them: leader classes, species classes and planet classes are all outside the chain it
    /// tried, and so is every key that lives in the wiki's own file rather than the database.
    /// </para>
    /// <para>
    /// Guessing was also answering the wrong question in one case. <c>LITHOID</c> and <c>MACHINE</c>
    /// are both archetype keys and species-class keys, so an Archetype chip found a species class
    /// and pointed at its page. A chip that is told what it names cannot make that mistake.
    /// </para>
    /// </remarks>
    /// <param name="key">What the chip carries.</param>
    /// <param name="icon">Its picture.</param>
    /// <param name="effects">What it does, for the panel behind it.</param>
    /// <param name="description">Where its prose lives, when that is not its own key.</param>
    /// <returns>The chip.</returns>
    private EmpireChoice Chip(
        string key,
        string? icon,
        EffectSet? effects = null,
        string? description = null) =>
        new(key, session.Localizer.Text(key, Localizer.Prettify(key)), icon, effects)
        {
            Description = description,
        };

    /// <summary>Only the keys that name something, since the game writes empty lists freely.</summary>
    private static IEnumerable<string> Real(IEnumerable<string?> keys) =>
        keys.OfType<string>().Where(k => k.Length > 0);

    /// <summary>Ethics, which carry both a picture and what they do.</summary>
    private IReadOnlyList<EmpireChoice> Ethics(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, Database.Ethic(k)?.Icon, Database.Ethic(k)?.Effects))];

    /// <summary>Species traits.</summary>
    private IReadOnlyList<EmpireChoice> Traits(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, Database.Trait(k)?.Icon, Database.Trait(k)?.Effects))];

    /// <summary>Civics and origins, which are the same record told apart by a flag.</summary>
    private IReadOnlyList<EmpireChoice> Civics(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, Database.Civic(k)?.Icon, Database.Civic(k)?.Effects))];

    /// <summary>
    /// Archetypes, which wear the trait every species of them carries.
    /// </summary>
    /// <remarks>
    /// And speak with its voice too, for the reason <see cref="Worlds"/> gives: the game writes no
    /// description for an archetype - there is no <c>BIOLOGICAL_desc</c> - so the picture was
    /// borrowed and the words were not, and every Archetype chip opened a panel reading "Biological.
    /// No effects." The trait is not a stand-in here either: every species of the archetype carries
    /// it, so what it says and what it does are true of all of them.
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Archetypes(params IEnumerable<string?> keys) =>
    [
        .. Real(keys).Select(k =>
        {
            var mark = ArchetypeMarks.Trait(Database, k);

            return Chip(
                k,
                Database.Trait(mark)?.Icon,
                Database.Trait(mark)?.Effects,
                mark is { Length: > 0 } trait ? $"{trait}_desc" : null);
        }),
    ];

    /// <summary>
    /// Governments, which have no picture and no prose of their own.
    /// </summary>
    /// <remarks>
    /// A name and a link, which is the whole of what one of these is worth as a chip and is exactly
    /// what was missing: the page about a government is where its titles and its odds are, and a
    /// personality naming one had been saying the key in a sentence.
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Governments(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, null, null))];

    /// <summary>Authorities, which carry both a picture and what they do.</summary>
    private IReadOnlyList<EmpireChoice> Authorities(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, Database.Authority(k)?.Icon, Database.Authority(k)?.Effects))];

    /// <summary>Species classes, which wear one of their own faces.</summary>
    private IReadOnlyList<EmpireChoice> Classes(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, SpeciesFaces.Of(Database, k)))];

    /// <summary>
    /// Leader classes, which have a badge of their own.
    /// </summary>
    /// <remarks>
    /// Three of the four are baked - the envoy asks for a frame past the end of the sheet, and may
    /// not rule, so it is never offered and never named here.
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Leaders(params IEnumerable<string?> keys) =>
        [.. Real(keys).Select(k => Chip(k, Database.LeaderClass(k)?.Icon))];

    /// <summary>
    /// Starting systems, which an origin locks an empire into.
    /// </summary>
    /// <remarks>
    /// Named under the key with <c>_NAME</c> after it rather than under the key itself, which is the
    /// game's own arrangement and why one read plainly comes out as "Custom Starting Init 01" where
    /// the game says "Random Trinary I".
    /// </remarks>
    /// <param name="keys">The systems.</param>
    /// <returns>The chips.</returns>
    private IReadOnlyList<EmpireChoice> Systems(params IEnumerable<string?> keys) =>
    [
        .. Real(keys).Select(k =>
        {
            var system = Database.Initializer(k);

            return new EmpireChoice(
                k,
                session.Localizer.Text(system?.NameKey, Localizer.Prettify(k)),
                null,
                null)
            {
                Description = system?.DescriptionKey,
            };
        }),
    ];

    /// <summary>
    /// Homeworlds, which say nothing about themselves.
    /// </summary>
    /// <remarks>
    /// The game writes no description for a planet class at all - there is no <c>pc_ocean_desc</c>
    /// anywhere - so a world borrows the prose of the habitability trait living there would give,
    /// which is the only thing it has to say. The designer's own picker has always done this; the
    /// wiki was showing a bare word with an empty panel behind it.
    /// </remarks>
    private IReadOnlyList<EmpireChoice> Worlds(params IEnumerable<string?> keys) =>
    [
        .. Real(keys).Select(k =>
        {
            var habitability = session.Rules.HabitabilityTraitFor(k);

            return Chip(
                k,
                Database.PlanetClass(k)?.Icon,
                Database.Trait(habitability)?.Effects,
                habitability is { Length: > 0 } trait ? $"{trait}_desc" : null);
        }),
    ];

    /// <summary>
    /// Leader traits, which are in the wiki's own file rather than the database.
    /// </summary>
    /// <remarks>
    /// Named through the reader the shelf built, which has the file's text merged over the app's.
    /// Without it a chip fell back to the key prettified - "Leader Trait Armada Logistician II" -
    /// and reading the file's text raw would have been worse still, since the game writes a second
    /// tier's name as a reference to the first.
    /// </remarks>
    /// <param name="known">Every trait the file carries, by key.</param>
    /// <param name="reader">The text, with the file's merged in.</param>
    /// <param name="keys">What to name.</param>
    /// <returns>The chips.</returns>
    private static IReadOnlyList<EmpireChoice> LeaderTraitChips(
        IReadOnlyDictionary<string, LeaderTraitDefinition> known,
        Localizer reader,
        IEnumerable<string?> keys) =>
    [
        .. Real(keys).Select(k =>
        {
            var trait = known.GetValueOrDefault(k);

            return new EmpireChoice(
                k,

                // Through the chain, as the row heading is. A chip naming one of the two hundred
                // and thirty-four tiers the game never named would otherwise read as its key
                // prettified - "Leader Trait Adventurous Spirit 2" - beside a row headed with the
                // real name.
                trait is null ? reader.Text(k, Localizer.Prettify(k)) : Chained(trait, known, reader),
                trait?.Icon,
                trait?.Effects);
        }),
    ];
}
