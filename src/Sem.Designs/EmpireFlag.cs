using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// An empire's flag: a background, an emblem laid over it, and the colours they are tinted with.
/// </summary>
public sealed class EmpireFlag : CwView
{
    internal static readonly string[] FieldOrder =
        ["icon", "background", "colors", "use_ship_color", "use_map_color"];

    /// <summary>Views a block the design already has.</summary>
    public EmpireFlag(CwBlock block)
        : base(block, FieldOrder)
    {
    }

    /// <summary>Views a block that is made only when something is written to it.</summary>
    public EmpireFlag(CwView parent, string key)
        : base(parent, key, FieldOrder)
    {
    }

    /// <summary>The number of colour slots the game always writes, padding unused ones.</summary>
    /// <remarks>
    /// Six since Stellaris 4.5, which added the ship tint and the two map colours. Four before it,
    /// and a file written by an older game still has four - which is read, and grown to six the
    /// first time something is written, exactly as the game itself grew them.
    /// </remarks>
    public const int ColorSlots = 6;

    /// <summary>The placeholder written into an unused colour slot.</summary>
    public const string EmptyColor = "null";

    /// <summary>
    /// What each colour slot is for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These have been renumbered twice, both times because they were deduced rather than read. So
    /// they are quoted from the game rather than concluded: Stellaris 4.5 documents all six in the
    /// header of <c>flags/colors.txt</c>, lines 94-117.
    /// </para>
    /// <code>
    ///   randomizable_combo = { slot0 slot1 slot2 slot3 slot4 slot5 }
    ///
    ///   slot0  Primary color    Drives the red channel of the flag background texture.
    ///           Also the derived map border color and the fallback ship entity tint.
    ///   slot1  Secondary color  Drives the green channel of the flag background texture.
    ///           Also the derived map territory fill color.
    ///   slot2  Tertiary color   Drives the blue channel. No special gameplay role.
    ///   slot3  Ship color       Inert in the flag shader. When use_ship_color = yes, this
    ///           swatch's ship = rgb value is the entity tint on all ships and structures.
    ///   slot4  Map border color When use_map_color = yes, this swatch's map = rgb overrides
    ///           the map border color.
    ///   slot5  Map fill color   Likewise for the territory fill.
    /// </code>
    /// <para>
    /// Which settles what two rounds of guesswork did not. The tertiary really does draw nothing:
    /// across all sixty-three backgrounds the game ships, the brightest pixel in any blue channel is
    /// 11 of 255. It was reasonable to conclude from that the slot must be for the map, and wrong -
    /// slots 2 and 3 were the tertiary and the ship tint all along, and the map's own pair did not
    /// exist yet.
    /// </para>
    /// </remarks>
    public const int PrimarySlot = 0;

    /// <inheritdoc cref="PrimarySlot"/>
    public const int SecondarySlot = 1;

    /// <inheritdoc cref="PrimarySlot"/>
    public const int TertiarySlot = 2;

    /// <inheritdoc cref="PrimarySlot"/>
    public const int ShipSlot = 3;

    /// <inheritdoc cref="PrimarySlot"/>
    public const int MapBorderSlot = 4;

    /// <inheritdoc cref="PrimarySlot"/>
    public const int MapFillSlot = 5;

    /// <summary>The emblem laid over the background.</summary>
    public FlagImage Icon => new(this, "icon");

    /// <summary>The background shape the emblem sits on.</summary>
    public FlagImage Background => new(this, "background");

    /// <summary>
    /// The colour slots, in order. Unused slots hold <see cref="EmptyColor"/> rather than being
    /// omitted, so a well-formed 4.5 design has six entries and an older one has four.
    /// </summary>
    public IReadOnlyList<string> Colors =>
        GetBlock("colors") is { } colors
            ? [.. colors.Nodes.Where(n => !n.IsAssignment && n.Scalar is not null).Select(n => n.ScalarValue!)]
            : [];

