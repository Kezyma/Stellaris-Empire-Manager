using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads species traits, starting ruler traits and leader traits.</summary>
internal static class TraitsExtractor
{
    /// <summary>
    /// Reads every trait the game defines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no flag distinguishing a species trait from a leader trait, so the kind is worked
    /// out from which fields a definition has: species traits declare the archetypes allowed to
    /// take them, leader traits declare a leader class, and the eleven traits an empire's starting
    /// ruler may take are marked with <c>starting_ruler_trait</c>.
    /// </para>
    /// <para>
    /// Opposites are made symmetric here, because the game's files do not always declare both
    /// directions and the designer has to block the pairing either way round.
    /// </para>
    /// </remarks>
    public static (List<TraitDefinition> Traits, List<LeaderTraitDefinition> Leaders) Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var traits = new List<TraitDefinition>();
        var leaders = new List<LeaderTraitDefinition>();
        var colors = TraitIconComposer.ReadNamedColors(loader);

        foreach (var entry in loader.LoadDefinitions("common/traits"))
        {
            var body = entry.Body;
            var kind = ClassifyTrait(body);

            // The leader traits an empire cannot start with go into a file of their own.
            //
            // Seven hundred and twenty-eight of them, and nothing an empire is designed with holds
            // one: the ruler's picker asks for the starting traits, the validator asks the same, and
            // no empire in the game's own or the player's files carries one. So they stay out of the
            // database, which every visitor fetches before anything can be drawn - and out of its
            // schema, which a desktop player pays for by re-reading the game. The wiki has a page
            // about them and fetches them when somebody opens it.
            if (kind == TraitKind.Leader)
            {
                leaders.Add(LeaderTrait(entry.Key, body, loader, requirements, assets, colors));
                continue;
            }

            traits.Add(new TraitDefinition(entry.Key, kind)
            {
                Cost = body.GetCost(loader),
                AllowedArchetypes = body.GetList("allowed_archetypes"),
                AllowedSpeciesClasses = ReadSpeciesClasses(body),
                PortraitOverride = body.GetList("portrait_override"),
                Opposites = body.GetList("opposites"),
                AllowedPlanetClasses = body.GetList("allowed_planet_classes"),
                AllowedOrigins = body.GetList("allowed_origins"),
                ForbiddenOrigins = body.GetList("forbidden_origins"),
                AllowedEthics = body.GetList("allowed_ethics"),
                ForbiddenEthics = body.GetList("forbidden_ethics"),
                AllowedCivics = body.GetList("allowed_civics"),
                AllowedLeaderClasses = body.GetList("leader_class"),

                // Traits are offered at empire creation unless a definition says otherwise.
                Initial = body.GetBool("initial", defaultValue: true),
                Hidden = body.GetBool("hidden"),
                RequiredDlc = body.GetString("host_has_dlc"),
                Category = body.GetString("category"),

                // A trait's own tags group it for filtering and have no text; the categories it
                // displays are the separate localized_tags field.
                Effects = EffectsReader.Read(
                    body, loader, requirements,
                    tagsKey: "localized_tags",

                    // The one option family whose own documentation says which triggered blocks
                    // are displayed, and expects the rest to speak through a tooltip.
                    hidesTriggeredBlocks: true),

                // A leader trait describes its icon rather than naming one, and is built from that
                // description. The species traits name theirs outright — fifty-three borrow
                // another's, Jinxed wearing trait_jinxed and the Lithoid traits their organic
                // counterparts' — and most say nothing and follow the naming convention instead.
                // What does none of these falls back to the game's own unknown-trait icon.
                Icon = TraitIconComposer.Compose(body, entry.Key, loader, assets, colors)
                    ?? assets.RegisterFirst(
                        [
                            .. Declared(body),
                            $"gfx/interface/icons/traits/{entry.Key}.dds",
                            "gfx/interface/icons/traits/trait_unknown.dds",
                        ],
                        $"icons/traits/{entry.Key}.png"),
            });
        }

