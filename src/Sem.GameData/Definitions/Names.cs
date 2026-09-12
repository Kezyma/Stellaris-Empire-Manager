namespace Sem.GameData;

/// <summary>A species name list.</summary>
public sealed record NameListDefinition(string Key, string? Category)
{
    /// <summary>Whether the player may choose it. A few lists exist only for the game's own use.</summary>
    public Requirement Selectable { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// A different name list to draw species, homeworld and system names from.
    /// </summary>
    /// <remarks>
    /// The game's three human lists do this, so that randomising a species for the United Nations of
    /// Earth offers ordinary human species names rather than the empire's own naming conventions.
    /// </remarks>
    public string? RandomNameSource { get; init; }

    /// <summary>Ruler names this list offers, already in the player's language.</summary>
    public NameSet CharacterNames { get; init; } = new();

    /// <summary>Planet names this list offers, already in the player's language.</summary>
    public IReadOnlyList<string> PlanetNames { get; init; } = [];

    /// <summary>Ship names this list offers, used to show what the list sounds like.</summary>
    public IReadOnlyList<string> ShipNames { get; init; } = [];

    /// <summary>Fleet names this list offers.</summary>
    public IReadOnlyList<string> FleetNames { get; init; } = [];

    /// <summary>
    /// The pattern this list numbers its fleets by, where it names none.
    /// </summary>
    /// <remarks>
    /// Sixteen lists work this way, the default human one among them: rather than a pool of names
    /// they carry a template such as "Tähtaailaivasto $R$" and count upwards. A list with this and
    /// no <see cref="FleetNames"/> is not an empty list.
    /// </remarks>
    public string? FleetPattern { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => $"name_list_{Key}";
}

/// <summary>
/// One kind of name, in the variants a list may offer it.
/// </summary>
/// <remarks>
/// The game's rule, from its own documentation: the list matching the character's gender is used
/// when it holds anything, and the ungendered list stands in when it does not. Every kind of name
/// follows it — a list may give full names by gender and second names without, or the reverse.
/// </remarks>
public sealed record GenderedNames
{
    /// <summary>Names for a character of no particular gender.</summary>
    public IReadOnlyList<string> Any { get; init; } = [];

    /// <summary>Names for a male character.</summary>
    public IReadOnlyList<string> Male { get; init; } = [];

    /// <summary>Names for a female character.</summary>
    public IReadOnlyList<string> Female { get; init; } = [];

    /// <summary>Whether the list offers none of this kind.</summary>
    public bool IsEmpty => Any.Count == 0 && Male.Count == 0 && Female.Count == 0;

    /// <summary>The names to draw on for a character, following the game's rule.</summary>
    public IReadOnlyList<string> For(bool female)
    {
        var gendered = female ? Female : Male;
        return gendered.Count > 0 ? gendered : Any;
    }

    /// <summary>Every name of this kind, for showing what a list sounds like.</summary>
    public IReadOnlyList<string> All =>
        [.. Any.Concat(Male).Concat(Female).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// The names a list offers for a character.
/// </summary>
/// <remarks>
/// A name is either complete in itself or a first name joined to a second. Where a list offers both,
/// the game picks between them evenly.
/// </remarks>
public sealed record NameSet
{
    /// <summary>Names that stand alone.</summary>
    public GenderedNames FullNames { get; init; } = new();

    /// <summary>First names, always joined to a second.</summary>
    public GenderedNames FirstNames { get; init; } = new();

    /// <summary>Second names, always joined to a first.</summary>
    public GenderedNames SecondNames { get; init; } = new();

    /// <summary>
    /// The names a ruler who reigns under a regnal name is drawn from.
    /// </summary>
    /// <remarks>
    /// Sixty-one of the game's seventy-one lists declare these, and none of them was read: the
    /// extractor asked for full, first and second names and stopped. They are a pool of their own —
    /// a list may offer names it uses only for a monarch — so they are kept apart rather than
    /// folded into the ordinary ones.
    /// </remarks>
    public GenderedNames RegnalFirstNames { get; init; } = new();

    /// <inheritdoc cref="RegnalFirstNames" />
    public GenderedNames RegnalSecondNames { get; init; } = new();

    /// <summary>Whether there is anything here to build a name from.</summary>
    public bool IsEmpty => FullNames.IsEmpty && FirstNames.IsEmpty;

    /// <summary>
    /// Names as they would actually appear, joined where the list holds them in parts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list either names its leaders outright or gives first and family names to be put together.
    /// Where it does the second, showing the two pools side by side reads as a jumble, so they are
    /// joined — which is what the game does in its own descriptions of these lists. The limit is
    /// there because the two pools multiply: a hundred first names and a hundred family names are
    /// ten thousand people, and nobody reads a list that long.
    /// </para>
    /// <para>
    /// Joined through <see cref="LeaderName"/> rather than with a space, because a family name is
    /// often a frame written round a given one and the two are not simply set side by side. Written
    /// with a space, a third of the game's lists offered the player names with <c>$1$</c> and
    /// <c>|||masc:</c> still in them.
    /// </para>
    /// </remarks>
    /// <param name="limit">How many to build, at most.</param>
    /// <param name="gender">Whose names these are, where the caller knows.</param>
    public IReadOnlyList<string> Assembled(int limit, string? gender = null)
    {
        var whole = FullNames.All;
        var firsts = FirstNames.All;
        var seconds = SecondNames.All;

        // Capped like everything else. The limit used to guard only the composed names, so a list
        // whose leaders are all written out in full — which the human lists are — returned its whole
        // pool however few the caller asked for.
        var joined = new List<string>(
            whole.Take(limit).Select(name => LeaderName.Variant(name, gender)));

        if (firsts.Count == 0)
        {
            return joined;
        }

        for (var i = 0; i < firsts.Count && joined.Count < limit; i++)
        {
            joined.Add(seconds.Count == 0
                ? LeaderName.Variant(firsts[i], gender)
                : LeaderName.Compose(firsts[i], seconds[i % seconds.Count], gender));
        }

        // The regnal pool after the ordinary one, since a ruler may be styled either way. Appended
        // rather than mixed in, so what a list offers first is what it offered before.
        var regnalFirsts = RegnalFirstNames.All;
        var regnalSeconds = RegnalSecondNames.All;

        for (var i = 0; i < regnalFirsts.Count && joined.Count < limit; i++)
        {
            joined.Add(regnalSeconds.Count == 0
                ? LeaderName.Variant(regnalFirsts[i], gender)
                : LeaderName.Compose(regnalFirsts[i], regnalSeconds[i % regnalSeconds.Count], gender));
        }

        return joined.Distinct(StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// A ready-made species, as the game's own randomise button offers.
/// </summary>
/// <remarks>
/// The game does not invent a species name a piece at a time. It picks one of these, which carries a
/// name, its plural, a homeworld, a home system and the name list that suits it — so pressing
/// randomise fills in five fields at once and they agree with each other.
/// </remarks>
/// <param name="SpeciesClass">The class this suits, such as <c>MAM</c>.</param>
/// <param name="Name">The species name.</param>
public sealed record SpeciesNameSuggestion(string SpeciesClass, string Name)
{
    /// <summary>The plural form.</summary>
    public string? Plural { get; init; }

    /// <summary>A name for the homeworld.</summary>
    public string? HomePlanet { get; init; }

    /// <summary>A name for the home system.</summary>
    public string? HomeSystem { get; init; }

    /// <summary>The name list that goes with it.</summary>
    public string? NameList { get; init; }

    /// <summary>
    /// The localisation keys behind each of those names.
    /// </summary>
    /// <remarks>
    /// The game's own files hold keys — <c>SPEC_Rexor</c>, not "Rexor" — and a design that picks one
    /// of these stores the key rather than the word, so that a player reading another language sees
    /// the species named in theirs. Both are needed: the key to write and the text to show.
    /// </remarks>
    public string? NameKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? PluralKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? HomePlanetKey { get; init; }

    /// <inheritdoc cref="NameKey"/>
    public string? HomeSystemKey { get; init; }
}
