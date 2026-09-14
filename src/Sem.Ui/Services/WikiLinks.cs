using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// Where in the wiki a thing lives, and what its address is.
/// </summary>
/// <remarks>
/// <para>
/// A chip knows its key and nothing else. Pressing one should go and read about that thing, and what
/// stands between the two is which shelf it is on - so this asks the game rather than threading a
/// second field through every chip, every fact and every pack in the app.
/// </para>
/// <para>
/// One method and one lookup per kind, in the order a key is likeliest to be found. A key that
/// belongs to nothing comes back as nothing, and the chip is drawn as it always was: not every
/// chip on a wiki page is something the wiki has a page for - a tradition and a name list are both
/// things a design names and neither has a shelf yet.
/// </para>
/// </remarks>
/// <param name="database">The extracted game.</param>
/// <param name="shelf">
/// Which shelf the wiki's own file is for, where one is open, and the keys it carries. A leader
/// trait is in no collection the database has, so it can only be recognised by being told.
/// </param>
public sealed class WikiLinks(GameDatabase database, (WikiKind Kind, IReadOnlySet<string> Keys)? shelf = null)
{
    private readonly GameDatabase _database =
        database ?? throw new ArgumentNullException(nameof(database));

    /// <summary>Where the wiki lives, so one place decides it.</summary>
    public const string Root = "wiki";

    /// <summary>The address of the shelf one kind is on.</summary>
    /// <param name="kind">The shelf.</param>
    /// <returns>The route, without a leading slash.</returns>
    public static string Section(WikiKind kind) => kind switch
    {
        WikiKind.Origins => $"{Root}/origins",
        WikiKind.Ethics => $"{Root}/ethics",
        WikiKind.Authorities => $"{Root}/authorities",
        WikiKind.Species => $"{Root}/species",
        WikiKind.SpeciesTraits => $"{Root}/species-traits",
        WikiKind.LeaderTraits => $"{Root}/leader-traits",
        WikiKind.Planets => $"{Root}/planets",
        WikiKind.Shipsets => $"{Root}/shipsets",
        WikiKind.Personalities => $"{Root}/personalities",
        WikiKind.Governments => $"{Root}/governments",
        WikiKind.AscensionPerks => $"{Root}/ascension-perks",
        _ => $"{Root}/civics",
    };

    /// <summary>The address of one entry, which is its shelf and its key.</summary>
    /// <param name="kind">The shelf.</param>
    /// <param name="key">The entry.</param>
    /// <returns>The route, without a leading slash.</returns>
    public static string Entry(WikiKind kind, string key) => $"{Section(kind)}/{key}";

    /// <summary>
    /// The shelf a key belongs to, or nothing where the wiki has no page for it.
    /// </summary>
    /// <remarks>
    /// Civics before origins because they are the same collection and told apart by a flag, and
    /// species classes before the traits because their keys are the only ones that look nothing
    /// like the others - <c>HUM</c> and <c>TOX</c> rather than <c>civic_</c> and <c>ethic_</c>.
    /// </remarks>
    /// <param name="key">What a chip carries.</param>
    /// <returns>The shelf, or null.</returns>
    public WikiKind? Shelf(string? key)
    {
        if (key is not { Length: > 0 })
        {
            return null;
        }

        // What the page in front of the reader carries, first: its keys are the ones the database
        // cannot answer for, and asking it first costs one set lookup.
        if (shelf is { } open && open.Keys.Contains(key))
        {
            return open.Kind;
        }

        if (_database.Civic(key) is { } civic)
        {
            return civic.IsOrigin ? WikiKind.Origins : WikiKind.Civics;
        }

        if (_database.Ethic(key) is not null)
        {
            return WikiKind.Ethics;
        }

        if (_database.Authority(key) is not null)
        {
            return WikiKind.Authorities;
        }

        if (_database.SpeciesClass(key) is not null)
        {
            return WikiKind.Species;
        }

        // The five shelves whose keys nothing else looks like. A planet class and an ascension perk
        // are both things a civic asks for, and both were chips that went nowhere before these
        // pages existed - which is what the comment at the top of this file used to say.
        if (_database.PlanetClass(key) is not null)
        {
            return WikiKind.Planets;
        }

        if (_database.AscensionPerk(key) is not null)
        {
            return WikiKind.AscensionPerks;
        }

        if (_database.GovernmentType(key) is not null)
        {
            return WikiKind.Governments;
        }

        if (_database.Personality(key) is not null)
        {
            return WikiKind.Personalities;
        }

        if (_database.GraphicalCulture(key) is not null)
        {
            return WikiKind.Shipsets;
        }

        // The species traits only. A leader trait is not in the database at all - it lives in the
        // wiki's own file - so a chip naming one is not a link, which is the right answer: nothing
        // outside that page has any reason to name one.
        return _database.Trait(key) is { Kind: TraitKind.Species } ? WikiKind.SpeciesTraits : null;
    }

    /// <summary>The address of whatever a key names, or nothing where the wiki has no page for it.</summary>
    /// <param name="key">What a chip carries.</param>
    /// <returns>The route, or null.</returns>
    public string? For(string? key) =>
        Shelf(key) is { } kind && key is { Length: > 0 } ? Entry(kind, key) : null;
}
