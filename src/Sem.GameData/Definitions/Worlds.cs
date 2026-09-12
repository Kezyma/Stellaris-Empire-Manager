namespace Sem.GameData;

/// <summary>A planet class.</summary>
public sealed record PlanetClassDefinition(string Key)
{
    /// <summary>Its climate group: dry, wet, cold or otherwise.</summary>
    public string? Climate { get; init; }

    /// <summary>Whether an empire may start here without an origin saying so.</summary>
    public bool IsStartingWorld { get; init; }

    /// <summary>What must hold for this to be offered, normally owning a content pack.</summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>The sky over this world, seen from its surface.</summary>
    public string? Sky { get; init; }

    /// <summary>
    /// The world seen from its own surface: bands of landscape in front of its sky.
    /// </summary>
    /// <remarks>
    /// This is what shows through a room's window. The game builds the view from a sky and up to
    /// four bands of scenery, interleaved with the empire's own city so that hills sit between rows
    /// of towers — which is why the backdrop cannot be one picture. Furthest from the viewer first.
    /// </remarks>
    public IReadOnlyList<SceneryBand> Scenery { get; init; } = [];

    /// <summary>
    /// Whether the empire's city is built on this world at all.
    /// </summary>
    /// <remarks>
    /// Twenty worlds say no, and they are the ones that are already a built thing: a machine world,
    /// a hive world, a habitat, an ecumenopolis's cousins. The game draws the world and stops, and
    /// painting an empire's towers over one showed a city on a planet that is a city.
    /// </remarks>
    public bool ShowsCity { get; init; } = true;

    /// <summary>
    /// The level the city is always drawn at, where the world fixes it.
    /// </summary>
    /// <remarks>
    /// Only the ecumenopolis, which is built to the horizon whatever its population.
    /// </remarks>
    public int? FixedCityLevel { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>How a starting system may be used.</summary>
public enum InitializerUsage
{
    /// <summary>Not offered during empire creation.</summary>
    None,

    /// <summary>The player may pick it for any empire.</summary>
    CustomEmpire,

    /// <summary>Only available when an origin names it.</summary>
    Origin,

    /// <summary>
    /// A system a nomadic empire starts in.
    /// </summary>
    /// <remarks>
    /// Added by 4.5 with the Nomads pack, and a separate pool rather than more of
    /// <see cref="CustomEmpire"/>: the six in <c>00_nomad_custom_initializers.txt</c> are headed
    /// "Nomad Starting Systems" and are built round a black hole, a neutron star, a protoplanetary
    /// disc - places with nothing to land on, which is the point of arriving by arkship. The
    /// seventh is vela_system, which is where the game's own nomadic empire starts.
    ///
    /// Appended rather than inserted because the value is serialised as its number.
    /// </remarks>
    Nomad,
}

/// <summary>A starting system.</summary>
public sealed record InitializerDefinition(string Key, InitializerUsage Usage)
{
    /// <summary>
    /// Localisation key for the display name.
    /// </summary>
    /// <remarks>
    /// The key with <c>_NAME</c> after it, not the key itself — which is why these read as tidied-up
    /// file names ("Custom Starting Init 01") where the game says "Random Trinary I".
    /// </remarks>
    public string NameKey => $"{Key}_NAME";

    /// <summary>Localisation key for the description, which says what shape the system is.</summary>
    public string DescriptionKey => $"{Key}_DESC";
}

/// <summary>
/// One band of an empire's city, and how built-up a world has to be for the game to draw it.
/// </summary>
/// <remarks>
/// The bounds are the game's own, from the <c>planet</c> block of
/// <c>gfx/portraits/portraits/00_portraits_main.txt</c>. They are not decoration: the sixth band is a
/// wall-to-wall ecumenopolis needing a world at five, and drawn alongside the rest it covers the sky,
/// the horizon and every other band at once.
/// </remarks>
/// <param name="Band">Which of the game's bands this is, counting from one.</param>
/// <param name="Image">Where the band lives within the extracted assets.</param>
/// <param name="MinPop">How built-up a world must be before this band appears.</param>
/// <param name="MaxPop">The last level it appears at, or null where the game sets no limit.</param>
public sealed record CityLayer(int Band, string Image, int MinPop, int? MaxPop)
{
    /// <summary>Whether the game would draw this band on a world of the given level.</summary>
    public bool AppearsAt(int level) => level >= MinPop && (MaxPop is not { } max || level <= max);
}

/// <summary>
/// One band of landscape between a world's sky and its city.
/// </summary>
/// <remarks>
/// Numbered rather than merely ordered, because two worlds are missing one: an arctic and a desert
/// world have a first, third and fourth band and no second. Held as a plain list, their third band
/// took the second's place in the interleave and every row of hills after the gap was drawn in front
/// of the towers it belongs behind.
/// </remarks>
/// <param name="Band">Which of the game's bands this is, counting from one.</param>
/// <param name="Image">Where the band lives within the extracted assets.</param>
public sealed record SceneryBand(int Band, string Image);
