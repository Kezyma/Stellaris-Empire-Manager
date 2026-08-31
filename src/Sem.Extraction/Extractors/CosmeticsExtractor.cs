using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the appearance choices: rooms, ship and city sets, advisor voices, name lists.</summary>
internal static class CosmeticsExtractor
{
    /// <summary>
    /// Reads the room backgrounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selector's <c>game_setup</c> group is what the game's own designer offers, and those come
    /// first. But it names sixty-seven rooms in all, and the rest are not rooms the game withholds —
    /// they are rooms it hands out by circumstance, each guarded in the <c>ruler</c> group by
    /// something only a running game can answer: a fallen empire's machine, a galactic emperor, a
    /// specialist subject. Nothing grants them at design time, and nothing needs to: a design may
    /// name any of them and the game draws it, which is how the Federated Theian Preservers sit in
    /// an ancient brick room the picker never offered.
    /// </para>
    /// <para>
    /// So all of them are read, and one is left out only when the installation has no picture for
    /// it. Rooms have no localised names anywhere in the game, which is why the picker shows images
    /// alone.
    /// </para>
    /// </remarks>
    public static List<RoomDefinition> ExtractRooms(ScriptLoader loader, AssetCatalog assets)
    {
        var document = loader.Load("gfx/portraits/asset_selectors/room_textures.txt");
        var selector = document?.Nodes.FirstOrDefault(n => n.Key == "room_selector")?.Block;

        if (selector is null)
        {
            return [];
        }

        var offered = selector.GetBlock("game_setup") is { } setup
            ? setup.Nodes.Select(n => n.Key).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : [];

        var results = new List<RoomDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in Named(selector).Where(k => k.EndsWith("_room", StringComparison.Ordinal)))
        {
            if (!seen.Add(key))
            {
                continue;
            }

            // Rooms are 952 by 340. The picker shows them small, but the empire preview shows one at
            // the width of a panel, so they are kept at three quarters rather than a third.
            if (assets.Register($"gfx/portraits/city_sets/{key}.dds", $"rooms/{key}.png", maxDimension: 720)
                is { } image)
            {
                results.Add(new RoomDefinition(key)
                {
                    Image = image,
                    IsOffered = offered.Contains(key),
                });
            }
        }

