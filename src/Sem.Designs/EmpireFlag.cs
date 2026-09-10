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
    /// The two slots the galaxy map reads: the empire's border, and the territory inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four colours, and the flag draws the first two. A background is three shapes packed into one
    /// file's red, green and blue channels, and <c>gfx/FX/flag_sprite.shader</c> tints each channel
    /// with one of the first three - but across all sixty-three backgrounds the game ships, the
    /// brightest pixel in any blue channel is 11 of 255. Nothing is drawn there, so the third cannot
    /// change a flag; and the shader's fourth line is commented out, so the fourth cannot either.
    /// </para>
    /// <para>
    /// Neither is idle, though: they are the map's, not the flag's, and the flag shader is the wrong
    /// place to have looked for them. The third draws the empire's border and the fourth fills the
    /// territory inside it, which is why nearly every empire leaves the fourth alone - a fill the
    /// game picks from the border is the ordinary look, and choosing one is the exception.
    /// </para>
    /// </remarks>
    public const int MapBorderSlot = 2;

    /// <inheritdoc cref="MapBorderSlot"/>
    public const int MapFillSlot = 3;

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
    /// The empire's border on the galaxy map, or null where the game picks one.
    /// </summary>
    /// <remarks>
    /// Read and written through the colour slots rather than beside them, so it is subject to
    /// everything they already guarantee: padded the way the game pads, quoted the way the game
    /// quotes, and carried through untouched when the flag editor rewrites the two it draws.
    ///
    /// Thirty-two of the game's fifty-three ready-made empires write <c>null</c> here and seventeen
    /// more write <c>black</c>; only four choose a colour of their own.
    /// </remarks>
    public string? MapBorder
    {
        get => ColorIn(MapBorderSlot);
        set => SetColor(MapBorderSlot, value);
    }

    /// <summary>
    /// What fills the territory inside that border, or null where the game picks it.
    /// </summary>
    /// <remarks>
    /// Left alone by all but two of the three hundred and three colour blocks the game ships, which
    /// is what a slot looks like when the default is the answer nearly everybody wants.
    /// </remarks>
    public string? MapFill
    {
        get => ColorIn(MapFillSlot);
        set => SetColor(MapFillSlot, value);
    }

    /// <summary>What one slot holds, with the game's word for "nothing" read as nothing.</summary>
    private string? ColorIn(int slot) =>
        Colors is { } held && slot < held.Count && held[slot] != EmptyColor ? held[slot] : null;

    /// <summary>Puts a colour in one slot, leaving the others as they were found.</summary>
    /// <remarks>
    /// Whole-list, because that is what the file holds: the slots are an ordered block of four and
    /// there is no writing to the third of them without saying what the other three are. Doing it
    /// here rather than at each caller is what keeps the map's two colours safe from the flag
    /// editor, and the flag's two safe from the map's.
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
