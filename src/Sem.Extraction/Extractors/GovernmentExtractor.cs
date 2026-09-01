using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads authorities, civics, origins and government types.</summary>
internal static class GovernmentExtractor
{
    /// <summary>Reads the government authorities.</summary>
    public static List<AuthorityDefinition> ExtractAuthorities(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<AuthorityDefinition>();
        var councilors = ReadRulerCouncilors(loader, requirements);

        foreach (var entry in loader.LoadDefinitions("common/governments/authorities"))
        {
            var body = entry.Body;

            results.Add(new AuthorityDefinition(entry.Key)
            {
                Playable = body.GetBlock("playable") is { } playable
                    ? requirements.CompileTrigger(playable)
                    : new AlwaysRequirement(true),
                Possible = requirements.CompileRequirementsList(body.GetBlock("possible")),

                // An authority restricted to a country type the player cannot be is the game's own.
                AiOnly = IsAiOnly(body),
                ForcedTraits = ReadForcedTraits(body),
                HasHeir = body.GetBool("has_heir"),
                ElectionType = body.GetString("election_type"),
                // The tags are the authority's own description of itself: succession, whether there
                // is an heir, and whatever a particular authority does to its capital. The file's
                // own header calls them "the translation tags to add to the description", and
                // without them the imperial authority came out as one line about leader pools with
                // nothing about the throne it is named for.
                Effects = WithRuler(
                    EffectsReader.Read(body, loader, requirements, tagsKey: "tags"),
                    body.GetString("ruler_council_position"),
                    councilors),
                Icon = assets.Register(
                    $"gfx/interface/icons/governments/authorities/{entry.Key}.dds",
                    $"icons/authorities/{entry.Key}.png"),
            });
        }

        return results;
    }

    /// <summary>
    /// Reads civics and origins, which the game defines in the same folder and distinguishes with
    /// a flag.
    /// </summary>
    public static List<CivicDefinition> ExtractCivics(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var results = new List<CivicDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/governments/civics"))
        {
            var body = entry.Body;
            var isOrigin = body.GetBool("is_origin");
            var effects = EffectsReader.Read(body, loader, requirements, tagsKey: "tags");
            var secondarySpecies = body.GetBlock("has_secondary_species");

            results.Add(new CivicDefinition(entry.Key, isOrigin)
            {
                Playable = ReadPlayable(body, requirements),
                Potential = requirements.CompileRequirementsList(body.GetBlock("potential")),
                Possible = requirements.CompileRequirementsList(body.GetBlock("possible")),
                ForcedTraits = ReadForcedTraits(body),
                SoftTraits = ReadTraitList(body.GetBlock("soft_traits")),
                Effects = effects,
                StartingColony = body.GetString("starting_colony"),
                HabitabilityPreference = body.GetString("habitability_preference"),
                Initializers = body.GetList("initializers"),
                AddedPlanetClasses = body.GetList("added_planet_types"),
                RemovedPlanetClasses = body.GetList("removed_planet_types"),
                RequiresSecondarySpecies = secondarySpecies is not null,
                SecondarySpeciesTraits = secondarySpecies is null
                    ? []
                    : ReadTraitList(secondarySpecies.GetBlock("traits")),
                EffectsKey = body.GetString("description"),
                PenaltiesKey = body.GetString("negative_description"),
                Icon = ResolveIcon(entry.Key, body, isOrigin, assets),

                // The scene an origin opens on. Only origins have one, so a civic is not asked;
                // asking and being told no is how two hundred civics came to be counted as missing
                // artwork. Wider than it is tall and a good deal larger than an icon, so it is
                // capped: sixty-one at full size would weigh more than every other picture together.
                Picture = body.GetString("picture") is { Length: > 0 } picture
                    ? assets.RegisterSprite(picture, $"pictures/origins/{entry.Key}.png", maxDimension: 480)
                    : null,
            });
        }

        return results;
    }

    /// <summary>
    /// Reads the government types, which decide what an empire is called. The game picks the
    /// highest-weighted one whose conditions the design meets, breaking ties by file order.
    /// </summary>
    public static List<GovernmentTypeDefinition> ExtractGovernmentTypes(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<GovernmentTypeDefinition>();
        var order = 0;

        foreach (var entry in loader.LoadDefinitions("common/governments"))
        {
            var body = entry.Body;

            // The authorities, civics and councillors subfolders are read separately.
            if (body.GetString("ruler_title") is null && body.GetBlock("possible") is null)
            {
                continue;
            }

            results.Add(new GovernmentTypeDefinition(entry.Key, body.GetWeight(loader), order++)
            {
                // Government conditions use ordinary triggers, not the requirements list.
                Possible = requirements.CompileTrigger(body.GetBlock("possible")),
                RulerTitleKey = body.GetString("ruler_title"),
                RulerTitleFemaleKey = body.GetString("ruler_title_female"),
            });
        }

        return results;
    }

