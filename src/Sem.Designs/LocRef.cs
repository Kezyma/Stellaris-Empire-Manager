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

    /// <summary>The placeholder the game wraps a species name in to make an adjective.</summary>
    public const string AdjectiveTemplate = "%ADJECTIVE%";

    /// <summary>Replaces this name with literal text, discarding any variables it had.</summary>
    public void SetLiteral(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        RemoveAll("variables");
        Key = text;
        IsLiteral = true;
    }

    /// <summary>
    /// Replaces this name with a localisation key, discarding any variables it had.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="SetLiteral"/>, and the one the game uses whenever a name was
    /// chosen from a list rather than typed. The <c>literal</c> field is removed rather than set to
    /// no, because the game omits it entirely on a name that is a key.
    /// </remarks>
    public void SetKey(string localisationKey)
    {
        ArgumentNullException.ThrowIfNull(localisationKey);

        RemoveAll("variables");
        Key = localisationKey;
        IsLiteral = false;
    }

    /// <summary>
    /// Makes this an adjective formed from a species name, as the game forms one.
    /// </summary>
    /// <remarks>
    /// The game does not store "Oxanalytoran". It stores the template and the species it is made
    /// from — <c>key="%ADJECTIVE%" variables={ { key="adjective" value={ key="SPEC_Oxanalytor" } } }</c>
    /// — and builds the word when it shows it, which is what lets the same design read correctly in
    /// a language that forms adjectives differently.
    /// </remarks>
    public void SetAdjectiveOf(string speciesNameKey)
    {
        ArgumentNullException.ThrowIfNull(speciesNameKey);

        RemoveAll("variables");
        Key = AdjectiveTemplate;
        IsLiteral = false;

        var variable = new CwBlock();
        variable.Add(CwNode.QuotedAssignment("key", "adjective"));
        variable.Add(CwNode.Assignment("value", CreateKeyBlock(speciesNameKey)));

        // The game writes the list as unkeyed blocks, so the variable goes in without a name.
        GetOrAddBlock("variables").Add(new CwNode(variable));
    }

    /// <summary>
    /// Makes this a name built from a format and the words that fill its blanks.
    /// </summary>
    /// <remarks>
    /// How the game writes a name it generated: <c>key="AofB" variables={ { key="1" value={ key="Empire" } }
    /// { key="2" value={ key="Pakshalika" } } }</c>, where <c>AofB</c> is the format "$1$ of $2$".
    /// Storing the pieces rather than the finished words is what lets the same design read as
    /// "Empire of Pakshalika" in English and as whatever that language would say elsewhere.
    ///
    /// The blanks are numbered from one, which is the game's own convention and is why they are
    /// counted here rather than passed in.
    /// </remarks>
    public void SetFormat(string formatKey, IEnumerable<string> parts)
    {
        ArgumentException.ThrowIfNullOrEmpty(formatKey);
        ArgumentNullException.ThrowIfNull(parts);

        RemoveAll("variables");
        Key = formatKey;
        IsLiteral = false;

        var variables = GetOrAddBlock("variables");
        var position = 1;

        foreach (var part in parts)
        {
            var variable = new CwBlock();
            variable.Add(CwNode.QuotedAssignment("key", position.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            variable.Add(CwNode.Assignment("value", CreateLiteralBlock(part)));

            // Unkeyed, as the game writes the list.
            variables.Add(new CwNode(variable));
            position++;
        }
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
