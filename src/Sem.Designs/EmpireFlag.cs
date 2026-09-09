using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// An empire's flag: a background, an emblem laid over it, and the colours they are tinted with.
/// </summary>
public sealed class EmpireFlag : CwView
{
    internal static readonly string[] FieldOrder = ["icon", "background", "colors"];

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
    public const int ColorSlots = 4;

    /// <summary>The placeholder written into an unused colour slot.</summary>
    public const string EmptyColor = "null";

    /// <summary>
    /// The slot this app keeps a tag in, which the flag itself never draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game writes four colours and draws three. Its own shader declares the array four wide and
    /// leaves the fourth line commented out - <c>gfx/FX/flag_sprite.shader</c>, where
    /// <c>//vColor += BackgroundColor[3] * vBG.a;</c> sits after the clamp, and which is the only
    /// thing in the whole of <c>gfx/</c> that reads a flag's colours. Its designer offers buttons for
    /// the first two and nothing else, and where the third at least has a word - the game ships
    /// <c>TERTIARY_COLOR</c> - there is no <c>QUATERNARY_COLOR</c> anywhere in it. Across the three
    /// hundred and three colour blocks Paradox ships, the slot is something other than <c>null</c>
    /// twice, and means nothing either time.
    /// </para>
    /// <para>
    /// So it is somewhere to keep a colour of the player's own that travels with the empire into the
    /// file and back out again, and costs the flag nothing.
    /// </para>
    /// </remarks>
    public const int TagSlot = 3;

    /// <summary>The slot holding the empire's colour on the galaxy map.</summary>
    /// <remarks>
    /// Kept inside the flag, which is where the game keeps it, and not a flag colour. A background
    /// is three shapes packed into one file's red, green and blue channels, each tinted with one of
    /// the first three colours - and across all sixty-three backgrounds the game ships, the
    /// brightest pixel in any blue channel is 11 of 255. Nothing is drawn there. Yet the game has a
    /// word for this slot, <c>TERTIARY_COLOR</c>, and four of its own empires set it to something
    /// distinctive on backgrounds with no blue at all.
    /// </remarks>
    public const int MapColorSlot = 2;

    /// <summary>The emblem laid over the background.</summary>
    public FlagImage Icon => new(this, "icon");

    /// <summary>The background shape the emblem sits on.</summary>
    public FlagImage Background => new(this, "background");

    /// <summary>
    /// The four colour slots, in order. Unused slots hold <see cref="EmptyColor"/> rather than
    /// being omitted, so this always has four entries in a well-formed design.
    /// </summary>
    public IReadOnlyList<string> Colors =>
        GetBlock("colors") is { } colors
            ? [.. colors.Nodes.Where(n => !n.IsAssignment && n.Scalar is not null).Select(n => n.ScalarValue!)]
            : [];

    /// <summary>
    /// Replaces the colour slots, padding to four with <see cref="EmptyColor"/> so the result
    /// matches what the game writes.
    /// </summary>
    public void SetColors(IReadOnlyList<string> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        if (colors.Count > ColorSlots)
        {
            throw new ArgumentException(
                $"A flag has {ColorSlots} colour slots but {colors.Count} were supplied.", nameof(colors));
        }

        var padded = new string[ColorSlots];
        for (var i = 0; i < ColorSlots; i++)
        {
            padded[i] = i < colors.Count && !string.IsNullOrEmpty(colors[i]) ? colors[i] : EmptyColor;
        }

        var block = GetOrAddBlock("colors");
        var elements = block.Nodes.Where(n => !n.IsAssignment).ToList();

        for (var i = 0; i < Math.Min(elements.Count, ColorSlots); i++)
        {
            elements[i].Value = CwScalar.Quoted(padded[i]);
        }

        for (var i = ColorSlots; i < elements.Count; i++)
        {
            block.Remove(elements[i]);
        }

        for (var i = elements.Count; i < ColorSlots; i++)
        {
            block.Add(new CwNode(CwScalar.Quoted(padded[i])));
        }
    }

    /// <summary>
    /// The colour this empire is tagged with, or null where it is not tagged.
    /// </summary>
    /// <remarks>
    /// Read and written through the colour slots rather than beside them, so a tag is subject to
    /// everything they already guarantee: it is padded the way the game pads, it is quoted the way
    /// the game quotes, and the flag editor rewriting the three colours it does draw carries this
    /// one through untouched.
    /// </remarks>
    public string? Tag
    {
        get => ColorIn(TagSlot);
        set => SetColor(TagSlot, value);
    }

    /// <summary>
    /// The colour this empire flies on the galaxy map, or null where it takes one from its flag.
    /// </summary>
    /// <remarks>
    /// Almost every empire leaves this empty: thirty-two of the game's fifty-three ready-made
    /// empires write <c>null</c> here and seventeen more write <c>black</c>, and only four set it to
    /// a colour of their own.
    /// </remarks>
    public string? MapColor
    {
        get => ColorIn(MapColorSlot);
        set => SetColor(MapColorSlot, value);
    }

    /// <summary>What one slot holds, with the game's word for "nothing" read as nothing.</summary>
    private string? ColorIn(int slot) =>
        Colors is { } held && slot < held.Count && held[slot] != EmptyColor ? held[slot] : null;

    /// <summary>Puts a colour in one slot, leaving the others as they were found.</summary>
    /// <remarks>
    /// Whole-list, because that is what the file holds: the slots are an ordered block of four and
    /// there is no writing to the third of them without saying what the other three are. Doing it
    /// here rather than at each caller is what keeps a tag safe from the flag editor and the map
    /// colour safe from both.
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

    public override string ToString() => $"{Category}/{File}";
}
