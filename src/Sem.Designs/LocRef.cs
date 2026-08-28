using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// A name as the empire designs file stores it: either a localisation key the game resolves, or
/// text the player typed, optionally with substitution variables.
/// </summary>
/// <remarks>
/// <para>
/// This one shape covers every name in a design: the empire name and adjective, the ship prefix,
/// planet and system names, species name, plural and adjective, and the ruler's name and titles.
/// It is recursive, because a variable's value is itself one of these; the player's own file
/// nests four deep.
/// </para>
/// <para>
/// The distinction that matters is <see cref="IsLiteral"/>. With it, <see cref="Key"/> is the text
/// to display. Without it, <see cref="Key"/> is a localisation key such as <c>%ADJ%</c> or
/// <c>AVI3_CHR_Silver</c>, and the displayed name comes from the game's localisation files.
/// </para>
/// </remarks>
public sealed class LocRef(CwBlock block) : CwView(block, FieldOrder)
{
    private static readonly string[] FieldOrder = ["key", "literal", "variables"];

    /// <summary>
    /// The localisation key, or the literal text when <see cref="IsLiteral"/> is true. Empty is
    /// valid and normal: an empire with no ship prefix stores <c>key=""</c>.
    /// </summary>
    public string Key
    {
        get => GetString("key") ?? string.Empty;
        set => SetString("key", value);
    }

    /// <summary>
    /// True when <see cref="Key"/> is text the player typed rather than a localisation key. The
    /// game writes <c>literal=yes</c> only when true and omits the field otherwise.
    /// </summary>
    public bool IsLiteral
    {
        get => GetBool("literal") ?? false;
        set => SetString("literal", value ? "yes" : null, quoted: false);
    }

    /// <summary>
    /// Substitution variables for a templated name, in order. Keys are usually positional
    /// (<c>"1"</c>, <c>"2"</c>) but can be named, as <c>adjective</c> is in species adjectives.
    /// </summary>
    public IReadOnlyList<LocVariable> Variables =>
        GetBlock("variables") is { } variables
            ? [.. variables.Nodes.Where(n => !n.IsAssignment && n.Block is not null).Select(n => new LocVariable(n.Block!))]
            : [];

    /// <summary>True when this name has no key at all, as an omitted ship prefix does.</summary>
    public bool IsEmpty => Key.Length == 0;

    /// <summary>Builds a block holding text the player typed.</summary>
    public static CwBlock CreateLiteralBlock(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var block = new CwBlock();
        block.Add(CwNode.QuotedAssignment("key", text));
        block.Add(CwNode.BareAssignment("literal", "yes"));
        return block;
    }

    /// <summary>Builds a block referring to a localisation key.</summary>
    public static CwBlock CreateKeyBlock(string localisationKey)
    {
        ArgumentNullException.ThrowIfNull(localisationKey);

        var block = new CwBlock();
        block.Add(CwNode.QuotedAssignment("key", localisationKey));
        return block;
    }

    /// <summary>Replaces this name with literal text, discarding any variables it had.</summary>
    public void SetLiteral(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        RemoveAll("variables");
        Key = text;
        IsLiteral = true;
    }

    public override string ToString() => IsLiteral ? Key : $"[{Key}]";
}

/// <summary>One substitution variable inside a templated name.</summary>
public sealed class LocVariable(CwBlock block) : CwView(block, FieldOrder)
{
    private static readonly string[] FieldOrder = ["key", "value"];

    /// <summary>The placeholder this fills, such as <c>"1"</c> or <c>adjective</c>.</summary>
    public string Key
    {
        get => GetString("key") ?? string.Empty;
        set => SetString("key", value);
    }

    /// <summary>What the placeholder resolves to, itself a name.</summary>
    public LocRef? Value => GetBlock("value") is { } value ? new LocRef(value) : null;

    public override string ToString() => $"{Key} = {Value}";
}
