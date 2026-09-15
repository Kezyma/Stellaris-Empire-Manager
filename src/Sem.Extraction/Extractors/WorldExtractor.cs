using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads planet classes and starting systems.</summary>
internal static class WorldExtractor
{
    /// <summary>The one block in the planet-class folder that does not declare a planet class.</summary>
    private const string RandomListBlock = "random_list";

    /// <summary>
    /// Reads the planet classes, marking those an empire may start on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A starting world is flagged <c>initial</c> and not flagged <c>starting_planet = no</c>. Both
    /// are needed: a volcanic world and an arkship are each <c>initial</c>, and neither is a world a
    /// player may simply choose — an Infernal species class adds the first and an origin the second.
    /// Reading only the first offered both to everyone.
    /// </para>
    /// <para>
    /// Origins reach further still by naming a starting colony of their own, which is how Void
    /// Dwellers begin on a habitat.
    /// </para>
    /// </remarks>
    public static List<PlanetClassDefinition> ExtractPlanetClasses(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<PlanetClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/planet_classes"))
        {
            var body = entry.Body;

            // The folder holds one kind of block that is not a planet class: nine random_list
            // entries naming groups of worlds for the galaxy generator. Read as classes they
            // collapsed into a single phantom, since they all share the key, and it sat in the
            // shipped data being counted as a world nobody could ever start on.
            if (entry.Key == RandomListBlock)
            {
                continue;
            }

            results.Add(new PlanetClassDefinition(entry.Key)
            {
                Climate = body.GetString("climate"),
                IsStartingWorld = body.GetBool("initial") && body.GetBool("starting_planet", defaultValue: true),

                // Whether anybody settles here, which the page had no way of saying: "Start here"
                // answers a narrower question, so a gas giant and a habitat both read "No" and only
                // one of them is a place an empire can ever live.
                Colonizable = body.GetBool("colonizable"),

                Potential = requirements.CompileTrigger(body.GetBlock("potential")),

                // Each class names its own picture, and the larger of the two is a frame of a strip
                // rather than a file of its own. Reading the name is also what makes this the round
                // planet the game shows, instead of the square ground texture sharing its key.
                Icon = assets.RegisterSprite(
                           body.GetString("icon_large"), $"icons/planets/{entry.Key}.png")
                       ?? assets.RegisterSprite(
                           body.GetString("icon"), $"icons/planets/{entry.Key}.png"),

                // Drawn as whatever the class says its picture is, which is not always itself.
                // Twenty-one borrow another's: a machine world is painted as pc_ai and a hive world
                // as pc_infested, and asking for a picture named after the class found nothing at
                // all - so a machine world had no sky and no landscape, only whatever city the
                // empire happened to bring.
                Sky = loader.Content.Contains($"gfx/portraits/environments/{Painted(body, entry.Key)}_sky.dds")
                    ? assets.Register(
                        $"gfx/portraits/environments/{Painted(body, entry.Key)}_sky.dds",
                        $"worlds/{Painted(body, entry.Key)}_sky.png",
                        maxDimension: 800)
                    : null,

                Scenery = Scenery(Painted(body, entry.Key), loader, assets),

                // A world that is already a built thing has no room for an empire's towers.
                ShowsCity = body.GetBool("show_city", defaultValue: true),
                FixedCityLevel = loader.ResolveInt(body.GetString("fixed_city_level")),
            });
        }

        return results;
    }

    /// <summary>
    /// What every world says about itself beyond the sky over it.
    /// </summary>
    /// <remarks>
    /// The thinnest record in the game: eight of the sixty-three fields the folder declares were
    /// read, and the page's Bonus column was fed entirely by the habitability trait - so a Gaia
    /// world's own ten per cent to job output, happiness and growth appeared nowhere.
    /// </remarks>
    /// <param name="loader">The script loader.</param>
    /// <param name="requirements">The compiler, for the conditions inside a modifier block.</param>
    /// <returns>What a page about the worlds needs.</returns>
    public static List<WorldDetail> ExtractDetail(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var becomes = Terraforming(loader, requirements);
        var results = new List<WorldDetail>();

        foreach (var entry in loader.LoadDefinitions("common/planet_classes"))
        {
            if (entry.Key == RandomListBlock)
            {
                continue;
            }

            var body = entry.Body;
            var size = body.GetBlock("planet_size");
            var moon = body.GetBlock("moon_size");

            results.Add(new WorldDetail(entry.Key)
            {
                Effects = EffectsReader.Read(body, loader, requirements),
                Ideal = body.GetBool("ideal"),
                AutoPreference = body.GetList("auto_trait_prio"),
                SmallestSize = loader.ResolveInt(size?.GetString("min")),
                LargestSize = loader.ResolveInt(size?.GetString("max")),
                SmallestMoon = loader.ResolveInt(moon?.GetString("min")),
                LargestMoon = loader.ResolveInt(moon?.GetString("max")),
                Districts = body.GetString("district_set"),
                StartingDistrict = body.GetString("starting_district"),
                CarryCapacity = loader.ResolveInt(body.GetString("carry_cap_per_free_district")),
                Artificial = body.GetBool("is_artificial_planet"),
                Ringworld = body.GetBool("ringworld"),
                Asteroid = body.GetBool("asteroid"),
                Habitat = body.GetBool("habitat"),
                Becomes = becomes.GetValueOrDefault(entry.Key) ?? [],
            });
        }

        return results;
    }

