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
    /// <param name="loader">The script loader.</param>
    /// <param name="requirements">The compiler.</param>
    /// <param name="assets">Where the icons are registered.</param>
    /// <param name="detail">Filled in with what a page about the species traits needs.</param>
    /// <returns>The traits, and the leader traits the wiki keeps its own file of.</returns>
    public static (List<TraitDefinition> Traits, List<LeaderTraitDefinition> Leaders) Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        List<SpeciesTraitDetail> detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var traits = new List<TraitDefinition>();
        var leaders = new List<LeaderTraitDefinition>();
        var colors = TraitIconComposer.ReadNamedColors(loader);

        foreach (var entry in loader.LoadDefinitions("common/traits"))
        {
            var body = entry.Body;
            var kind = ClassifyTrait(body);

            // Every trait a leader can hold goes into the wiki's own file, including the
            // thirty-four an empire may start with.
            //
            // Those thirty-four are not a separate kind of thing: every one of them declares a
            // leader_class like any other leader trait, and carries starting_ruler_trait on top. The
            // classifier below has to answer with one kind and answers with the more specific one,
            // which is right for the database - but reading that as "not a leader trait" is what cut
            // twenty-seven upgrade chains in half, leaving a second and third tier on the page with
            // no first.
            //
            // So a starting trait is written to both: to the database, where the ruler's picker
            // reads it, and to the pack, where the page about leader traits does. The pack is
            // fetched only when somebody opens that page, so the seven hundred and twenty-eight that
            // no empire can hold cost a visitor nothing.
            if (kind is TraitKind.Leader or TraitKind.StartingRuler)
            {
                leaders.Add(LeaderTrait(
                    entry.Key, body, loader, requirements, assets, colors,
                    canStart: kind == TraitKind.StartingRuler));
            }

            if (kind == TraitKind.Leader)
            {
                continue;
            }

            // What a page about them needs and a picker never did: the grouping words, what a pop
            // with it is worth, what it makes its pops pay for, and whether a game can add or take
            // it away later.
            detail.Add(new SpeciesTraitDetail(entry.Key)
            {
                Tags = body.GetList("tags"),
                SlaveCost = loader.ResolveInt(body.GetBlock("slave_cost")?.GetString("trade")),
                CanAddLater = Gate(body, "species_potential_add", requirements),
                CanRemoveLater = Gate(body, "species_possible_remove", requirements),
                ClassOverride = Gate(body, "species_class_override", requirements),
                Resources = Resources(body),
                BoundToWorlds = body.GetList("bound_to_planet_classes"),
                Advanced = body.GetBool("advanced_trait"),
                ImmortalLeaders = body.GetBool("immortal_leaders"),
                Sapient = body.GetBool("sapient", defaultValue: true),
                Infertile = body.GetBool("infertile"),
                ImprovesLeaders = body.GetBool("improves_leaders"),
            });

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
                    hidesTriggeredBlocks: true,

                    // And the ruler an empire starts with is on its council, so a block scoped to
                    // the council is that empire's. Four of the thirty-four write a triggered one
                    // and none of them were read: Mining Rush is written as two of those and
                    // nothing else, so the ruler's picker offered it with no numbers at all.
                    onTheCouncil: kind == TraitKind.StartingRuler),

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
    /// <param name="key">The trait's own key.</param>
    /// <param name="body">What the game declares about it.</param>
    /// <param name="loader">The script, for the values it names rather than writes.</param>
    /// <param name="requirements">How a condition is compiled.</param>
    /// <param name="assets">Where its picture is registered.</param>
    /// <param name="colors">The named colours its icon recipe can call for.</param>
    /// <param name="canStart">Whether an empire may be designed holding this one.</param>
    /// <returns>The trait, as the wiki's own file carries it.</returns>
    private static LeaderTraitDefinition LeaderTrait(
        string key,
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets,
        IReadOnlyDictionary<string, (byte R, byte G, byte B, byte A)> colors,
        bool canStart)
    {
        // The same block the icon is drawn from carries the tier and the rarity, because both are
        // arguments to the recipe that draws it.
        var recipe = body.Nodes
            .FirstOrDefault(n => n.Key == "inline_script" && n.Block?.GetString("ICON") is { Length: > 0 })
            ?.Block;

        return new LeaderTraitDefinition(key)
        {
            CanStart = canStart,
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

            // When a leader can actually be given it, which is the largest thing the page about
            // them was missing: three hundred and one write the block and nothing read it.
            CanBeGiven = body.GetBlock("leader_potential_add") is { } given
                ? requirements.CompilePlanTrigger(given)
                : null,

            Cost = body.GetCost(loader) is var priced and > 0 ? priced : null,
            Randomised = body.GetBool("randomized", defaultValue: true),
            Initial = body.GetBool("initial", defaultValue: true),

            // The fourth value in the inline script the icon, the rarity and the tier already come
            // out of. Yes on 299, no on 435 and "triggered" on 29, and read by nothing until now.
            Council = recipe?.GetString("COUNCIL"),

            ForcedCouncilor = body.GetBool("force_councilor_trait"),
            ImmortalLeaders = body.GetBool("immortal_leaders"),
            Prerequisites = body.GetList("prerequisites"),
            AllowedOrigins = body.GetList("allowed_origins"),
            ForbiddenOrigins = body.GetList("forbidden_origins"),
            AllowedEthics = body.GetList("allowed_ethics"),

            // Read as a leader's rather than as a species trait's. The two state their effects in
            // different words - a leader says what it does while not on the council, or while
            // governing a planet - and read the other way round a hundred and thirteen of them had
            // nothing to show at all.
            Effects = EffectsReader.Read(
                body, loader, requirements, tagsKey: "localized_tags", forLeader: true),

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

            // "all" is the one value that is not a class. It says every class may hold the trait,
            // which is what an empty list already says, and naming it would put a chip on the page
            // for something the game has no badge and no word for.
            : body.GetString("leader_class") is { Length: > 0 } single
                && !string.Equals(single, "all", StringComparison.Ordinal)
                ? [single]
                : [];

    /// <summary>
    /// One of the trait's condition blocks, compiled, or nothing where it states none.
    /// </summary>
    /// <remarks>
    /// Compiled as a plan's condition rather than a design's, because every one of them asks about
    /// a game already under way - whether the species has been gene-tailored, whether the empire has
    /// the technology - and a design cannot answer any of it.
    /// </remarks>
    /// <param name="body">The trait's definition.</param>
    /// <param name="field">Which block.</param>
    /// <param name="requirements">The compiler.</param>
    /// <returns>The condition, or null.</returns>
    private static Requirement? Gate(CwBlock body, string field, RequirementCompiler requirements) =>
        body.GetBlock(field) is { } block ? requirements.CompilePlanTrigger(block) : null;

    /// <summary>
    /// What a trait makes its pops pay for and produce.
    /// </summary>
    /// <remarks>
    /// Named rather than measured - see <see cref="TraitResource"/> for why. Thirty-five traits
    /// declare one and for several it is the whole of what they do: the extractor's effects reader
    /// matches <c>modifier</c> and <c>*_modifier</c> only, so Gaseous Byproducts and Scintillating
    /// Skin reached the page with no numbers and no words.
    /// </remarks>
    /// <param name="body">The trait's definition.</param>
    /// <returns>The resources, which for most traits are none.</returns>
    private static List<TraitResource> Resources(CwBlock body)
    {
        var found = new List<TraitResource>();

        if (body.GetBlock("resources") is not { } resources)
        {
            return found;
        }

        foreach (var node in resources.Nodes)
        {
            if (node.Key is not ("upkeep" or "produces") || node.Block is null)
            {
                continue;
            }

            var upkeep = node.Key == "upkeep";

            foreach (var amount in node.Block.Nodes)
            {
                // Everything in the block that is a plain number is a resource. What is not is the
                // trigger beside them and the multiplier under it, both of which are blocks.
                if (amount.Key is { } resource and not ("trigger" or "mult" or "category") &&
                    amount.Scalar is not null &&
                    !found.Any(f => f.Resource == resource && f.Upkeep == upkeep))
                {
                    found.Add(new TraitResource(resource, upkeep));
                }
            }
        }

        return found;
    }

    private static TraitKind ClassifyTrait(CwBlock body)
    {
        if (body.GetBool("starting_ruler_trait"))
        {
            return TraitKind.StartingRuler;
        }

        // Leader traits name the classes that can hold them; species traits name archetypes.
        //
        // Whether the field is there at all, either way round - not what it names. Asking only for
        // a block missed leader_trait_rift_warped, which writes "leader_class = all" as a bare word
        // and has no leader_trait_type to fall back on, so one leader trait sat in the species list.
        // Asking ReadLeaderClasses instead would miss it again for the opposite reason: "all" is
        // not a class, so that answers with nothing at all.
        return body.GetBlock("leader_class") is not null
            || body.GetString("leader_class") is { Length: > 0 }
            || body.GetString("leader_trait_type") is not null
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
