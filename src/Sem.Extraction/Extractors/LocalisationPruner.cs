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

    /// <summary>
    /// The words the game's own empire creation screen puts on its fields.
    /// </summary>
    /// <remarks>
    /// Taken from the <c>text = </c> attributes in <c>interface/customize_species_editors.gui</c>
    /// and <c>interface/game_setup</c>, so a label reads as the game words it rather than as
    /// whatever English seemed reasonable — and reads in the player's language when they have one.
    /// </remarks>
    internal static readonly string[] DesignerLabels =
    [
        "EMPIRE_NAME", "EMPIRE_ADJECTIVE", "SHIP_PREFIX",
        "SPECIES_NAME", "SPECIES_PLURAL", "SPECIES_ADJECTIVE", "SPECIES_CLASS_LABEL",
        "NAME_LIST", "PORTRAIT", "GENDER",
        "TRAITS", "TRAIT_POINTS", "POINTS_LEFT", "PICKS_LEFT",
        "ETHICS", "POINTS_LEFT_ETHICS",
        "GOVERNMENT_AUTHORITY_AND_TYPE", "GOVERNMENT_LABEL", "CIVICS_LABEL", "POINTS_LEFT_CIVICS",
        "ORIGIN",
        "HOMEWORLD_CLASS_LABEL", "HOMEWORLD_NAME",
        "SELECT_SYSTEM_INITIALIZER_LABEL", "SYSTEM_NAME",
        "RANDOM_FRONTEND_NAME", "random_system_initializer_DESC",
        "EMPIRE_ADVISOR", "EMPIRE_CREATION_ROOM_APPEARANCE", "EMPIRE_CREATION_CITY_APPEARANCE", "SHIPSETS_LABEL",

        // The advisor's other mode. The game frames the voice as a choice between letting the empire
        // pick one and naming one, and a design that names none is the first of those - so the words
        // for it are needed as much as any voice's name.
        "SETTINGS_VOICE_TYPE_auto_advisor_voice_type",
        "SETTINGS_VOICE_TYPE_auto_advisor_voice_type_DESC",
        "SHIPSET_MECHANICAL", "SHIPSET_BIOLOGICAL",
        "EMPIRE_FLAG", "CHOOSE_SYMBOL", "EMBLEM_BACKGROUND_PATTERN",
        "PRIMARY_COLOR", "SECONDARY_COLOR", "TERTIARY_COLOR",
        // The game words its four title boxes as a pair of headings over a pair of genders: "Ruler
        // Title" and "Heir Title" each over "Male" and "Female".
        "RULER_CLASS", "RULER_TITLE", "HEIR_TITLE",
        "RULER_TITLE_MALE", "RULER_TITLE_FEMALE", "HEIR_TITLE_MALE", "HEIR_TITLE_FEMALE",
        "LEADER_NAME",

        // What the game's own empire list puts on its third line, under the government.
        "SPECIES_CLASS_LABEL",
        "IS_NOMADIC",
        "SUMMARY_EMPIRE_MODIFIERS",

        // The words on the ruler's appearance, which the game's own leader editor uses.
        // EVOLUTION_VARIANT is deliberately absent: the game offers two controls for it and a design
        // holds one number, decided in play, so there is nothing here to label.
        "LEADER_SUB_PORTRAIT", "VARIATION", "ATTACHMENTS", "CLOTHES",
        "EVOLUTION_STAGE", "CHOOSE_SEX",

        // What a portrait may call its attachment instead, by its custom_attachment_label.
        "HAIR_STYLE", "HAT", "MASK",

        // The box a player writes an empire's or a ruler's own story in.
        "BIOGRAPHY",

        // The three states of the spawn button, and the sentence each shows on hovering.
        "EMPIRE_SPAWN_ALLOWED", "EMPIRE_SPAWN_ALLOWED_DESC",
        "EMPIRE_SPAWN_DISALLOWED", "EMPIRE_SPAWN_DISALLOWED_DESC",
        "EMPIRE_SPAWN_ALWAYS", "EMPIRE_SPAWN_ALWAYS_DESC",
    ];

    /// <summary>Keeps only the entries the database can reach, following references between them.</summary>
    /// <param name="alwaysKept">
    /// Entries to keep whatever refers to them — the name lists and the name-system formats, which a
    /// player's own empire may use and no part of the game's own content points at.
    /// </param>
    public static Dictionary<string, string> Prune(
        GameDatabase database,
        IReadOnlyDictionary<string, string> all,
        IReadOnlySet<string>? alwaysKept = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(all);

        var wanted = new HashSet<string>(StringComparer.Ordinal);
        CollectSeeds(database, wanted);

        foreach (var key in alwaysKept ?? (IReadOnlySet<string>)new HashSet<string>())
        {
            wanted.Add(key);
        }

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
                foreach (var referenced in FindReferences(value, database.ScriptedText))
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

        // Every word an empire name can be built from. These look like words and are keys: the game
        // writes "Mercantile_Union" and shows "Mercantile Union", "All_Consuming" and shows
        // "All-Consuming", "StarCorp" and shows "StarCorp" unchanged. Nothing else in the data
        // refers to them, so without this the whole 1,774 were cut and the designer offered the
        // tokens themselves.
        foreach (var word in database.EmpireNameParts.SelectMany(list => list.Parts))
        {
            Add(word.Word);
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

        // The designer's own furniture: every label the game's empire creation screen puts on a
        // field, so this one reads as the game reads and translates with it. Named one at a time
        // because nothing in the database refers to them, and anything unreferenced is pruned.
        foreach (var label in DesignerLabels)
        {
            Add(label);
        }

        // A ready-made species is stored in a design by its key, so the key has to be readable.
        foreach (var species in database.SpeciesNames)
        {
            Add(species.NameKey);
            Add(species.PluralKey);
            Add(species.HomePlanetKey);
            Add(species.HomeSystemKey);
        }

        foreach (var voice in database.AdvisorVoices)
        {
            Add(voice.NameKey);
            AddRequirement(voice.Playable);
        }

        foreach (var group in database.ShipSets)
        {
            Add(group.NameKey);
        }

        foreach (var leaderClass in database.LeaderClasses)
        {
            Add(leaderClass.NameKey);
        }

        foreach (var culture in database.GraphicalCultures)
        {
            Add(culture.NameKey);
            Add(culture.DescriptionKey);
            AddRequirement(culture.Selectable);
        }

        foreach (var category in database.FlagCategories)
        {
            Add(category.NameKey);
        }

        // An arkship's name is built from two other entries — a class word and the word "Arkship" —
        // so both have to travel with it. The reference-following pass finds them from the name.
        //
        // The description is the whole of what the panel says about an arkship: the game writes the
        // bonus list by hand rather than generating it, and this one key expands to the shared
        // effects, the arkship's own modifiers and the prose about it. Left out, the picker had
        // three cards with a name apiece and nothing to choose between them.
        foreach (var arkship in database.Arkships)
        {
            Add(arkship.NameKey);
            Add(arkship.DescriptionKey);
        }

        foreach (var empire in database.PrescriptedEmpires)
        {
            Add(empire.NameKey);
            Add($"{empire.NameKey}_desc");
            AddRequirement(empire.Playable);

            // Everything the empire itself names — its species, its ship prefix, its ruler. These
            // are keys the design holds rather than anything the database refers to, so without
            // reading them out of the design they are pruned and the empire reads as its own keys.
            foreach (Match match in StoredKey().Matches(empire.Design ?? string.Empty))
            {
                Add(match.Groups[1].Value);
            }
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
            // A blocked option shows the explanation of whichever condition turned it down, and a
            // condition nested three deep is as able to be the one that did.
            foreach (var nested in requirement.AndNested())
            {
                Add(nested.FailureText);
            }
        }
    }

    /// <summary>
    /// Finds the entries a displayed string refers to: <c>$variables$</c> the game substitutes,
    /// and bracketed references such as <c>['trait:trait_adaptive']</c> that render another entry's
    /// name inline.
    /// </summary>
    private static IEnumerable<string> FindReferences(
        string value,
        IReadOnlyDictionary<string, string> scriptedText)
    {
        foreach (Match match in VariableReference().Matches(value))
        {
            yield return match.Groups[1].Value;
        }

        foreach (Match match in ScopedReference().Matches(value))
        {
            yield return match.Groups[1].Value;
        }

        // A call into the game's script shows its default branch at design time, and that branch
        // is an entry like any other — dropped, the sentence around it stops mid-phrase.
        foreach (Match match in ScriptedCall().Matches(value))
        {
            var path = match.Groups[1].Value.Split('.');

            if (scriptedText.GetValueOrDefault(path[^1]) is { Length: > 0 } key)
            {
                yield return key;
            }

            // A call on a scope, where the scope is a job: [bureaucrat.GetNamePlural] is that job's
            // plural name, and the name lives under a key built from the job's own. Kept whether or
            // not this installation has the job, since asking for an entry that is not there costs
            // nothing and leaving one out shows a modifier with no subject.
            if (path.Length > 1)
            {
                yield return $"job_{path[0]}";
                yield return $"job_{path[0]}_plural";
            }
        }
    }

    /// <summary>Matches <c>$KEY$</c> and <c>$KEY|format$</c>.</summary>
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_.\-]*)(?:\|[^$]*)?\$")]
    private static partial Regex VariableReference();

    /// <summary>
    /// Matches <c>[Scope.Name]</c> and the bare <c>[Name]</c>, capturing the scripted phrase.
    /// </summary>
    /// <remarks>
    /// The scope is optional, and has to be: the bare form is the commoner one in what a designer
    /// shows. Requiring it meant the entries those phrases resolve to were never followed and so
    /// never kept, which is half of why they showed as raw script.
    ///
    /// The whole path is captured, since where there is a scope it is usually a job and the job is
    /// the only thing that says which name to keep.
    /// </remarks>
    [GeneratedRegex(@"\[([A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*)\]")]
    private static partial Regex ScriptedCall();

    /// <summary>
    /// Matches <c>['scope:key']</c> and the bare <c>['key']</c>, capturing the key.
    /// </summary>
    /// <remarks>
    /// Space after the bracket allowed, because the game's own text has it in places — the riftworld
    /// origin writes <c>[ 'concept_astral_rift'</c> — and a key not matched here is a key not kept.
    /// </remarks>
    [GeneratedRegex(@"\[\s*'(?:[a-z_]+:)?([A-Za-z0-9_.]+)'")]
    private static partial Regex ScopedReference();

    /// <summary>
    /// Matches a name a design stores by key, as <c>key="SPEC_Oxanalytor"</c>.
    /// </summary>
    /// <remarks>
    /// The percent-wrapped templates are skipped: the engine builds those and the game's text has no
    /// entry for any of them.
    /// </remarks>
    [GeneratedRegex(@"key=""([A-Za-z_][A-Za-z0-9_.\-]*)""")]
    private static partial Regex StoredKey();
}
