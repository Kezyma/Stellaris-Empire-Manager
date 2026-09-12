namespace Sem.GameData;

/// <summary>
/// Finding one definition by its key, without walking the list to do it.
/// </summary>
/// <remarks>
/// <para>
/// Every collection here is a list, and almost every consumer wanted one item out of it. Written as
/// <c>FirstOrDefault(x =&gt; x.Key == key)</c> that is a linear scan - against 546 portraits, 398
/// traits, 358 civics - run per row, per tile, per render. Several components had noticed and built
/// their own <c>ToDictionary</c>, but in a property, so they rebuilt it on every read.
/// </para>
/// <para>
/// Last definition wins, and a repeated key does not throw. That is the game's own load order and
/// what the extractor already applies: a database with one duplicate in it should be read the way
/// the game reads it rather than bring the app down at start.
/// </para>
/// </remarks>
public sealed partial record GameDatabase
{
    /// <summary>
    /// The built indexes, one slot per lookup, each remembering the list it was built from.
    /// </summary>
    /// <remarks>
    /// A record's copy constructor copies this field, so a database made by <c>with</c> shares the
    /// table - and the extractor ends with <c>database with { Portraits = … }</c>, which would
    /// otherwise answer from the list it just replaced. Hence the source list beside each index: a
    /// lookup uses what it finds only if it was built from the very list being asked about, and
    /// rebuilds when it was not.
    /// </remarks>
    private readonly (object Source, object Index)?[] _byKey = new (object, object)?[25];

    /// <summary>One archetype by key, or null.</summary>
    public ArchetypeDefinition? Archetype(string? key) => Find(Archetypes, a => a.Key, key, 0);

    /// <summary>One species class by key, or null.</summary>
    public SpeciesClassDefinition? SpeciesClass(string? key) => Find(SpeciesClasses, c => c.Key, key, 1);

    /// <summary>One trait by key, or null.</summary>
    public TraitDefinition? Trait(string? key) => Find(Traits, t => t.Key, key, 2);

    /// <summary>One ethic by key, or null.</summary>
    public EthicDefinition? Ethic(string? key) => Find(Ethics, e => e.Key, key, 3);

    /// <summary>One authority by key, or null.</summary>
    public AuthorityDefinition? Authority(string? key) => Find(Authorities, a => a.Key, key, 4);

    /// <summary>One civic or origin by key, or null.</summary>
    public CivicDefinition? Civic(string? key) => Find(Civics, c => c.Key, key, 5);

    /// <summary>One ascension perk by key, or null.</summary>
    public AscensionPerkDefinition? AscensionPerk(string? key) => Find(AscensionPerks, p => p.Key, key, 6);

    /// <summary>One tradition tree by key, or null.</summary>
    public TraditionTreeDefinition? TraditionTree(string? key) => Find(TraditionTrees, t => t.Key, key, 7);

    /// <summary>One tradition by key, or null.</summary>
    public TraditionDefinition? Tradition(string? key) => Find(Traditions, t => t.Key, key, 8);

    /// <summary>One government type by key, or null.</summary>
    public GovernmentTypeDefinition? GovernmentType(string? key) => Find(GovernmentTypes, g => g.Key, key, 9);

    /// <summary>One AI personality by key, or null.</summary>
    public PersonalityDefinition? Personality(string? key) => Find(Personalities, p => p.Key, key, 10);

    /// <summary>One planet class by key, or null.</summary>
    public PlanetClassDefinition? PlanetClass(string? key) => Find(PlanetClasses, p => p.Key, key, 11);

    /// <summary>One portrait by key, or null.</summary>
    public PortraitDefinition? Portrait(string? key) => Find(Portraits, p => p.Key, key, 12);

    /// <summary>One portrait set by key, or null.</summary>
    public PortraitSetDefinition? PortraitSet(string? key) => Find(PortraitSets, s => s.Key, key, 13);

    /// <summary>One name list by key, or null.</summary>
    public NameListDefinition? NameList(string? key) => Find(NameLists, n => n.Key, key, 14);

    /// <summary>One starting system by key, or null.</summary>
    public InitializerDefinition? Initializer(string? key) => Find(Initializers, i => i.Key, key, 15);

    /// <summary>One advisor voice by key, or null.</summary>
    public AdvisorVoiceDefinition? AdvisorVoice(string? key) => Find(AdvisorVoices, a => a.Key, key, 16);

    /// <summary>One room by key, or null.</summary>
    public RoomDefinition? Room(string? key) => Find(Rooms, r => r.Key, key, 17);

    /// <summary>One graphical culture by key, or null.</summary>
    public GraphicalCultureDefinition? GraphicalCulture(string? key) =>
        Find(GraphicalCultures, c => c.Key, key, 18);

    /// <summary>One shipset group by key, or null.</summary>
    public ShipSetDefinition? ShipSet(string? key) => Find(ShipSets, s => s.Key, key, 19);

    /// <summary>One leader class by key, or null.</summary>
    public LeaderClassDefinition? LeaderClass(string? key) => Find(LeaderClasses, l => l.Key, key, 20);

    /// <summary>One flag category by key, or null.</summary>
    public FlagCategoryDefinition? FlagCategory(string? key) => Find(FlagCategories, c => c.Key, key, 21);

    /// <summary>One flag colour by key, or null.</summary>
    public FlagColorDefinition? FlagColor(string? key) => Find(FlagColors, c => c.Key, key, 22);

    /// <summary>One scripted flag set by key, or null.</summary>
    public EmpireFlagSet? EmpireFlagSet(string? key) => Find(EmpireFlagSets, s => s.Key, key, 23);

    /// <summary>One arkship by key, or null.</summary>
    public ArkshipDefinition? Arkship(string? key) => Find(Arkships, a => a.Key, key, 24);

    /// <summary>
    /// Looks one item up in a list, building that list's index the first time it is asked for.
    /// </summary>
    /// <param name="items">The collection to search.</param>
    /// <param name="key">How to read an item's key.</param>
    /// <param name="wanted">The key to find; a null or empty one finds nothing.</param>
    /// <param name="slot">Which entry of <see cref="_byKey"/> holds this collection's index.</param>
    /// <returns>The matching item, or null.</returns>
    /// <remarks>
    /// The slot is passed rather than derived from the caller so the lookup costs an array index
    /// instead of hashing a string on a path the rules engine runs inside its own loops.
    /// </remarks>
    private TItem? Find<TItem>(IReadOnlyList<TItem> items, Func<TItem, string> key, string? wanted, int slot)
        where TItem : class
    {
        if (wanted is not { Length: > 0 })
        {
            return null;
        }

        var entry = _byKey[slot];

        if (entry is not { } held || !ReferenceEquals(held.Source, items))
        {
            held = (items, items
                .GroupBy(key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal));

            _byKey[slot] = held;
        }

        return ((Dictionary<string, TItem>)held.Index).GetValueOrDefault(wanted);
    }
}
