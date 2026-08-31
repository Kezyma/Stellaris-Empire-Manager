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
