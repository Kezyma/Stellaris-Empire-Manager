using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Turning a palette name into something to paint with.
/// </summary>
/// <remarks>
/// Three controls and the flag preview call this and nothing in the suite could reach it, because
/// every test here is service-level and its only callers were components. The part worth pinning is
/// that the game keeps two colours against every name - one for a flag and one for the map - and
/// they differ for twenty-four of the seventy-two, so reading the wrong one is a mistake that looks
/// right two thirds of the time.
/// </remarks>
public sealed class SwatchesTests
{
    // The two shades deliberately differ, which is the thing worth pinning: the game keeps both
    // and they are not the same for a third of the palette.
    private static readonly FlagColorDefinition Blue =
        new("blue", 10, 20, 30) { MapRed = 40, MapGreen = 50, MapBlue = 60 };

    private static GameDatabase Database => new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        FlagColors = [Blue],
    };

    /// <summary>The flag shade and the map shade are different questions.</summary>
    [Fact]
    public void TheTwoShadesAreReadFromDifferentFields()
    {
        Assert.Equal(((byte)10, (byte)20, (byte)30), Swatches.FlagShade(Blue));
        Assert.Equal(((byte)40, (byte)50, (byte)60), Swatches.MapShade(Blue));
    }

    /// <summary>And each comes out as CSS a browser will take.</summary>
    [Fact]
    public void AShadeIsWrittenAsCss()
    {
        Assert.Equal("rgb(10,20,30)", Swatches.Flag(Blue));
        Assert.Equal("rgb(40,50,60)", Swatches.Map(Blue));
    }

    /// <summary>Nothing to paint is nothing, rather than black.</summary>
    [Fact]
    public void NoColourIsNotAColour() => Assert.Null(Swatches.Css(null));

    /// <summary>A name the database knows is looked up in the shade asked for.</summary>
    [Fact]
    public void ANameIsResolvedInWhicheverShadeIsWanted()
    {
        Assert.Equal("rgb(10,20,30)", Swatches.Of(Database, "blue", Swatches.FlagShade));
        Assert.Equal("rgb(40,50,60)", Swatches.Of(Database, "blue", Swatches.MapShade));
    }

    /// <summary>
    /// The empty slot is not a colour, and neither is a name nothing has.
    /// </summary>
    /// <remarks>
    /// The game writes a real token in a slot the player has left to it, so "there is no colour
    /// here" and "the colour is called nothing" are the same answer and both have to be one.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no_such_colour")]
    public void AnEmptyOrUnknownSlotResolvesToNothing(string? key)
    {
        Assert.Null(Swatches.Of(Database, key, Swatches.FlagShade));
        Assert.Null(Swatches.Entry(Database, key));
    }

    /// <summary>Including the game's own word for an empty slot.</summary>
    [Fact]
    public void TheGamesOwnEmptyTokenIsNotAColour()
    {
        Assert.Null(Swatches.Entry(Database, EmpireFlag.EmptyColor));
        Assert.Null(Swatches.Of(Database, EmpireFlag.EmptyColor, Swatches.FlagShade));
    }

    /// <summary>With no database there is nothing to look anything up in.</summary>
    [Fact]
    public void WithoutADatabaseThereIsNothingToResolve() =>
        Assert.Null(Swatches.Of(null, "blue", Swatches.FlagShade));

    /// <summary>
    /// A slot with nothing in it is named by whoever is asking.
    /// </summary>
    /// <remarks>
    /// Three controls need this and none of them says the same thing: a slot the player may fill
    /// says "Not set", one the flag fills for them says "Automatic", and a sentence read aloud says
    /// "the game's choice".
    /// </remarks>
    [Fact]
    public void AnEmptySlotIsNamedByTheCaller()
    {
        Assert.Equal("Not set", Swatches.Named(null, "Not set"));
        Assert.Equal("Automatic", Swatches.Named("", "Automatic"));
        Assert.Equal("the game's choice", Swatches.Named(EmpireFlag.EmptyColor, "the game's choice"));
    }

    /// <summary>And a slot with a colour in it is named after the colour.</summary>
    [Fact]
    public void AFilledSlotIsNamedAfterItsColour() =>
        Assert.Equal("Blue", Swatches.Named("blue", "Not set"));
}
