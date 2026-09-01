using Sem.GameData;

namespace Sem.Ui.Components;

/// <summary>
/// A world with a city built on it, as a stack of pictures.
/// </summary>
/// <remarks>
/// The game paints a world as a sky with bands of landscape in front of it, and paints the empire's
/// city between those bands, so one row of hills sits behind the towers and the next sits in front.
/// It cannot be one picture: every combination of world and city would be thousands of them.
/// </remarks>
public static class WorldBackdrop
{
    /// <summary>
    /// The world and the city interleaved, furthest from the viewer first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sky, then a band of landscape, then a band of city, and so on — which is the order the game's
    /// own layout lists them in, and the reason a city cannot simply be painted on top. A world with
    /// fewer bands than the city has, or the other way about, keeps whatever is left over rather
    /// than dropping it.
    /// </para>
    /// <para>
    /// Only the city bands belonging to a world of <paramref name="level"/> are drawn. Painting all
    /// of them put an ecumenopolis over every homeworld — the last band alone covers nearly half the
    /// frame — and left nothing of the planet to see.
    /// </para>
    /// <para>
    /// Paired by the band each picture is rather than by its place in the list, because two worlds
    /// are missing a band: an arctic and a desert world have no second one, and matched by position
    /// their hills came out in front of the towers they belong behind.
    /// </para>
    /// <para>
    /// Shared by the scene and by the city picker's tiles, so that choosing a city is choosing from
    /// pictures of the thing the scene will draw. They were two answers to one question once, and
    /// the tiles were the wrong one.
    /// </para>
    /// </remarks>
    /// <param name="world">The world, or null to draw the city against nothing.</param>
    /// <param name="city">The city, or null to draw the world alone.</param>
    /// <param name="level">How built-up the world is, on the game's own nought-to-five scale.</param>
    public static IEnumerable<string> Layers(
        PlanetClassDefinition? world,
        GraphicalCultureDefinition? city,
        int level)
    {
        if (world?.Sky is { Length: > 0 } sky)
        {
            yield return sky;
        }

        var scenery = world?.Scenery ?? [];
        var towers = city?.CityLayers ?? [];

        var bands = Math.Max(
            scenery.Count > 0 ? scenery.Max(s => s.Band) : 0,
            towers.Count > 0 ? towers.Max(c => c.Band) : 0);

        for (var band = 1; band <= bands; band++)
        {
            if (scenery.FirstOrDefault(s => s.Band == band) is { } hills)
            {
                yield return hills.Image;
            }

            if (towers.FirstOrDefault(c => c.Band == band) is { } built && built.AppearsAt(level))
            {
                yield return built.Image;
            }
        }
    }
}
