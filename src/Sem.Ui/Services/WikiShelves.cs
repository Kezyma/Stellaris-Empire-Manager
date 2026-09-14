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

        WikiKind.SpeciesTraits => new WikiShelf(
            "Species Traits", "species traits", "species trait",
            SpeciesTraits(), WikiFacet.SpeciesTraits),

        WikiKind.Planets => new WikiShelf(
            "Planets", "planets", "planet", Planets(), WikiFacet.Planets) { Picture = "Sky" },

        WikiKind.Personalities => new WikiShelf(
            "AI Personalities", "personalities", "personality",
            Personalities(), WikiFacet.Personalities),

        WikiKind.Governments => new WikiShelf(
            "Governments", "governments", "government", Governments(), WikiFacet.Governments),

        WikiKind.AscensionPerks => new WikiShelf(
            "Ascension Perks", "ascension perks", "ascension perk",
            AscensionPerks(), WikiFacet.AscensionPerks),

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
        WikiFact.Of("Forces", Traits(authority.ForcedTraits)),
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
    /// Refusal is not the only door though, and <see cref="Closed"/> holds the other two.
    /// </para>
    /// <para>
    /// The pack is not read here. <see cref="Row"/> is handed the same tree and works the packs out
    /// of it, which is how Toxoid comes to carry a Toxoids chip without this method mentioning one.
    /// </para>
    /// </remarks>
    private WikiRow SpeciesClass(SpeciesClassDefinition species)
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
    private IReadOnlyList<WikiFact> SpeciesFacts(SpeciesClassDefinition species, IReadOnlyList<string> faces) =>
    [
        WikiFact.Of("Archetype", Archetypes(species.Archetype)),
        WikiFact.Of("Always has", Traits(species.ForcedTrait)),
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
    private IReadOnlyList<WikiRow> Planets() =>
    [
        .. Database.PlanetClasses
            .Select(Planet)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>
    /// One world, with the trait it grants standing in for its own prose and numbers.
    /// </summary>
    /// <remarks>
    /// Playable here means the game offers it to start on. Most of these are places an empire finds
    /// rather than places it wakes up on, and saying so is the whole difference between the two
    /// dozen a reader can pick from and the rest of the galaxy.
    /// </remarks>
    /// <param name="world">The planet class.</param>
    /// <returns>Its row.</returns>
    private WikiRow Planet(PlanetClassDefinition world)
    {
        var preference = session.Rules.HabitabilityTraitFor(world.Key);
        var opened = OpenedBy(world.Key);
        var offered = world.IsStartingWorld || opened.Count > 0;

        return Row(
            world.Key,
            Database.Trait(preference)?.Effects ?? EffectSet.None,
            world.Potential,
            offered,
            offered ? null : "Not a homeworld",
            offered
                ? null
                : "Nothing offers this world to start on. An empire finds it during a game rather "
                    + "than waking up on it.",
            [new WikiCondition("Requirements", _reader.Read(world.Potential), "Any empire")],
            PlanetFacts(world, preference, opened),
            Wants(world.Potential)) with
        {
            Icon = world.Icon,
            Picture = world.Sky,
        };
    }

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
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> PlanetFacts(
        PlanetClassDefinition world,
        string? preference,
        IReadOnlyList<EmpireChoice> opened) =>
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
        WikiFact.Said("Cities", world.ShowsCity ? "Built on it" : "Already one"),
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

        var reader = pack is null
            ? session.Localizer
            : Reading(pack.Text);

        return
        [
            .. Database.GraphicalCultures
                .Select(c => Shipset(c, fleets.GetValueOrDefault(c.Key), reader))
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
    /// <param name="reader">The text, with the pack's merged in.</param>
    /// <returns>Its row.</returns>
    private WikiRow Shipset(
        GraphicalCultureDefinition culture,
        ShipsetFleet? fleet,
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
                : "The game keeps this set for its own empires and does not offer it in the designer.",
            [new WikiCondition("Requirements", _reader.Read(culture.Selectable), "Any empire")],
            ShipsetFacts(culture),
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
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> ShipsetFacts(GraphicalCultureDefinition culture) =>
    [
        // "Fleet" rather than "Ships", which would sit beside the Ship column and mean something
        // else - that column is a picture of one, this is what the set flies. And the game's two
        // words read as what they are: default_ship is a fleet that is built, bio_ship one grown.
        WikiFact.Said("Fleet", culture.ShipCategory switch
        {
            "bio_ship" => "Grown",
            { Length: > 0 } => "Built",
            _ => "None of its own",
        }),
        WikiFact.Said("Cities", culture.HasCityArt ? "Yes" : "No"),
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
    private IReadOnlyList<WikiRow> Personalities() =>
    [
        .. Database.Personalities
            .Select(Personality)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>One personality, named under the prefix the game keeps them under.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>Its row.</returns>
    private WikiRow Personality(PersonalityDefinition personality) =>
        Row(
            personality.Key,
            EffectSet.None,
            playable: null,
            reachable: personality.Allow is not AlwaysRequirement { Value: false },
            personality.Allow is AlwaysRequirement { Value: false } ? "Never drawn" : null,
            personality.Allow is AlwaysRequirement { Value: false }
                ? "The game refuses this one outright, so no empire is ever played as it."
                : null,
            [new WikiCondition("Played by", _reader.Read(personality.Allow), "Any empire")],
            PersonalityFacts(personality),
            Wants(personality.Allow),
            nameKey: personality.NameKey,
            proseKey: personality.DescriptionKey);

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
    /// <returns>Its facts.</returns>
    private static IReadOnlyList<WikiFact> PersonalityFacts(PersonalityDefinition personality) =>
        [WikiFact.Said("Weight", Number(personality.Weight))];

    /// <summary>
    /// The governments, which are what an empire ends up called.
    /// </summary>
    /// <remarks>
    /// A hundred and seventy of them and nobody picks one either: the game takes the
    /// highest-weighted whose conditions the design meets, which is how an empire becomes a Divine
    /// Empire rather than a Despotic Hegemony. The weight is therefore the second half of every
    /// answer and is a column rather than a footnote.
    /// </remarks>
    private IReadOnlyList<WikiRow> Governments() =>
    [
        .. Database.GovernmentTypes
            .Select(Government)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

    /// <summary>One government, with the titles it hands out.</summary>
    /// <param name="government">The government type.</param>
    /// <returns>Its row.</returns>
    private WikiRow Government(GovernmentTypeDefinition government)
    {
        var refused = government.Possible is AlwaysRequirement { Value: false };

        return Row(
            government.Key,
            EffectSet.None,
            playable: null,
            reachable: !refused,
            refused ? "Never used" : null,
            refused
                ? "The game refuses this one outright, so no design is ever called it."
                : null,
            [new WikiCondition("Requirements", _reader.Read(government.Possible), "Any empire")],
            GovernmentFacts(government),
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
    /// <returns>Its facts.</returns>
    private IReadOnlyList<WikiFact> GovernmentFacts(GovernmentTypeDefinition government) =>
    [
        WikiFact.Said("Ruler", Title(government.RulerTitleKey)),
        WikiFact.Said("Ruler (female)", Title(government.RulerTitleFemaleKey)),
        WikiFact.Said("Heir", Title(government.HeirTitleKey)),
        WikiFact.Said("Heir (female)", Title(government.HeirTitleFemaleKey)),
        WikiFact.Said("Weight", Number(government.Weight)),
    ];

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
            [WikiFact.Said("Path", Title(perk.Category))],
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
    private IReadOnlyList<WikiRow> SpeciesTraits() =>
    [
        .. Database.Traits
            .Where(t => t.Kind == TraitKind.Species)
            .Select(SpeciesTrait)
            .OrderBy(r => r.Name, StringComparer.CurrentCulture),
    ];

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
    private WikiRow SpeciesTrait(TraitDefinition trait) =>
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
            [],
            SpeciesTraitFacts(trait),
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

        return new WikiShelf(
            "Leader Traits", "leader traits", "leader trait",
            [
                .. Chains(traits, known.Keys.ToHashSet(StringComparer.Ordinal))
                    .Select(chain => Grouped([.. chain.Select(t => LeaderTrait(t, known, reader))]))
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
        Localizer reader)
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
            [],
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
        WikiFact.Said("Sort", trait.Sort is { Length: > 0 } sort ? Localizer.Prettify(sort) : null),
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
    ];

    /// <summary>What is worth saying about a species trait beyond what it does.</summary>
    private IReadOnlyList<WikiFact> SpeciesTraitFacts(TraitDefinition trait) =>
    [
        // Said as a number rather than as chips, and sortable, because the whole of picking traits
        // is spending a budget: two points for Intelligent, and a drawback to pay for it.
        WikiFact.Said("Cost", trait.Cost.ToString(System.Globalization.CultureInfo.CurrentCulture)),
        WikiFact.Of("Archetype", Archetypes(trait.AllowedArchetypes)),
        WikiFact.Of("Only for", Classes(trait.AllowedSpeciesClasses)),
        WikiFact.Of("Rules out", Traits(trait.Opposites)),
        WikiFact.Of("Homeworld", Worlds(trait.AllowedPlanetClasses)),
        WikiFact.Of("Origin", Civics(trait.AllowedOrigins)),
        WikiFact.Of("Not with", Ethics(trait.ForbiddenEthics)),
        WikiFact.Of("Needs civic", Civics(trait.AllowedCivics)),
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