        return (ApplySymmetricOpposites(traits), ApplySymmetricOpposites(leaders));
    }

    /// <summary>
    /// One leader trait, as a page about them needs it.
    /// </summary>
    /// <remarks>
    /// The restrictions a species trait carries have no meaning here - a leader has no archetype and
    /// no homeworld - so what is read is the handful that do, plus the four a species trait has no
    /// notion of: which classes may hold it, what sort it is, where in its own chain it sits, and
    /// what it replaces on the way up.
    /// </remarks>
    private static LeaderTraitDefinition LeaderTrait(
        string key,
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        IReadOnlyDictionary<string, (byte R, byte G, byte B, byte A)> colors)
    {
        // The same block the icon is drawn from carries the tier and the rarity, because both are
        // arguments to the recipe that draws it.
        var recipe = body.Nodes
            .FirstOrDefault(n => n.Key == "inline_script" && n.Block?.GetString("ICON") is { Length: > 0 })
            ?.Block;

        return new LeaderTraitDefinition(key)
        {
            LeaderClasses = ReadLeaderClasses(body),
            Sort = body.GetString("leader_trait_type"),
            Rarity = recipe?.GetString("RARITY"),
            Tier = int.TryParse(
                recipe?.GetString("TIER"),
                System.Globalization.CultureInfo.InvariantCulture,
                out var tier)
                ? tier
                : 0,
            Replaces = body.GetList("replace_traits"),
            Opposites = body.GetList("opposites"),
            RequiredDlc = body.GetString("host_has_dlc"),

            // Left as the game states them rather than hidden, unlike the species traits above. A
            // leader trait says almost everything through a triggered block - whether it is on the
            // council, which subclass it belongs to - and hiding those leaves the page blank.
            Effects = EffectsReader.Read(body, loader, requirements, tagsKey: "localized_tags"),

            Icon = TraitIconComposer.Compose(body, key, loader, assets, colors)
                ?? assets.RegisterFirst(
                    [
                        .. Declared(body),
                        $"gfx/interface/icons/traits/{key}.dds",
                        "gfx/interface/icons/traits/trait_unknown.dds",
                    ],
                    $"icons/traits/{key}.png"),
        };
    }

    /// <summary>
    /// Which leader classes a trait names, written either way round.
    /// </summary>
    /// <remarks>
    /// A hundred and thirty-four of them write <c>leader_class = commander</c> as a bare word rather
    /// than a list of one, which reading it as a list alone does not see. The same problem
    /// <see cref="ReadSpeciesClasses"/> already solves for <c>species_class</c>.
    /// </remarks>
    private static IReadOnlyList<string> ReadLeaderClasses(CwBlock body) =>
        body.GetList("leader_class") is { Count: > 0 } listed
            ? listed
            : body.GetString("leader_class") is { Length: > 0 } single ? [single] : [];

    private static TraitKind ClassifyTrait(CwBlock body)
    {
        if (body.GetBool("starting_ruler_trait"))
        {
            return TraitKind.StartingRuler;
        }

        // Leader traits name the classes that can hold them; species traits name archetypes.
        return body.GetBlock("leader_class") is not null || body.GetString("leader_trait_type") is not null
            ? TraitKind.Leader
            : TraitKind.Species;
    }

    /// <summary>
    /// The icon a trait names for itself, if it names one.
    /// </summary>
    /// <remarks>
    /// Galactic Paragons added a layered form — <c>icon = { ... }</c> stacking a frame, a background
    /// and a symbol — used by the unplugged leader traits. There is nothing here that draws layers,
    /// so a block yields nothing and those keep the ordinary fallback.
    /// </remarks>
    private static IEnumerable<string> Declared(CwBlock body) =>
        body.GetBlock("icon") is null && body.GetString("icon") is { Length: > 0 } icon
            ? [icon.Replace('\\', '/')]
            : [];

    /// <summary>
    /// Reads the species classes a trait is limited to. The field is written either as a list or
    /// as a single value, so both are accepted.
    /// </summary>
    private static IReadOnlyList<string> ReadSpeciesClasses(CwBlock body)
    {
        var list = body.GetList("species_class");
        return list.Count > 0
            ? list
            : body.GetString("species_class") is { } single ? [single] : [];
    }

    /// <summary>
    /// Makes exclusions mutual. If A lists B as an opposite but B does not list A, the designer
    /// must still refuse the pairing when the player picks B first.
    /// </summary>
    private static List<TraitDefinition> ApplySymmetricOpposites(List<TraitDefinition> traits)
    {
        var closure = Mutual(traits.Select(t => (t.Key, t.Opposites)));

        return [.. traits.Select(t => closure.TryGetValue(t.Key, out var all)
            ? t with { Opposites = [.. all] }
            : t)];
    }

    /// <summary>
    /// The same for the leader traits, which are their own set.
    /// </summary>
    /// <remarks>
    /// Closed within the leaders rather than across both. The two never name each other - a species
    /// trait's opposites are species traits - and running them together would let a key that happens
    /// to appear in both sets carry an exclusion across.
    /// </remarks>
    private static List<LeaderTraitDefinition> ApplySymmetricOpposites(List<LeaderTraitDefinition> traits)
    {
        var closure = Mutual(traits.Select(t => (t.Key, t.Opposites)));

        return [.. traits.Select(t => closure.TryGetValue(t.Key, out var all)
            ? t with { Opposites = [.. all] }
            : t)];
    }

    /// <summary>Every exclusion, stated from both ends.</summary>
    /// <param name="traits">What each trait says it cannot be held with.</param>
    /// <returns>The closure, by trait key.</returns>
    private static Dictionary<string, SortedSet<string>> Mutual(
        IEnumerable<(string Key, IReadOnlyList<string> Opposites)> traits)
    {
        var closure = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (key, opposites) in traits)
        {
            foreach (var opposite in opposites)
            {
                Pair(key, opposite);
                Pair(opposite, key);
            }
        }

        return closure;

        void Pair(string from, string to)
        {
            if (!closure.TryGetValue(from, out var set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                closure[from] = set;
            }

            set.Add(to);
        }
    }
}
