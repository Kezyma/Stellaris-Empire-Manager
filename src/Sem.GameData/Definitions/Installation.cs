namespace Sem.GameData;

/// <summary>Values from the game's defines that constrain empire creation.</summary>
public sealed record GameDefines
{
    /// <summary>Total ethics points available. Three in an unmodified game.</summary>
    public required int EthicsPoints { get; init; }

    /// <summary>How many civics an empire may take. Two in an unmodified game.</summary>
    public required int CivicPoints { get; init; }

    /// <summary>
    /// How many ascension perks a game allows. Eight in an unmodified one.
    /// </summary>
    /// <remarks>
    /// Not required, unlike the two above, so that the databases tests build by hand do not all have
    /// to know about a number none of them cares about.
    /// </remarks>
    public int AscensionPerkSlots { get; init; } = 8;

    /// <summary>How many tradition trees a game allows to be opened. Seven in an unmodified one.</summary>
    public int TraditionSlots { get; init; } = 7;


    /// <summary>
    /// How many civics an empire may end a game with, rather than start one with. Three in an
    /// unmodified game.
    /// </summary>
    /// <remarks>
    /// Two from <c>GOVERNMENT_CIVIC_POINTS_BASE</c> plus the one
    /// <c>tech_galactic_administration</c> grants, which is the slot players reform their
    /// government to fill and the reason a plan has civics in it at all. Read from the technology
    /// rather than typed here, so a patch that moves the slot moves this too.
    /// </remarks>
    public int PlannedCivicPoints { get; init; } = 3;

    /// <summary>
    /// How built-up the world in the designer's own preview is, on the game's nought-to-five scale.
    /// </summary>
    /// <remarks>
    /// The game's <c>DEFAULT_CITY_POP_LEVEL</c>, whose line in the defines is commented "Shown in
    /// empire designer" — so this is not a judgement about how a homeworld ought to look but the
    /// number the game itself draws with. It is what keeps the sixth band of city, an ecumenopolis
    /// covering half the frame, off a world that has not earned it.
    /// </remarks>
    public int CityPopLevel { get; init; } = 4;
}

/// <summary>A downloadable content pack.</summary>
/// <param name="Folder">Its folder under <c>dlc/</c>, which also indicates whether it is installed.</param>
/// <param name="Name">The name the game's script matches on, such as <c>Utopia</c>.</param>
/// <param name="NameKey">Localisation key for its display name.</param>
/// <param name="Category">Its kind: expansion, story pack, species pack or content pack.</param>
/// <param name="Installed">Whether the pack is present in this installation.</param>
public sealed record DlcDefinition(
    string Folder,
    string Name,
    string? NameKey,
    string? Category,
    bool Installed)
{
    /// <summary>Path to the pack's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Whether the game gave this pack a badge of its own.
    /// </summary>
    /// <remarks>
    /// False for the three that are a single species portrait and nothing else - the game draws
    /// them by name and has no icon set for them. Those borrow the face of the portrait they add,
    /// so <see cref="Icon"/> is filled in either way and cannot answer this; and it is worth
    /// answering, because a borrowed face is a different shape of thing from a badge and belongs at
    /// the end of the row rather than among them.
    /// </remarks>
    public bool HasOwnIcon { get; init; } = true;

    /// <summary>
    /// Whether owning this pack changes anything the designer offers.
    /// </summary>
    /// <remarks>
    /// Eleven do not. Some are obviously beside the point — a soundtrack, a set of forum avatars —
    /// but three are expansions whose content this designer never reaches: Utopia, Synthetic Dawn
    /// and Distant Stars add nothing an empire is built from. A switch that does nothing is worse
    /// than no switch, so the bar leaves them out.
    /// </remarks>
    public bool Decides { get; init; }
}

/// <summary>Enough of a built-in empire to list it as a starting point in the designer.</summary>
public sealed record PrescriptedEmpireSummary(string Key, string SourceFile)
{
    /// <summary>Localisation key for its name.</summary>
    public string? NameKey { get; init; }

    /// <summary>Its species class, for showing alongside the entry.</summary>
    public string? SpeciesClass { get; init; }

    /// <summary>Its portrait, for showing alongside the entry.</summary>
    public string? Portrait { get; init; }

    /// <summary>Its authority.</summary>
    public string? Authority { get; init; }

    /// <summary>Its origin.</summary>
    public string? Origin { get; init; }

    /// <summary>The scripted trigger gating whether the game offers it.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>The set of country flags it carries, where it carries one.</summary>
    public string? FlagSet { get; init; }

    /// <summary>The room it sits in.</summary>
    public string? Room { get; init; }

    /// <summary>
    /// The key of the paragraph the game writes about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of the game's empires has one, and it exists only as text: no prescripted country
    /// file mentions it, so it belongs to the game's empire rather than to a copy a player takes,
    /// which has nowhere to keep it.
    /// </para>
    /// <para>
    /// Keyed on the empire rather than on its name, which is not the same thing and was read as
    /// though it were. Ten of the fifty-two are named by a key that is not their own - the two
    /// human variants share the original's name, three Machine Age empires drop a suffix, and five
    /// are written in capitals while every description in the game is written in lower case. Those
    /// five went without: the paragraph was asked for under a key nobody had written.
    /// </para>
    /// </remarks>
    public string DescriptionKey => $"EMPIRE_DESIGN_{Key}_desc";

    /// <summary>
    /// The empire itself, written in the player's own designs format.
    /// </summary>
    /// <remarks>
    /// The game writes its own empires in a shape of their own, and a browser has no installation to
    /// convert them with, so the conversion travels with the data. This is what a player takes a
    /// copy of.
    /// </remarks>
    public string? Design { get; init; }
}
