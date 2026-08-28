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
    public static List<RoomDefinition> ExtractRooms(ScriptLoader loader)
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
                results.Add(new RoomDefinition(key) { Image = $"rooms/{key}.png" });
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
        RequirementCompiler requirements)
    {
        var results = new List<GraphicalCultureDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/graphical_culture"))
        {
            var body = entry.Body;

            results.Add(new GraphicalCultureDefinition(entry.Key)
            {
                Selectable = body.GetBlock("selectable") is { } selectable
                    ? requirements.CompileTrigger(selectable)
                    : new AlwaysRequirement(true),
                Fallback = body.GetString("fallback"),
                HasCityArt = loader.Content.Contains($"gfx/portraits/city_sets/{entry.Key}_city_l01.dds"),
            });
        }

        return results;
    }

    /// <summary>Reads the advisor voices, in the order the game lists them.</summary>
    public static List<AdvisorVoiceDefinition> ExtractAdvisorVoices(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<AdvisorVoiceDefinition>();

        foreach (var entry in loader.LoadDefinitions("sound/advisor_voice_types"))
        {
            var body = entry.Body;

            results.Add(new AdvisorVoiceDefinition(entry.Key, body.GetString("name") ?? entry.Key)
            {
                Playable = requirements.CompileTrigger(body.GetBlock("playable")),
                Icon = body.GetString("icon") is { Length: > 0 } icon
                    ? $"icons/advisors/{Path.GetFileNameWithoutExtension(icon)}.png"
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
        RequirementCompiler requirements)
    {
        var results = new List<NameListDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/name_lists"))
        {
            var body = entry.Body;

            results.Add(new NameListDefinition(entry.Key, body.GetString("category"))
            {
                Selectable = body.GetBlock("selectable") is { } selectable
                    ? requirements.CompileTrigger(selectable)
                    : new AlwaysRequirement(true),
            });
        }

        return results;
    }
}