    /// <summary>
    /// What each world can be turned into, read from a folder nobody had opened.
    /// </summary>
    /// <remarks>
    /// Two hundred and fifty-eight links in the folder and two hundred and thirty worth keeping,
    /// from twenty source classes to fourteen. Each is a <c>terraform_link</c> naming a world to
    /// start from, a world to end at, how long it takes and what the empire needs first; the cost is
    /// a block of scripted inline calls and is deliberately left, since what a reader wants first is
    /// what a world can become.
    /// </remarks>
    /// <param name="loader">The script loader.</param>
    /// <param name="requirements">The compiler, for the condition on a link.</param>
    /// <returns>The links, by the world they start from.</returns>
    private static Dictionary<string, List<Terraforming>> Terraforming(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var links = new Dictionary<string, List<Terraforming>>(StringComparer.Ordinal);

        foreach (var entry in loader.LoadEntries("common/terraform"))
        {
            if (entry.Node.Key != "terraform_link" || entry.Node.Block is not { } link)
            {
                continue;
            }

            if (link.GetString("from") is not { Length: > 0 } from ||
                link.GetString("to") is not { Length: > 0 } to)
            {
                continue;
            }

            // Compiled the way a plan is compiled, which is what this question is: a technology the
            // empire has not researched yet is unknown rather than refused, and asked the other way
            // round every one of the hundred and forty-five links that wait on one reads as never.
            //
            // Which leaves the fourteen the game itself has switched off, all of them the link from
            // a normal world to a hive world and all of them a second copy of one that exists live
            // a few lines above. Read without asking, the page told a reader every world in the
            // game can be turned into a Hive World.
            var needs = link.GetBlock("condition") is { } condition
                ? requirements.CompilePlanTrigger(condition)
                : null;

            if (needs?.Settled() is false)
            {
                continue;
            }

            // And a world that becomes itself, which is the Wilderness origin regrowing a planet it
            // has stripped rather than a world turning into another one. Ten of them, and under a
            // heading saying what this becomes every one reads as a mistake.
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                continue;
            }

            if (!links.TryGetValue(from, out var into))
            {
                into = [];
                links[from] = into;
            }

            if (!into.Any(t => string.Equals(t.World, to, StringComparison.Ordinal)))
            {
                into.Add(new Terraforming(to, loader.ResolveInt(link.GetString("duration")) ?? 0)
                {
                    Needs = needs,
                });
            }
        }

        return links;
    }

    /// <summary>
    /// Which world's picture this one is drawn as, which is usually but not always its own.
    /// </summary>
    /// <remarks>
    /// The game's <c>picture</c> field. Twenty-one classes name another's - the ringworlds share
    /// three pictures between six of them - and the art is filed under the name given rather than
    /// under the class, so a class that borrows one and is asked for its own finds nothing.
    /// </remarks>
    private static string Painted(CwBlock body, string key) => body.GetString("picture") ?? key;

    /// <summary>
    /// How many bands of landscape a world can have in front of its sky.
    /// </summary>
    /// <remarks>
    /// Four, as the portrait layout describes: the game paints them from the horizon forwards with
    /// the empire's own city between them.
    /// </remarks>
    private const int SceneryBands = 4;

    /// <summary>The landscape of one world, furthest band first.</summary>
    private static IReadOnlyList<SceneryBand> Scenery(string key, ScriptLoader loader, AssetCatalog assets)
    {
        var layers = new List<SceneryBand>();

        for (var band = 1; band <= SceneryBands; band++)
        {
            var source = $"gfx/portraits/environments/{key}_l{band:00}.dds";

            // Not every world has every band — an arctic world has no second one — and a gap is
            // not a fault. Asking for a picture that is not there is what fills the missing-image
            // report with noise, so it is not asked for. The band each one is keeps the gap from
            // shifting the rest forward when the city is interleaved with them.
            if (loader.Content.Contains(source) &&
                assets.Register(source, $"worlds/{key}_l{band:00}.png", maxDimension: 800) is { } image)
            {
                layers.Add(new SceneryBand(band, image));
            }
        }

        return layers;
    }

    /// <summary>
    /// Reads the starting systems, keeping only those empire creation can reach.
    /// </summary>
    /// <remarks>
    /// A system marked <c>custom_empire</c> can be chosen for any empire. One marked <c>origin</c>
    /// appears only when the selected origin names it, whatever else it is marked as, so origin
    /// takes precedence. One marked <c>nomad_init</c> belongs to an empire that starts aboard an
    /// arkship.
    ///
    /// The last of those is 4.5's, and reading only the first two dropped all seven of them - which
    /// included vela_system, the system the game's own nomadic empire starts in. Importing that
    /// empire therefore produced a design whose own starting system this app had never heard of,
    /// and the picker marked it unavailable.
    /// </remarks>
    public static List<InitializerDefinition> ExtractInitializers(ScriptLoader loader)
    {
        var results = new List<InitializerDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/solar_system_initializers"))
        {
            var usages = entry.Body.GetStrings("usage");

            var usage = usages.Contains("origin")
                ? InitializerUsage.Origin
                : usages.Contains("custom_empire")
                    ? InitializerUsage.CustomEmpire
                    : usages.Contains("nomad_init")
                        ? InitializerUsage.Nomad
                        : InitializerUsage.None;

            if (usage != InitializerUsage.None)
            {
                results.Add(new InitializerDefinition(entry.Key, usage));
            }
        }

        return results;
    }
}