        // The designer's own list first, in the order the game gives it, then the rest.
        return [.. results.OrderByDescending(r => r.IsOffered)];
    }

    /// <summary>
    /// Every room the selector names, wherever in its tree it names one.
    /// </summary>
    /// <remarks>
    /// A room appears as a key in the <c>game_setup</c> list and as a value elsewhere, so both are
    /// gathered.
    /// </remarks>
    private static IEnumerable<string> Named(CwBlock block)
    {
        foreach (var node in block.Nodes)
        {
            if (node.Key is { Length: > 0 } key)
            {
                yield return key;
            }

            if (node.ScalarValue is { Length: > 0 } value)
            {
                yield return value;
            }

            if (node.Block is { } child)
            {
                foreach (var found in Named(child))
                {
                    yield return found;
                }
            }
        }
    }

    /// <summary>
    /// Reads the named sets of country flags the game's own empires carry.
    /// </summary>
    /// <remarks>
    /// These are markers rather than pictures. The United Nations of Earth carries
    /// <c>custom_start_screen</c>, which is what gives it an opening screen of its own, and
    /// <c>human_1</c>, which is what its events look for. A design names a set with
    /// <c>flag = "empire_human_1"</c>, and a player's own file may hold one already — copying an
    /// empire out of the game brings its flags along.
    /// </remarks>
    public static List<EmpireFlagSet> ExtractEmpireFlagSets(
        ScriptLoader loader,
        IReadOnlyList<PrescriptedEmpireSummary> empires)
    {
        ArgumentNullException.ThrowIfNull(empires);

        var carriedBy = empires
            .Where(e => e.FlagSet is { Length: > 0 })
            .GroupBy(e => e.FlagSet!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(e => e.Key)], StringComparer.Ordinal);

        return
        [
            .. loader.LoadDefinitions("common/prescripted_flags")
                .Select(entry => new EmpireFlagSet(entry.Key, entry.Body.GetList("flags"))
                {
                    Empires = carriedBy.GetValueOrDefault(entry.Key, []),
                })
        ];
    }

    /// <summary>
    /// Reads the ship and city appearance sets.
    /// </summary>
    /// <remarks>
    /// A set without a <c>selectable</c> condition is offered; the game marks the ones reserved
    /// for fallen empires and pirates by making that condition never true.
    /// </remarks>
    public static List<GraphicalCultureDefinition> ExtractGraphicalCultures(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<GraphicalCultureDefinition>();
        var bands = CityBands(loader);

        foreach (var entry in loader.LoadDefinitions("common/graphical_culture"))
        {
            var body = entry.Body;
            var citySource = $"gfx/portraits/city_sets/{entry.Key}_city_l01.dds";

            results.Add(new GraphicalCultureDefinition(entry.Key)
            {
                Selectable = body.GetBlock("selectable") is { } selectable
                    ? requirements.CompileTrigger(selectable)
                    : new AlwaysRequirement(true),
                Fallback = body.GetString("fallback"),

                // A set with no city artwork can still dress ships, but cannot be a city choice —
                // and asking for the picture anyway is what put two dozen entries in the report of
                // images the installation is missing.
                HasCityArt = loader.Content.Contains(citySource),
                CityPreview = loader.Content.Contains(citySource)
                    ? assets.Register(citySource, $"cities/{entry.Key}.png", maxDimension: 400)
                    : null,
                CityLayers = CityLayers(entry.Key, bands, loader, assets),

                // The first kind a set builds is what the game's own browser sorts it by. A set
                // that names none builds nothing and flies its fallback's ships.
                ShipCategory = body.GetBlock("ship_kinds")?.Nodes
                    .Select(n => n.ScalarValue)
                    .FirstOrDefault(v => v is { Length: > 0 }),
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the kinds of leader the game defines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A class names its own display text. Its icon is one slice of a strip: the game keeps all four
    /// in <c>leaders_medium.dds</c> as a single sprite of four frames and picks the frame in code,
    /// so the frame is the class's own position in this file.
    /// </para>
    /// <para>
    /// Not <c>GFX_leader_bg_official</c>, which the name invites and which is wrong. That is a
    /// painted city — the backdrop a leader's portrait is shown against — and using it gave every
    /// class a landscape where its icon should be.
    /// </para>
    /// </remarks>
    public static List<LeaderClassDefinition> ExtractLeaderClasses(ScriptLoader loader, AssetCatalog assets)
    {
        var results = new List<LeaderClassDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/leader_classes"))
        {
            results.Add(new LeaderClassDefinition(entry.Key, entry.Body.GetString("name") ?? entry.Key)
            {
                // The game says a class may rule unless it says otherwise, and only the envoy does.
                CanRule = entry.Body.GetBool("can_rule_empire", defaultValue: true),
                Icon = assets.RegisterSprite(
                    LeaderIcons,
                    $"icons/leaders/{entry.Key}.png",
                    frame: loader.ResolveInt(entry.Body.GetString("icon"))),
            });
        }

        return results;
    }

    /// <summary>
    /// The strip holding one icon per kind of leader.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which slice belongs to which class is the class's own business and it says so: the file
    /// carries the comment "1-based index in the icon file" against the field, and the four base
    /// classes claim 1, 3, 4 and 5 — commander, scientist, official and envoy. That is not the
    /// order they are written in, so counting them out would have given the official the
    /// commander's badge.
    /// </para>
    /// <para>
    /// The strip has four slices, and the envoy asks for a fifth that is not in it. Asking for a
    /// slice the sheet does not have yields nothing rather than a stray cut of the picture, and
    /// costs nothing here since an envoy may not rule and so is never offered.
    /// </para>
    /// <para>
    /// Not <c>GFX_leader_bg_official</c> and its fellows, which are painted cities a portrait is
    /// shown against rather than badges.
    /// </para>
    /// </remarks>
    private const string LeaderIcons = "GFX_leader_icons_medium";

    /// <summary>
    /// Reads the ships a nomadic empire may begin as.
    /// </summary>
    /// <remarks>
    /// Found by the flag the game marks them with rather than by name. Nine arkships exist across
    /// three families and three tiers, but only the first tier of each is something a player starts
    /// with — the rest are built later — and <c>is_starting_arkship</c> is how the game itself says
    /// which. Declaration order is the order its own panel lists them in: civilian, science,
    /// military.
    ///
    /// Each names its own picture — <c>icon = ship_size_civilian_arkship_tier_1</c> — which resolves
    /// to one frame of the ship-size sheet rather than to a file. Taking only the key left the
    /// panel's three cards as the only ones in the designer with nothing to look at.
    /// </remarks>
    public static List<ArkshipDefinition> ExtractArkships(ScriptLoader loader, AssetCatalog assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var results = new List<ArkshipDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/ship_sizes"))
        {
            if (!entry.Body.GetBool("is_starting_arkship"))
            {
                continue;
            }

            results.Add(new ArkshipDefinition(entry.Key)
            {
                Icon = assets.RegisterSprite(
                    $"GFX_{entry.Body.GetString("icon")}",
                    $"icons/arkships/{entry.Key}.png"),
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the groups the game sorts its shipset browser into.
    /// </summary>
    /// <remarks>
    /// <c>common/ship_sets</c> exists for exactly this and says so: it is there "to categorize the
    /// list of ship graphics cultures in the ship set browser within the empire editor". Each group
    /// names itself and carries a condition on the kind of ship a set builds — biological gathers
    /// <c>bio_ship</c>, mechanical gathers everything that is not — so the headings and the sorting
    /// are both the game's rather than ours.
    /// </remarks>
    public static List<ShipSetDefinition> ExtractShipSets(ScriptLoader loader)
    {
        var results = new List<ShipSetDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/ship_sets"))
        {
            var potential = entry.Body.GetBlock("potential");
            var inverted = potential?.GetBlock("NOT");

            results.Add(new ShipSetDefinition(entry.Key, entry.Body.GetString("name") ?? entry.Key)
            {
                Category = (inverted ?? potential)?.GetString("uses_ship_category"),
                Inverted = inverted is not null,
            });
        }

        return results;
    }

    /// <summary>
    /// How built-up a world each band of city belongs to, in the order the game paints them.
    /// </summary>
    /// <remarks>
    /// Read rather than written down, because the bounds are the whole point: the last band is an
    /// ecumenopolis and painting it over a designer's homeworld hides the world. The game lists the
    /// bands as repeated <c>city</c> entries in the <c>planet</c> block, interleaved with the
    /// <c>environment</c> ones, so their order in the file is their order on the screen.
    /// </remarks>
    private static IReadOnlyList<(int Min, int? Max)> CityBands(ScriptLoader loader)
    {
        var planet = loader.Load("gfx/portraits/portraits/00_portraits_main.txt")?.Nodes
            .FirstOrDefault(n => n.Key == "planet")?.Block;

        if (planet is null)
        {
            return [];
        }

        return
        [
            .. planet.Nodes
                .Where(n => n.Key == "city" && n.Block is not null)
                .Select(n => (
                    Min: loader.ResolveInt(n.Block!.GetString("min_pop")) ?? 0,
                    Max: loader.ResolveInt(n.Block!.GetString("max_pop"))))
        ];
    }

    /// <summary>One empire's city, band by band, furthest from the viewer first.</summary>
    private static IReadOnlyList<CityLayer> CityLayers(
        string key,
        IReadOnlyList<(int Min, int? Max)> bands,
        ScriptLoader loader,
        AssetCatalog assets)
    {
        var layers = new List<CityLayer>();

        for (var band = 1; band <= bands.Count; band++)
        {
            var source = $"gfx/portraits/city_sets/{key}_city_l{band:00}.dds";

            if (loader.Content.Contains(source) &&
                assets.Register(source, $"cities/{key}_l{band:00}.png", maxDimension: 800) is { } image)
            {
                var (min, max) = bands[band - 1];
                layers.Add(new CityLayer(band, image, min, max));
            }
        }

        return layers;
    }

    /// <summary>Reads the advisor voices, in the order the game lists them.</summary>
    public static List<AdvisorVoiceDefinition> ExtractAdvisorVoices(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<AdvisorVoiceDefinition>();

        foreach (var entry in loader.LoadDefinitions("sound/advisor_voice_types"))
        {
            var body = entry.Body;

            results.Add(new AdvisorVoiceDefinition(entry.Key, body.GetString("name") ?? entry.Key)
            {
                Playable = requirements.CompileTrigger(body.GetBlock("playable")),
                // These borrow icons from elsewhere, mostly the ethics and traits they suit, so
                // the declared path is followed rather than guessed at.
                Icon = body.GetString("icon") is { Length: > 0 } icon
                    ? assets.Register(
                        icon.Replace('\\', '/'),
                        $"icons/advisors/{Path.GetFileNameWithoutExtension(icon)}.png")
                    : null,
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the species name lists.
    /// </summary>
    /// <remarks>
    /// These carry no content-pack gating of their own. A pack's name lists are reached only
    /// through its species classes, so owning the pack is already implied by getting that far.
    /// </remarks>
    public static List<NameListDefinition> ExtractNameLists(
        ScriptLoader loader,
        RequirementCompiler requirements,
        IReadOnlyDictionary<string, string> text)
    {
        var results = new List<NameListDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/name_lists"))
        {
            var body = entry.Body;
            var pools = NameExtractor.ReadNamePools(body, text);

            results.Add(new NameListDefinition(entry.Key, body.GetString("category"))
            {
                Selectable = body.GetBlock("selectable") is { } selectable
                    ? requirements.CompileTrigger(selectable)
                    : new AlwaysRequirement(true),
                Randomized = body.GetBool("randomized", defaultValue: true),
                RandomNameSource = body.GetString("customize_random_override"),
                CharacterNames = pools.Characters,
                PlanetNames = pools.Planets,
                ShipNames = pools.Ships,
                FleetNames = pools.Fleets,
                FleetPattern = pools.FleetPattern,
            });
        }

        return results;
    }
}
