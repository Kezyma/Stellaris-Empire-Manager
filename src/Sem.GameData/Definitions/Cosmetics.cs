namespace Sem.GameData;

/// <summary>An advisor voice.</summary>
public sealed record AdvisorVoiceDefinition(string Key, string NameKey)
{
    /// <summary>What must hold for the voice to be offered.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// A room background. These have no localised names in the game, so the picker shows the image.
/// </summary>
public sealed record RoomDefinition(string Key)
{
    /// <summary>Path to the image within the extracted assets.</summary>
    public string? Image { get; init; }

    /// <summary>
    /// Whether the game's own empire designer lists this room.
    /// </summary>
    /// <remarks>
    /// Two different things are being kept apart here. A room the designer lists is one the game
    /// expects a custom empire to hold. A room it does not list is not thereby forbidden — most are
    /// handed out during play by conditions no designer could evaluate, and a design that names one
    /// is drawn in it. What would be a fault is offering something the game will not accept, since a
    /// custom empire it refuses does not appear as an error but simply stops being offered.
    /// </remarks>
    public bool IsOffered { get; init; } = true;
}

/// <summary>
/// A kind of leader.
/// </summary>
/// <remarks>
/// Read rather than assumed. The designer only ever needs the three that may rule, which is what the
/// ruler editor used to name outright — but the game declares four in
/// <c>common/leader_classes</c> and marks the envoy as unable to rule, so reading the file gets the
/// same three, in the game's own words, and follows a patch that adds a fifth.
/// </remarks>
/// <param name="Key">The class as a design stores it, such as <c>official</c>.</param>
/// <param name="NameKey">Localisation key for its name.</param>
public sealed record LeaderClassDefinition(string Key, string NameKey)
{
    /// <summary>Whether a leader of this class may be an empire's ruler.</summary>
    public bool CanRule { get; init; } = true;

    /// <summary>Path to its badge within the extracted assets.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// A group the game sorts its shipset browser into.
/// </summary>
/// <remarks>
/// There are two, from <c>common/ship_sets</c>: Biological and Mechanical. A set belongs to the one
/// whose condition its ships answer, so a player comparing appearances is not left to work out which
/// of the twenty are grown rather than built.
/// </remarks>
/// <param name="Key">The group's own name in the script.</param>
/// <param name="NameKey">Localisation key for the heading.</param>
public sealed record ShipSetDefinition(string Key, string NameKey)
{
    /// <summary>The kind of ship this group gathers, or the one it excludes.</summary>
    public string? Category { get; init; }

    /// <summary>Whether the condition is a negation — mechanical is "anything but biological".</summary>
    public bool Inverted { get; init; }

    /// <summary>Whether a set that builds this kind of ship belongs to this group.</summary>
    public bool Includes(string? shipCategory) =>
        string.Equals(shipCategory, Category, StringComparison.Ordinal) != Inverted;
}

/// <summary>A ship and city appearance set.</summary>
public sealed record GraphicalCultureDefinition(string Key)
{
    /// <summary>Whether the player may choose it.</summary>
    public Requirement Selectable { get; init; } = new AlwaysRequirement(true);

    /// <summary>The set to fall back to when this one lacks an asset.</summary>
    public string? Fallback { get; init; }

    /// <summary>Whether this set has city artwork, and so can be used as a city appearance.</summary>
    public bool HasCityArt { get; init; }

    /// <summary>
    /// The city's own layers, nearest the viewer last, for building the scene behind a portrait.
    /// </summary>
    /// <remarks>
    /// A city is drawn as several bands of buildings at different distances, interleaved with the
    /// planet's own scenery, so it cannot be one flat picture: the hills belong between two rows of
    /// towers. Each band carries the range of world it belongs to, and drawing them all at once is
    /// what buried the planet behind an ecumenopolis.
    /// </remarks>
    public IReadOnlyList<CityLayer> CityLayers { get; init; } = [];

    /// <summary>
    /// Which of a set's ships the game builds — <c>bio_ship</c> for a grown fleet, otherwise
    /// <c>default_ship</c>. Null when the set models no ships of its own.
    /// </summary>
    /// <remarks>
    /// This is what <c>common/ship_sets</c> sorts the picker by, and what tells a shipset from a set
    /// that only dresses cities: Solarpunk and Wilderness declare no <c>ship_kinds</c> and keep no
    /// models, so the game flies them in whatever their fallback builds.
    /// </remarks>
    public string? ShipCategory { get; init; }

