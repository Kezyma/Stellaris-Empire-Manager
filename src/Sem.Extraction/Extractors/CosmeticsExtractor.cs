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

            // Rooms are 952 by 340 and the picker shows them at about a third of that, so half size
            // is still sharper than anything the interface will draw.
            if (assets.Register($"gfx/portraits/city_sets/{key}.dds", $"rooms/{key}.png", maxDimension: 480)
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

                // A set with no city artwork can still dress ships, but cannot be a city choice.
                HasCityArt = loader.Content.Contains(citySource),
                CityPreview = assets.Register(citySource, $"cities/{entry.Key}.png", maxDimension: 400),
            });
        }

        return results;
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
            });
        }

        return results;
    }
}
