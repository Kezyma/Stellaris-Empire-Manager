using Sem.Designs;
using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// One colour of the game's flag palette, written the way a stylesheet wants it.
/// </summary>
/// <remarks>
/// <para>
/// The game keeps two shades against every one of its seventy-two names - one a flag is tinted with
/// and one the galaxy map draws, and they differ for twenty-four of them - so every control that
/// shows a colour has to say which of the two it means. Six places were each writing their own
/// <c>rgb(...)</c>, three of them alongside their own copy of "look the key up, and answer nothing
/// if it names nothing".
/// </para>
/// <para>
/// A slot with no colour in it holds <see cref="EmpireFlag.EmptyColor"/> rather than being absent,
/// so that string is not a palette name and never resolves to one.
/// </para>
/// </remarks>
public static class Swatches
{
    /// <summary>The shade a flag is tinted with.</summary>
    public static (byte R, byte G, byte B) FlagShade(FlagColorDefinition color)
    {
        ArgumentNullException.ThrowIfNull(color);

        return (color.Red, color.Green, color.Blue);
    }

    /// <summary>The shade the galaxy map draws, which differs for 24 of the 72.</summary>
    public static (byte R, byte G, byte B) MapShade(FlagColorDefinition color)
    {
        ArgumentNullException.ThrowIfNull(color);

        return (color.MapRed, color.MapGreen, color.MapBlue);
    }

    /*
       There is no ShipShade beside these two, deliberately. The game keeps a third value against
       every name under ship =, and the header of flags/colors.txt calls it the entity tint - but it
       holds only 14 distinct values across the 72 names, so all six browns share 255,228,136 and all
       six reds share 255,57,36. Painting anything with it collapses six choices into one tile, which
       is not what the game's own colour grid shows and not what a player sees on their fleet. It is
       a per-family accent of some kind; until that is understood, ships are drawn in the swatch's
       own colour like everything else. See docs/flag-colours.md.
    */

    /// <summary>A colour as CSS, or nothing where there is no colour.</summary>
    public static string? Css((byte R, byte G, byte B)? color) =>
        color is { } c ? $"rgb({c.R},{c.G},{c.B})" : null;

    /// <summary>A palette entry in the shade a flag is tinted with.</summary>
    public static string Flag(FlagColorDefinition color) => Css(FlagShade(color))!;

    /// <summary>A palette entry in the shade the galaxy map draws.</summary>
    public static string Map(FlagColorDefinition color) => Css(MapShade(color))!;

    /// <summary>
    /// The colour a palette name stands for, in whichever of the two shades is wanted.
    /// </summary>
    /// <param name="database">The extracted game data.</param>
    /// <param name="key">The palette name the design stores, which may be empty or the empty slot.</param>
    /// <param name="shade">Which of the two shades to read, normally <see cref="FlagShade"/>.</param>
    public static string? Of(
        GameDatabase? database,
        string? key,
        Func<FlagColorDefinition, (byte R, byte G, byte B)> shade)
    {
        ArgumentNullException.ThrowIfNull(shade);

        return Entry(database, key) is { } color ? Css(shade(color)) : null;
    }

    /// <summary>One entry of the game's flag palette, by name, or nothing.</summary>
    public static FlagColorDefinition? Entry(GameDatabase? database, string? key) =>
        database is not null
        && key is { Length: > 0 } named
        && !string.Equals(named, EmpireFlag.EmptyColor, StringComparison.Ordinal)
            ? database.FlagColor(named)
            : null;

    /// <summary>
    /// What to call a colour slot, or what to say instead where nothing is in it.
    /// </summary>
    /// <remarks>
    /// The wording is the caller's, because the three controls that need it are not saying the same
    /// thing: a slot the player may fill says "Not set", a slot the flag fills for them says
    /// "Automatic", and a sentence read aloud says "the game's choice".
    /// </remarks>
    /// <param name="key">The palette name the design stores.</param>
    /// <param name="whenEmpty">What to say when there is no colour.</param>
    public static string Named(string? key, string whenEmpty) =>
        key is { Length: > 0 } named
        && !string.Equals(named, EmpireFlag.EmptyColor, StringComparison.Ordinal)
            ? Localizer.Prettify(named)
            : whenEmpty;
}
