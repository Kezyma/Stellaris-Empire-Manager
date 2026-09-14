using Sem.GameData;

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
    IReadOnlyList<Facet<WikiRow>> Facets);

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

    private WikiShelf Read(WikiKind kind) => kind switch
    {
        WikiKind.Origins => new WikiShelf(
            "Origins", "origins", "origin", Civics(origins: true), WikiFacet.Civics),

        WikiKind.Ethics => new WikiShelf(
            "Ethics", "ethics", "ethic", Ethics(), WikiFacet.Ethics),

        WikiKind.Authorities => new WikiShelf(
            "Authorities", "authorities", "authority", Authorities(), WikiFacet.Authorities),

        WikiKind.Species => new WikiShelf(
            "Species", "species classes", "species class", Species(), WikiFacet.Species),

        _ => new WikiShelf("Civics", "civics", "civic", Civics(origins: false), WikiFacet.Civics),
    };

    /// <summary>
    /// The civics, or the origins, which are the same records with a flag set.
    /// </summary>
    /// <remarks>
    /// Reachability is worked out across all three hundred and fifty-eight at once even when only
    /// half of them are wanted, because it has to be: a civic can be out of reach only because
    /// another one is, and three origins are a ring that each ask for one of the others.
    /// </remarks>
    private IReadOnlyList<WikiRow> Civics(bool origins)
    {
        var reach = CivicReach.Across(Database.Civics);

        return
        [
            .. Database.Civics
                .Where(c => c.IsOrigin == origins)
                .Select(c => Civic(c, reach[c.Key]))
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
    private WikiRow Civic(CivicDefinition civic, CivicReach reach)
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
            [],
            Wants(civic.Potential, civic.Possible)) with
        {
            Icon = civic.Icon,
            Picture = civic.Picture,
        };
    }

    /// <summary>
    /// The ethics, which state no conditions at all.
    /// </summary>
    /// <remarks>
    /// Seventeen records with no <c>playable</c> and no <c>possible</c> between them, so there is
    /// nothing to gate them and every one is within reach of anybody. What an ethic costs, what it
    /// rules out and which form of itself it has are the questions instead, and they are facts
    /// rather than conditions.
    /// </remarks>
    private IReadOnlyList<WikiRow> Ethics() =>
    [
        .. Database.Ethics
            .Select(Ethic)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    private WikiRow Ethic(EthicDefinition ethic) =>
        Row(
            ethic.Key,
            ethic.Effects,
            playable: null,
            reachable: true,
            null,
            null,
            [],
            EthicFacts(ethic),
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
    private IReadOnlyList<WikiFact> EthicFacts(EthicDefinition ethic) =>
    [
        WikiFact.Said("Cost", ethic.Cost.ToString(System.Globalization.CultureInfo.CurrentCulture)),

        WikiFact.Said(
            "Intensity",
            ethic.IsGestalt ? "Gestalt" : ethic.IsFanatic ? "Fanatic" : "Ordinary"),

        // One heading rather than "Stronger form" and "Milder form", which is what it was. The
        // direction reads better on a card and is a disaster in a table: the heading differs by row,
        // so the column list is the union of both and every ethic fills one of them and leaves the
        // other blank. The chip says which way round it is anyway - the fanatic form is the one with
        // "Fanatic" in its name.
        WikiFact.Of(
            "Other form",
            Named(ethic.IsFanatic ? ethic.RegularVariant : ethic.FanaticVariant)),

        ethic.IsGestalt
            ? WikiFact.Said("Rules out", "Every other ethic")
            : WikiFact.Of("Rules out", Named(Opposing(ethic))),
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
    /// The authorities, whose reachability the rules layer already has an opinion about.
    /// </summary>
    /// <remarks>
    /// <c>AiOnly</c> rather than a condition, and that is <c>EmpireRules</c>'s own decision restated:
    /// two authorities declare a <c>potential</c> about the kind of country, the game's designer does
    /// not read it, and honouring it would hide Machine Intelligence from the player who is entitled
    /// to it. What actually keeps one out of the list is the flag.
    /// </remarks>
    private IReadOnlyList<WikiRow> Authorities() =>
    [
        .. Database.Authorities
            .Select(Authority)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    private WikiRow Authority(AuthorityDefinition authority) =>
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
            AuthorityFacts(authority),
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
    private IReadOnlyList<WikiFact> AuthorityFacts(AuthorityDefinition authority) =>
    [
        WikiFact.Said("Elections", Localizer.Prettify(authority.ElectionType)),
        WikiFact.Said("Heir", authority.HasHeir ? "Yes" : "No"),
        WikiFact.Of("Forces", Named(authority.ForcedTraits)),
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
    private IReadOnlyList<WikiRow> Species() =>
    [
        .. Database.SpeciesClasses
            .Select(SpeciesClass)
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
    /// The pack is not read here. <see cref="Row"/> is handed the same tree and works the packs out
    /// of it, which is how Toxoid comes to carry a Toxoids chip without this method mentioning one.
    /// </para>
    /// </remarks>
    private WikiRow SpeciesClass(SpeciesClassDefinition species)
    {
        var faces = SpeciesFaces.All(Database, species.Key);
        var shut = Refused(species.Playable);

        return Row(
            species.Key,
            EffectSet.None,
            species.Playable,
            !shut,
            shut ? "Unplayable" : null,
            shut
                ? "The game's own designer does not offer this class, and an empire naming one does "
                    + "not appear in its list."
                : null,
            [new WikiCondition("Requirements", _reader.Read(species.Possible), "Any empire")],
            SpeciesFacts(species, faces),
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
    /// What is worth saying about a species class beyond the faces.
    /// </summary>
    /// <remarks>
    /// The archetype decides what the species is made of and half of what it may take; the forced
    /// trait is on every member of the class whatever else was chosen for them, which is a real cost
    /// and one the game states nowhere a player would look. The count of faces is a number worth
    /// sorting by: thirty for the humanoids and two for a pre-sapient.
    /// </remarks>
    private IReadOnlyList<WikiFact> SpeciesFacts(SpeciesClassDefinition species, IReadOnlyList<string> faces) =>
    [
        WikiFact.Of("Archetype", Named(species.Archetype)),
        WikiFact.Of("Always has", Named(species.ForcedTrait)),
        WikiFact.Said("Portraits", faces.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)),
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
        IReadOnlyDictionary<SelectionCategory, IReadOnlyList<EmpireChoice>>? wants)
    {
        // The fallback is asked for twice on purpose. Text falls back when the key is missing, and
        // one civic's key is present and empty: the game ships civic_caravaneer_caravansary with a
        // blank name. Unnamed, it sorted to the front of the list and drew a card with an icon, a
        // badge and no title at all.
        var named = session.Localizer.Text(key, Localizer.Prettify(key));
        var name = named is { Length: > 0 } ? named : Localizer.Prettify(key);

        // The convention every option chip already reads by, and every entry in the game is under
        // it. Not a property on the definition: adding one would be tidier and would cost a schema
        // bump, which every desktop player pays for by re-reading the game.
        var descriptionKey = $"{key}_desc";
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

    /// <summary>Keys as the chips the rest of the app draws them as.</summary>
    private IReadOnlyList<EmpireChoice> Named(params IEnumerable<string?> keys) =>
    [
        .. keys.OfType<string>()
            .Where(k => k.Length > 0)
            .Select(k => new EmpireChoice(
                k,
                session.Localizer.Text(k, Localizer.Prettify(k)),
                Database.Ethic(k)?.Icon ?? Database.Trait(k)?.Icon ?? Database.Civic(k)?.Icon,
                Database.Ethic(k)?.Effects ?? Database.Trait(k)?.Effects)),
    ];
}
