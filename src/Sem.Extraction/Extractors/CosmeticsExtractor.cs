using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads the appearance choices: rooms, ship and city sets, advisor voices, name lists.</summary>
internal static class CosmeticsExtractor
{
    /// <summary>
    /// Reads the room backgrounds the designer offers.
    /// </summary>
    /// <remarks>
    /// The list is the <c>game_setup</c> group of the room selector; the selector's other groups
    /// pick rooms during play based on conditions the designer cannot evaluate. Rooms have no
    /// localised names anywhere in the game, which is why the picker shows images alone.
    /// </remarks>
    public static List<RoomDefinition> ExtractRooms(ScriptLoader loader, AssetCatalog assets)
    {
        var results = new List<RoomDefinition>();

        var document = loader.Load("gfx/portraits/asset_selectors/room_textures.txt");
        var selector = document?.Nodes.FirstOrDefault(n => n.Key == "room_selector")?.Block;
        var gameSetup = selector?.GetBlock("game_setup");

        if (gameSetup is null)
        {
            return results;
        }

        foreach (var node in gameSetup.Nodes)
        {
            if (node.Key is { Length: > 0 } key)
            {
                // Rooms are 952 by 340 and the picker shows them at about a third of that, so
                // half size is still sharper than anything the interface will draw.
                results.Add(new RoomDefinition(key)
                {
                    Image = assets.Register(
                        $"gfx/portraits/city_sets/{key}.dds",
                        $"rooms/{key}.png",
                        maxDimension: 480),
                });
            }
        }

        return results;
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
            var (characters, planets, ships) = NameExtractor.ReadNamePools(body, text);

            results.Add(new NameListDefinition(entry.Key, body.GetString("category"))
            {
                Selectable = body.GetBlock("selectable") is { } selectable
                    ? requirements.CompileTrigger(selectable)
                    : new AlwaysRequirement(true),
                Randomized = body.GetBool("randomized", defaultValue: true),
                RandomNameSource = body.GetString("customize_random_override"),
                CharacterNames = characters,
                PlanetNames = planets,
                ShipNames = ships,
            });
        }

        return results;
    }
}