    /// <summary>
    /// Whether an authority exists only for the game's own empires, which it marks by requiring a
    /// country type the player can never be.
    /// </summary>
    /// <summary>
    /// The seat an authority's own ruler sits in, by the name the authority gives it.
    /// </summary>
    /// <remarks>
    /// Some of what an authority does is not written in the authority. An imperial ruler's influence
    /// from power projection and their edict fund are in the council seat the authority names
    /// through <c>ruler_council_position</c>, and reading only the authority left those out of the
    /// designer entirely - it showed the leader-pool penalty and none of the compensation.
    /// </remarks>
    private static Dictionary<string, EffectSet> ReadRulerCouncilors(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new Dictionary<string, EffectSet>(StringComparer.Ordinal);

        foreach (var entry in loader.LoadDefinitions("common/governments/councilors"))
        {
            results[entry.Key] = EffectsReader.Read(entry.Body, loader, requirements);
        }

        return results;
    }

    /// <summary>
    /// Folds a ruler's seat into the authority that seats them.
    /// </summary>
    /// <remarks>
    /// One list rather than two, because a player choosing an authority is choosing both and the
    /// game presents them together. Modifiers are summed for the same reason the reader sums them
    /// within an option: the two halves state different things, and where they ever stated the same
    /// thing it would be one number and not the second of them.
    /// </remarks>
    private static EffectSet WithRuler(
        EffectSet authority,
        string? seat,
        Dictionary<string, EffectSet> councilors)
    {
        if (seat is not { Length: > 0 } key || !councilors.TryGetValue(key, out var ruler))
        {
            return authority;
        }

        var modifiers = new Dictionary<string, double>(authority.Modifiers, StringComparer.Ordinal);

        foreach (var (name, value) in ruler.Modifiers)
        {
            modifiers[name] = modifiers.TryGetValue(name, out var already) ? already + value : value;
        }

        return authority with
        {
            Modifiers = modifiers,
            Conditional = [.. authority.Conditional, .. ruler.Conditional],
        };
    }

    private static bool IsAiOnly(CwBlock body)
    {
        var potential = body.GetBlock("potential")?.GetBlock("country_type");
        if (potential is null)
        {
            return false;
        }

        var required = potential.GetStrings("value");
        return required.Count > 0 && !required.Contains("default");
    }

    /// <summary>
    /// Civics gate availability with either a trigger block or, occasionally, a bare trigger name.
    /// </summary>
    private static Requirement ReadPlayable(CwBlock body, RequirementCompiler requirements)
    {
        var node = body.Nodes.FirstOrDefault(n => n.Key == "playable");

        if (node is null)
        {
            return new AlwaysRequirement(true);
        }

        return node.Block is { } block
            ? requirements.CompileTrigger(block)
            : requirements.CompileTriggerByName(node.ScalarValue);
    }

    /// <summary>Traits an authority, civic or origin forces onto the founder species.</summary>
    private static IReadOnlyList<string> ReadForcedTraits(CwBlock body) =>
        ReadTraitList(body.GetBlock("traits"));

    private static IReadOnlyList<string> ReadTraitList(CwBlock? block) =>
        block is null ? [] : block.GetStrings("trait");

    /// <summary>
    /// Works out a civic or origin's icon. Origins state theirs outright, one of them without
    /// quotes. Civics normally follow the naming convention but two dozen override it.
    /// </summary>
    private static string? ResolveIcon(string key, CwBlock body, bool isOrigin, AssetCatalog assets)
    {
        var folder = isOrigin ? "origins" : "civics";
        var destination = $"icons/{folder}/{key}.png";

        // A declared path is used as given; the game's own files are the authority on where an
        // icon lives, and several do not match their key.
        var declared = body.GetString("icon") is { Length: > 0 } path
            ? path.Replace('\\', '/')
            : null;

        return assets.RegisterFirst(
            declared is null
                ? [$"gfx/interface/icons/governments/{folder}/{key}.dds"]
                : [declared, $"gfx/interface/icons/governments/{folder}/{key}.dds"],
            destination);
    }
}
