using System.Text.RegularExpressions;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Cuts the game's localisation down to the text the designer can actually display.
/// </summary>
/// <remarks>
/// English alone runs to about 150,000 entries and fifteen megabytes, most of it event and dialogue
/// text no empire designer will ever show. The web build has to fetch this over the network, so it
/// ships only what is reachable: the names and descriptions of things the designer lists, the
/// explanations attached to blocked options, and whatever those texts refer to in turn.
/// </remarks>
internal static partial class LocalisationPruner
{
    /// <summary>How far to follow references between entries before giving up.</summary>
    private const int MaxExpansionRounds = 8;

    /// <summary>Keeps only the entries the database can reach, following references between them.</summary>
    public static Dictionary<string, string> Prune(
        GameDatabase database,
        IReadOnlyDictionary<string, string> all)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(all);

        var wanted = new HashSet<string>(StringComparer.Ordinal);
        CollectSeeds(database, wanted);

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        var frontier = wanted;

        for (var round = 0; round < MaxExpansionRounds && frontier.Count > 0; round++)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);

            foreach (var key in frontier)
            {
                if (!all.TryGetValue(key, out var value) || !kept.TryAdd(key, value))
                {
                    continue;
                }

                // A displayed string can name other entries, and those have to travel with it or
                // the player sees a raw key where a word should be.
                foreach (var referenced in FindReferences(value))
                {
                    if (!kept.ContainsKey(referenced))
                    {
                        next.Add(referenced);
                    }
                }
            }

            frontier = next;
        }

        return kept;
    }

    private static void CollectSeeds(GameDatabase database, HashSet<string> wanted)
    {
        foreach (var archetype in database.Archetypes)
        {
            Add(archetype.NameKey);
        }

        foreach (var speciesClass in database.SpeciesClasses)
        {
            Add(speciesClass.NameKey);

            // These carry the explanations for why a class is unavailable, such as needing gestalt
            // consciousness, which the designer shows on the blocked option.
            AddRequirement(speciesClass.Playable);
            AddRequirement(speciesClass.Possible);
            AddRequirement(speciesClass.PossibleSecondary);
        }

        foreach (var trait in database.Traits)
        {
            Add(trait.NameKey);
            Add(trait.DescriptionKey);
            AddEffects(trait.Effects);
        }

        foreach (var ethic in database.Ethics)
        {
            Add(ethic.NameKey);
            Add(ethic.DescriptionKey);
            AddEffects(ethic.Effects);
        }

        foreach (var authority in database.Authorities)
        {
            Add(authority.NameKey);
            Add(authority.DescriptionKey);
            AddRequirement(authority.Possible);
            AddRequirement(authority.Playable);
            AddEffects(authority.Effects);
        }

        foreach (var civic in database.Civics)
        {
            Add(civic.NameKey);
            Add(civic.EffectsKey);
            Add(civic.PenaltiesKey);
            Add($"{civic.Key}_desc");
            AddRequirement(civic.Potential);
            AddRequirement(civic.Possible);
            AddRequirement(civic.Playable);
            AddEffects(civic.Effects);
        }

        foreach (var government in database.GovernmentTypes)
        {
            Add(government.NameKey);
            Add(government.RulerTitleKey);
            Add(government.RulerTitleFemaleKey);
            AddRequirement(government.Possible);
        }

        foreach (var planet in database.PlanetClasses)
        {
            Add(planet.NameKey);
            AddRequirement(planet.Potential);
        }

        foreach (var category in database.PortraitCategories)
        {
            Add(category.NameKey);
        }

        foreach (var set in database.PortraitSets)
        {
            foreach (var portrait in set.Portraits)
            {
                AddRequirement(portrait.Playable);
            }
        }

        foreach (var portrait in database.Portraits)
        {
            Add(portrait.NameKey);
        }

        foreach (var nameList in database.NameLists)
        {
            Add(nameList.NameKey);
            AddRequirement(nameList.Selectable);
        }

        foreach (var initializer in database.Initializers)
        {
            Add(initializer.NameKey);
            Add(initializer.DescriptionKey);
        }

        // The blank choice in the starting-system list is the game's own Random, and it has a name
        // and a description like the rest even though no initializer carries them.
        Add("RANDOM_FRONTEND_NAME");
        Add("random_system_initializer_DESC");
        Add("SELECT_SYSTEM_INITIALIZER_LABEL");
        Add("SYSTEM_NAME");
        Add("IS_NOMADIC");

        foreach (var voice in database.AdvisorVoices)
        {
            Add(voice.NameKey);
            AddRequirement(voice.Playable);
        }

        foreach (var culture in database.GraphicalCultures)
        {
            Add(culture.NameKey);
            AddRequirement(culture.Selectable);
        }

        foreach (var category in database.FlagCategories)
        {
            Add(category.NameKey);
        }

        foreach (var empire in database.PrescriptedEmpires)
        {
            Add(empire.NameKey);
            AddRequirement(empire.Playable);
        }

        foreach (var pack in database.Dlc)
        {
            Add(pack.NameKey);
        }

        // Labels the designer's own interface borrows from the game.
        foreach (var key in (string[])
        [
            "EMPIRE_CREATION_ROOM_APPEARANCE",
            "EMPIRE_CREATION_CITY_APPEARANCE",
            "ROOM_BACKGROUND",
            "CITY_GRAPHICS",
        ])
        {
            Add(key);
        }

        void Add(string? key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                wanted.Add(key);
            }
        }

        void AddModifiers(IReadOnlyDictionary<string, double> modifiers)
        {
            foreach (var key in modifiers.Keys)
            {
                // A modifier's label is written one of three ways and the three barely overlap: a
                // lowercase prefixed key, an uppercase one, or the modifier's own name with no
                // prefix at all. Dropping any of them leaves effects lines with no label.
                Add($"mod_{key}");
                Add($"mod_{key}_desc");
                Add($"MOD_{key.ToUpperInvariant()}");
                Add($"MOD_{key.ToUpperInvariant()}_DESC");
                Add(key);
                Add($"{key}_tt");
            }
        }

        void AddEffects(EffectSet effects)
        {
            AddModifiers(effects.Modifiers);

            foreach (var conditional in effects.Conditional)
            {
                AddModifiers(conditional.Modifiers);
                AddRequirement(conditional.When);
            }

            foreach (var tag in effects.TagKeys)
            {
                Add(tag);
            }

            Add(effects.DescriptionKey);
            Add(effects.PenaltyKey);
            Add(effects.TooltipKey);
        }

        void AddRequirement(Requirement requirement)
        {
            Add(requirement.FailureText);

            switch (requirement)
            {
                case AllRequirement all:
                    foreach (var item in all.Items)
                    {
                        AddRequirement(item);
                    }

                    break;

                case AnyRequirement any:
                    foreach (var item in any.Items)
                    {
                        AddRequirement(item);
                    }

                    break;

                case NotRequirement not:
                    AddRequirement(not.Item);
                    break;
            }
        }
    }

    /// <summary>
    /// Finds the entries a displayed string refers to: <c>$variables$</c> the game substitutes,
    /// and bracketed references such as <c>['trait:trait_adaptive']</c> that render another entry's
    /// name inline.
    /// </summary>
    private static IEnumerable<string> FindReferences(string value)
    {
        foreach (Match match in VariableReference().Matches(value))
        {
            yield return match.Groups[1].Value;
        }

        foreach (Match match in ScopedReference().Matches(value))
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>Matches <c>$KEY$</c> and <c>$KEY|format$</c>.</summary>
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_.]*)(?:\|[^$]*)?\$")]
    private static partial Regex VariableReference();

    /// <summary>Matches <c>['scope:key']</c>, capturing the key.</summary>
    [GeneratedRegex(@"\['[a-z_]+:([A-Za-z0-9_.]+)'\]")]
    private static partial Regex ScopedReference();
}
