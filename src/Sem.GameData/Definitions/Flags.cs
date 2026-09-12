namespace Sem.GameData;

/// <summary>
/// A named set of country flags a design may carry.
/// </summary>
/// <remarks>
/// <para>
/// Not the picture on the ships — that is the empire flag, and it is drawn. These are markers the
/// game's own script tests for: <c>custom_start_screen</c> is what gives the United Nations of Earth
/// its own opening screen, and the rest gate the events written for that empire. A design names one
/// set, with <c>flag = "empire_human_1"</c>.
/// </para>
/// <para>
/// The game gives them no display names, so they are known by the empires that carry them.
/// </para>
/// </remarks>
/// <param name="Key">The set's name, as a design refers to it.</param>
/// <param name="Flags">The country flags it grants.</param>
public sealed record EmpireFlagSet(string Key, IReadOnlyList<string> Flags)
{
    /// <summary>The built-in empires that carry this set, by their own keys.</summary>
    public IReadOnlyList<string> Empires { get; init; } = [];
}

/// <summary>A folder of flag emblems, or the folder of flag backgrounds.</summary>
public sealed record FlagCategoryDefinition(string Key, bool IsBackground)
{
    /// <summary>The image file names in this category.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    /// <remarks>
    /// The game builds these from the folder name and ships none for the three categories its own
    /// designer never draws, so those fall back to the folder name prettified — there is no string
    /// to find because nothing was ever meant to read one.
    /// </remarks>
    public string NameKey => $"FLAG_CATEGORY_{Key}";

    /// <summary>
    /// Whether the game's own empire designer lists this category.
    /// </summary>
    /// <remarks>
    /// Set from <c>usage.txt</c>'s <c>show_in_designer</c>. False for the enclave, pre-FTL and
    /// special emblems, which the designer never draws and the game accepts perfectly well: its own
    /// scripts hand them out by writing the same category and file name a design stores. Kept apart
    /// from the rest in the picker rather than mixed in, so nobody has to wonder why the list is
    /// longer than the game's.
    /// </remarks>
    public bool IsOffered { get; init; } = true;
}

/// <summary>
/// How the game frames a flag at one of its sizes.
/// </summary>
/// <remarks>
/// <para>
/// A flag is three pictures, not one: an ornamental frame, the coloured field inset inside it under
/// a mask that rounds its corners, and the emblem inset smaller still. Drawing the field edge to
/// edge with the emblem stretched over all of it, which is what happened before, made every emblem
/// about a third larger than the game's.
/// </para>
/// <para>
/// A set of these per size, because the proportions are not constant: the emblem is seven tenths of
/// the field on the largest flag and four fifths on the smallest, so that a small one stays legible.
/// Every measurement here is in the frame's own pixels, which is why <see cref="FrameSize"/> is the
/// number everything else is a fraction of.
/// </para>
/// </remarks>
/// <param name="Key">The sprite's name, such as <c>GFX_empire_flag_128</c>.</param>
/// <param name="FrameSize">How wide the whole thing is, the frame included.</param>
/// <param name="BackgroundOffset">Where the coloured field starts inside the frame.</param>
/// <param name="BackgroundSize">How wide the coloured field is.</param>
/// <param name="EmblemOffset">Where the emblem starts inside the frame.</param>
/// <param name="EmblemSize">How wide the emblem is.</param>
public sealed record FlagFrameDefinition(
    string Key,
    double FrameSize,
    double BackgroundOffset,
    double BackgroundSize,
    double EmblemOffset,
    double EmblemSize)
{
    /// <summary>The ornamental border, within the extracted assets.</summary>
    public string? FrameImage { get; init; }

    /// <summary>The shape the coloured field is cut to, within the extracted assets.</summary>
    public string? MaskImage { get; init; }
}

/// <summary>
/// A named colour a flag can use.
/// </summary>
/// <remarks>
/// One name, three different colours. The game tints a flag, an empire's territory on the galaxy
/// map and its ship trails from the same choice, and the three are not the same shade — "pink" is a
/// muted purple on a flag and a far stronger one on the map. Showing the flag tint in a swatch that
/// chooses a map colour would be quietly misleading, so all three are kept.
/// </remarks>
public sealed record FlagColorDefinition(string Key, byte Red, byte Green, byte Blue)
{
    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapRed { get; init; }

    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapGreen { get; init; }

    /// <summary>The colour of an empire's territory on the galaxy map.</summary>
    public byte MapBlue { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipRed { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipGreen { get; init; }

    /// <summary>The colour of the empire's ship trails.</summary>
    public byte ShipBlue { get; init; }
}