    /// <summary>
    /// Replaces the colour slots, padding to <see cref="ColorSlots"/> with <see cref="EmptyColor"/>
    /// so the result matches what the game writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing is ever removed. This used to delete every entry past the fourth, on the reasoning
    /// that four was how many a flag had - and then 4.5 shipped six, which meant that opening a
    /// current designs file and changing one flag colour silently threw away the ship tint and both
    /// map colours. A block wider than this version understands is a block written by a version that
    /// understands more, and the rest of the file has always been treated that way: see the promise
    /// in <see cref="CwView"/>, that fields this project does not know about simply stay in the
    /// tree.
    /// </para>
    /// <para>
    /// Padding is a floor rather than a width, for the same reason. Short lists grow to six; long
    /// ones keep their length.
    /// </para>
    /// </remarks>
    public void SetColors(IReadOnlyList<string> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var block = GetOrAddBlock("colors");
        var elements = block.Nodes.Where(n => !n.IsAssignment).ToList();

        // Every slot the caller named, then empties up to the width the game writes. A slot past
        // both - one this version has no name for - is left exactly as it was found rather than
        // being emptied, which is the difference between preserving it and quietly clearing it.
        var written = Math.Max(colors.Count, ColorSlots);

        for (var i = 0; i < Math.Min(elements.Count, written); i++)
        {
            var value = i < colors.Count && !string.IsNullOrEmpty(colors[i]) ? colors[i] : EmptyColor;
            elements[i].Value = CwScalar.Quoted(value);
        }

        for (var i = elements.Count; i < written; i++)
        {
            var value = i < colors.Count && !string.IsNullOrEmpty(colors[i]) ? colors[i] : EmptyColor;
            block.Add(new CwNode(CwScalar.Quoted(value)));
        }
    }

    /// <summary>The colour the flag's background draws in its red channel.</summary>
    /// <remarks>
    /// Read and written through the colour slots rather than beside them, so each is subject to
    /// everything they already guarantee: padded the way the game pads, quoted the way the game
    /// quotes, and carried through untouched when another editor rewrites the ones it draws.
    /// </remarks>
    public string? Primary
    {
        get => ColorIn(PrimarySlot);
        set => SetColor(PrimarySlot, value);
    }

    /// <summary>And its green channel.</summary>
    /// <inheritdoc cref="Primary" path="/remarks"/>
    public string? Secondary
    {
        get => ColorIn(SecondarySlot);
        set => SetColor(SecondarySlot, value);
    }

    /// <summary>And its blue channel, which no background the game ships actually draws in.</summary>
    /// <inheritdoc cref="Primary" path="/remarks"/>
    public string? Tertiary
    {
        get => ColorIn(TertiarySlot);
        set => SetColor(TertiarySlot, value);
    }

    /// <summary>
    /// The tint on the empire's ships, used only when <see cref="UseShipColor"/> is set.
    /// </summary>
    /// <inheritdoc cref="Primary" path="/remarks"/>
    public string? ShipColor
    {
        get => ColorIn(ShipSlot);
        set => SetColor(ShipSlot, value);
    }

    /// <summary>
    /// The empire's border on the galaxy map, used only when <see cref="UseMapColor"/> is set.
    /// </summary>
    /// <inheritdoc cref="Primary" path="/remarks"/>
    public string? MapBorder
    {
        get => ColorIn(MapBorderSlot);
        set => SetColor(MapBorderSlot, value);
    }

    /// <summary>What fills the territory inside that border, on the same terms.</summary>
    /// <inheritdoc cref="Primary" path="/remarks"/>
    public string? MapFill
    {
        get => ColorIn(MapFillSlot);
        set => SetColor(MapFillSlot, value);
    }

    /// <summary>
    /// Whether the galaxy map uses this flag's own two map colours rather than deriving them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off is the ordinary answer, and off is written as nothing at all: the game writes
    /// <c>use_map_color=yes</c> and otherwise omits the key entirely, never writing <c>no</c>. So
    /// this removes rather than writes when cleared, because a file that round-trips byte for byte
    /// is what the whole designs layer promises and a stray <c>use_map_color=no</c> would break it
    /// for every empire that has never touched the switch.
    /// </para>
    /// <para>
    /// Read as false when absent, which is what the game's own help says:
    /// <c>use_ship_color = yes/no # default no</c>.
    /// </para>
    /// </remarks>
    public bool UseMapColor
    {
        get => GetBool("use_map_color") ?? false;
        set => SetBool("use_map_color", value ? true : null);
    }

    /// <summary>Whether the empire's ships use its ship colour rather than its primary.</summary>
    /// <inheritdoc cref="UseMapColor" path="/remarks"/>
    public bool UseShipColor
    {
        get => GetBool("use_ship_color") ?? false;
        set => SetBool("use_ship_color", value ? true : null);
    }

