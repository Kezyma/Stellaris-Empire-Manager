using Sem.GameData;
using Sem.Ui.Components;

namespace Sem.Ui.Tests;

/// <summary>
/// The order a world and the city built on it are painted in.
/// </summary>
/// <remarks>
/// Shared by the scene and by the city picker's tiles, which is the point of the tests: the two used
/// to answer this separately and the tiles answered it wrongly, showing one distant band of a city
/// where the scene showed the whole of it.
/// </remarks>
public sealed class WorldBackdropTests
{
    private static PlanetClassDefinition World(params int[] bands) => new("pc_test")
    {
        Sky = "sky.png",
        Scenery = [.. bands.Select(b => new SceneryBand(b, $"hills{b}.png"))],
    };

    private static GraphicalCultureDefinition City(params int[] bands) => new("test_city")
    {
        CityLayers = [.. bands.Select(b => new CityLayer(b, $"towers{b}.png", 0, null))],
    };

    /// <summary>
    /// A nomad's window shows its own hull against the stars, and nothing else.
    /// </summary>
    /// <remarks>
    /// The ship stands where a city stands, so the city goes: there is no world under it to build on,
    /// and painted over the hull it was a skyline hanging in space. The world goes with it - the ark
    /// class has no sky and no landscape of its own, being a ship rather than a place.
    /// </remarks>
    [Fact]
    public void ANomadSeesItsOwnShipAndNotACity()
    {
        var arkship = new ArkshipDefinition("civilian_arkship_tier_1")
        {
            Sky = "arkship_sky.png",
            Picture = "arkship_civilian.png",
        };

        var layers = WorldBackdrop.Layers(World(1, 2), City(1, 2), 4, arkship).ToList();

        Assert.Equal(["arkship_sky.png", "arkship_civilian.png"], layers);

        // And without one, nothing about the old answer has moved.
        Assert.Equal(
            ["sky.png", "hills1.png", "towers1.png", "hills2.png", "towers2.png"],
            WorldBackdrop.Layers(World(1, 2), City(1, 2), 4).ToList());
    }

    [Fact]
    public void TheSkyGoesFirstAndTheBandsAlternate()
    {
        var layers = WorldBackdrop.Layers(World(1, 2), City(1, 2), 4).ToList();

        Assert.Equal(
            ["sky.png", "hills1.png", "towers1.png", "hills2.png", "towers2.png"],
            layers);
    }

    /// <summary>
    /// A world missing a band does not shift the city's bands up to fill the hole.
    /// </summary>
    /// <remarks>
    /// An arctic and a desert world have a first, third and fourth band and no second. Paired by
    /// position rather than by number, every row of hills after the gap came out in front of the
    /// towers it belongs behind.
    /// </remarks>
    [Fact]
    public void BandsPairByNumberRatherThanByPosition()
    {
        var layers = WorldBackdrop.Layers(World(1, 3), City(1, 2, 3), 4).ToList();

        Assert.Equal(
            ["sky.png", "hills1.png", "towers1.png", "towers2.png", "hills3.png", "towers3.png"],
            layers);
    }

    /// <summary>
    /// Only the city a world of this size would have is drawn.
    /// </summary>
    /// <remarks>
    /// The last band alone covers nearly half the frame. Painting every band put an ecumenopolis
    /// over every homeworld and left nothing of the planet to see.
    /// </remarks>
    [Fact]
    public void CityBandsAboveTheWorldsSizeAreLeftOut()
    {
        var city = new GraphicalCultureDefinition("test_city")
        {
            CityLayers =
            [
                new CityLayer(1, "towers1.png", 0, null),
                new CityLayer(2, "ecumenopolis.png", 5, null),
            ],
        };

        var layers = WorldBackdrop.Layers(World(1, 2), city, 4).ToList();

        Assert.DoesNotContain("ecumenopolis.png", layers);
        Assert.Contains("towers1.png", layers);
    }

    [Fact]
    public void EitherHalfMayBeMissing()
    {
        Assert.Equal(["sky.png", "hills1.png"], WorldBackdrop.Layers(World(1), null, 4));
        Assert.Equal(["towers1.png"], WorldBackdrop.Layers(null, City(1), 4));
        Assert.Empty(WorldBackdrop.Layers(null, null, 4));
    }
}