    /// <summary>
    /// A drawn ship from this set within the extracted assets, when the set has ships.
    /// </summary>
    /// <remarks>
    /// The game keeps no picture of a shipset — its own picker spins the models — so this is
    /// rendered during extraction rather than copied out of the installation.
    /// </remarks>
    public string? ShipPreview { get; init; }

    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The key shouted, which is how the game names a shipset: <c>BIOGENESIS_01</c> is "Spinovore"
    /// and <c>BIOGENESIS_02</c> is "Shellcraft". Only those two are named — every other set has no
    /// entry under any spelling, and falls back to its key made readable. That is a fact about the
    /// game's text rather than a gap here.
    /// </remarks>
    public string NameKey => Key.ToUpperInvariant();

    /// <summary>
    /// Localisation key for what the game says the set looks like.
    /// </summary>
    /// <remarks>
    /// Every set has one of these — "Sturdy and resolute, these vessels are built to endure the
    /// trials of deep space" — and unlike the name they are all present.
    /// </remarks>
    public string DescriptionKey => $"{Key}_shipset_desc";
}

/// <summary>
/// A ship a nomadic empire begins as, instead of on a planet.
/// </summary>
/// <remarks>
/// A nomad has no homeworld, so the game swaps the planet-class picker for this one: the arkship
/// panel sits inside the same <c>planet_class_editor</c> window, beside the planets rather than
/// anywhere else. Three are offered — a civilian, a science and a military ark — and the game marks
/// exactly those three <c>is_starting_arkship</c>, which is how they are found rather than by name.
/// The higher tiers exist but are built during a game.
/// </remarks>
/// <param name="Key">Such as <c>civilian_arkship_tier_1</c>, as a design stores it.</param>
public sealed record ArkshipDefinition(string Key)
{
    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The game names these by their family rather than their tier: <c>civilian_arkship_tier_1</c>
    /// reads out of <c>civilian_arkship_name</c>, which is itself built from a class word and the
    /// word "Arkship".
    /// </remarks>
    public string NameKey =>
        $"{Key[..(Key.IndexOf("_tier_", StringComparison.Ordinal) is var t and >= 0 ? t : Key.Length)]}_name";

    /// <summary>
    /// Localisation key for the description, which carries the bonuses with it.
    /// </summary>
    /// <remarks>
    /// The game writes the bonus list by hand rather than generating it from the ship size's
    /// modifiers, so there is nothing here to compute: this one key expands to the shared effects,
    /// the arkship's own modifiers and the prose, and the text is the game's own.
    /// </remarks>
    public string DescriptionKey => $"{Key}_selector_desc";

    /// <summary>
    /// The picture the game's own panel shows beside the name.
    /// </summary>
    /// <remarks>
    /// One frame of the ship-size sheet rather than a file of its own, which is why it was missed:
    /// the definition names a sprite, the sprite names a sheet, and the arkships sat as three
    /// unillustrated cards in a panel where every other choice has a picture. It is also the same
    /// frame for all nine arkships - frame 29 of 29, a generic glyph - so it illustrates the panel
    /// without distinguishing anything in it. <see cref="Preview"/> is what tells them apart.
    /// </remarks>
    public string? Icon { get; init; }

    /// <summary>
    /// The entity the game draws this arkship as, which is how its model is found.
    /// </summary>
    /// <remarks>
    /// Kept because the meshes are not named after the ship size and are not in a folder named
    /// after anything either - all nine sit in <c>gfx/models/ships/other</c> beside the enclaves
    /// and the crystal stations. The entity name is the game's own link between the two.
    /// </remarks>
    public string? Entity { get; init; }

    /// <summary>A drawing of the ship, rendered from its model the way a shipset's is.</summary>
    public string? Preview { get; init; }

    /// <summary>
    /// The ship as it appears through the window, behind the ruler.
    /// </summary>
    /// <remarks>
    /// A nomad's scene is composed the way a settled empire's is - something in the distance, then
    /// something nearer, then the room over both - and this is the nearer thing, standing where a
    /// city stands for an empire that has one. Not the same picture as <see cref="Preview"/>: that
    /// is the ship on a card in the picker, drawn small and whole, and this is the ship filling a
    /// window. The game keeps three, one per family, named by each ship size's
    /// <c>arkship_picture</c>.
    /// </remarks>
    public string? Picture { get; init; }

    /// <summary>
    /// The stars behind it, which are the same for all three.
    /// </summary>
    /// <remarks>
    /// A world's sky is filed under the world's name in <c>gfx/portraits/environments</c>, and the
    /// ark class has nothing there - it is not a world and has no sky of its own. This one is filed
    /// with the ships instead.
    /// </remarks>
    public string? Sky { get; init; }
}