    /// <summary>
    /// The colour the galaxy map will actually draw this empire's border in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The switch decides which slot is read, and that is the whole of it: with
    /// <see cref="UseMapColor"/> set the map takes the chosen pair, and without it the map derives
    /// them from the flag - border from the primary, fill from the secondary. The game states this
    /// twice, in the header of <c>flags/colors.txt</c> and in <c>USE_MAP_COLOR_TOOLTIP</c>: "When
    /// disabled, they are derived from the Primary and Secondary Flag colors."
    /// </para>
    /// <para>
    /// A switch that is on over an empty slot falls back to the derived colour rather than to
    /// nothing. The game's own help says the slot "must be a real swatch", and an empire that is
    /// drawn in no colour at all is not a state worth modelling.
    /// </para>
    /// <para>
    /// Not modelled yet: when the secondary is too close to the primary the game substitutes the
    /// tertiary for the fill. The rule is real - it can be watched happening in the 4.5 editor - but
    /// the threshold is in compiled code, named by no define and no string, so it is left out rather
    /// than guessed. What is known bounds it: the closest pair the game's own randomiser will write
    /// into these two slots is <c>teal</c>/<c>dark_teal</c>, 36.7 apart in RGB, so the test must be
    /// tighter than that and near-identity is the likely trigger. Every colour here is therefore
    /// right whenever the two differ, which is the ordinary case. See docs/flag-colours.md.
    /// </para>
    /// </remarks>
    public string? DrawnMapBorder => (UseMapColor ? MapBorder : null) ?? Primary;

    /// <summary>What the map will fill the territory inside it with.</summary>
    /// <inheritdoc cref="DrawnMapBorder" path="/remarks"/>
    public string? DrawnMapFill => (UseMapColor ? MapFill : null) ?? Secondary;

    /// <summary>
    /// The colour the empire's ships will actually be tinted.
    /// </summary>
    /// <remarks>
    /// The same arrangement as the map's, one slot instead of two: <see cref="UseShipColor"/> reads
    /// the ship slot, and without it the fleet takes the primary flag colour. Quoted from the game's
    /// own parser help - "When yes, slot 3 swatch ship= value is used for ships" - and from the
    /// header of <c>flags/colors.txt</c>, which calls the primary "the fallback ship entity tint".
    ///
    /// Which slot wins is all this answers. What shade the hull then takes is a separate question
    /// and not a settled one: the swatch's <c>ship</c> value is the header's answer, but that column
    /// holds only 14 distinct values across the 72 names, so it cannot be one colour per name. See
    /// docs/flag-colours.md.
    /// </remarks>
    public string? DrawnShipColor => (UseShipColor ? ShipColor : null) ?? Primary;

    /// <summary>What one slot holds, with the game's word for "nothing" read as nothing.</summary>
    public string? ColorIn(int slot) =>
        Colors is { } held && slot < held.Count && held[slot] != EmptyColor ? held[slot] : null;

    /// <summary>Puts a colour in one slot, leaving the others as they were found.</summary>
    /// <remarks>
    /// Whole-list, because that is what the file holds: the slots are an ordered block and there is
    /// no writing to the fifth of them without saying what the other five are. Doing it here rather
    /// than at each caller is what keeps the map's pair safe from the flag editor, the flag's three
    /// safe from the map's, and the ship tint safe from both.
    /// </remarks>
    public void SetColor(int slot, string? value)
    {
        var colors = Colors.ToList();

        while (colors.Count <= slot)
        {
            colors.Add(EmptyColor);
        }

        colors[slot] = value is { Length: > 0 } chosen ? chosen : EmptyColor;
        SetColors(colors);
    }

    /// <summary>The emblem and the background it sits on.</summary>
    public override string ToString() => $"{Icon} on {Background}";
}

/// <summary>One image making up a flag, identified by the folder it lives in and its file name.</summary>
public sealed class FlagImage : CwView
{
    private static readonly string[] FieldOrder = ["category", "file"];

    /// <summary>Views a block the flag already has.</summary>
    public FlagImage(CwBlock block)
        : base(block, FieldOrder)
    {
    }

    /// <summary>Views a block that is made only when something is written to it.</summary>
    public FlagImage(CwView parent, string key)
        : base(parent, key, FieldOrder)
    {
    }

    /// <summary>The folder under <c>flags/</c>, such as <c>zoological</c> or <c>backgrounds</c>.</summary>
    public string? Category
    {
        get => GetString("category");
        set => SetString("category", value);
    }

    /// <summary>The file name within the category, including its <c>.dds</c> extension.</summary>
    public string? File
    {
        get => GetString("file");
        set => SetString("file", value);
    }

    /// <summary>The image as the game names it: its category and file.</summary>
    public override string ToString() => $"{Category}/{File}";
}
