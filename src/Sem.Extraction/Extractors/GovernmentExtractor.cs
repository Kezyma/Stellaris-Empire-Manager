using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads authorities, civics, origins and government types.</summary>
internal static class GovernmentExtractor
{
    /// <summary>
    /// Modifier keys granting extra species trait points or picks, which several civics and
    /// origins do and which must be applied before a species' trait budget can be checked.
    /// </summary>
    private const string TraitPointsSuffix = "_species_trait_points_add";
    private const string TraitPicksSuffix = "_species_trait_picks_add";

    /// <summary>Reads the government authorities.</summary>
    public static List<AuthorityDefinition> ExtractAuthorities(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<AuthorityDefinition>();

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
                Modifiers = body.GetModifiers("country_modifier", loader),
                Icon = $"icons/authorities/{entry.Key}.png",
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
        RequirementCompiler requirements)
    {
        var results = new List<CivicDefinition>();

        foreach (var entry in loader.LoadDefinitions("common/governments/civics"))
        {
            var body = entry.Body;
            var isOrigin = body.GetBool("is_origin");
            var modifiers = body.GetModifiers("modifier", loader);
            var secondarySpecies = body.GetBlock("has_secondary_species");

            results.Add(new CivicDefinition(entry.Key, isOrigin)
            {
                Playable = ReadPlayable(body, requirements),
                Potential = requirements.CompileRequirementsList(body.GetBlock("potential")),
                Possible = requirements.CompileRequirementsList(body.GetBlock("possible")),
                ForcedTraits = ReadForcedTraits(body),
                SoftTraits = ReadTraitList(body.GetBlock("soft_traits")),
                Modifiers = modifiers,
                TraitBudgetModifiers = ExtractTraitBudgetModifiers(modifiers),
                StartingColony = body.GetString("starting_colony"),
                Initializers = body.GetList("initializers"),
                AddedPlanetClasses = body.GetList("added_planet_types"),
                RemovedPlanetClasses = body.GetList("removed_planet_types"),
                RequiresSecondarySpecies = secondarySpecies is not null,
                SecondarySpeciesTraits = secondarySpecies is null
                    ? []
                    : ReadTraitList(secondarySpecies.GetBlock("traits")),
                EffectsKey = body.GetString("description"),
                PenaltiesKey = body.GetString("negative_description"),
                Icon = ResolveIcon(entry.Key, body, isOrigin),
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
    /// Picks out the modifiers that change how many traits a species may take, so the trait budget
    /// can account for Natural Design, Overtuned and the like.
    /// </summary>
    private static Dictionary<string, double> ExtractTraitBudgetModifiers(
        IReadOnlyDictionary<string, double> modifiers)
    {
        var budget = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (key, value) in modifiers)
        {
            if (key.EndsWith(TraitPointsSuffix, StringComparison.Ordinal) ||
                key.EndsWith(TraitPicksSuffix, StringComparison.Ordinal))
            {
                budget[key] = value;
            }
        }

        return budget;
    }

    /// <summary>
    /// Works out a civic or origin's icon. Origins always state theirs, and one states it without
    /// quotes. Civics normally follow the naming convention but two dozen override it.
    /// </summary>
    private static string ResolveIcon(string key, CwBlock body, bool isOrigin)
    {
        if (body.GetString("icon") is { Length: > 0 } declared)
        {
            var name = Path.GetFileNameWithoutExtension(declared.Replace('\\', '/'));
            return isOrigin ? $"icons/origins/{name}.png" : $"icons/civics/{name}.png";
        }

        return isOrigin ? $"icons/origins/{key}.png" : $"icons/civics/{key}.png";
    }
}
