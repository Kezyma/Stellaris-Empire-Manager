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
    public static List<TraitDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        AssetCatalog assets)
    {
        var traits = new List<TraitDefinition>();
        var colors = TraitIconComposer.ReadNamedColors(loader);

        foreach (var entry in loader.LoadDefinitions("common/traits"))
        {
            var body = entry.Body;
            var kind = ClassifyTrait(body);

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

                // Traits are offered at empire creation unless a definition says otherwise.
                Initial = body.GetBool("initial", defaultValue: true),
                Hidden = body.GetBool("hidden"),
                RequiredDlc = body.GetString("host_has_dlc"),
                Category = body.GetString("category"),
                SortingPriority = loader.ResolveInt(body.GetString("sorting_priority")) ?? 0,
                Tags = body.GetList("tags"),

                // A trait's own tags group it for filtering and have no text; the categories it
                // displays are the separate localized_tags field.
                Effects = EffectsReader.Read(body, loader, requirements, tagsKey: "localized_tags"),

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

        return ApplySymmetricOpposites(traits);
    }

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
        var closure = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var trait in traits)
        {
            foreach (var opposite in trait.Opposites)
            {
                Pair(trait.Key, opposite);
                Pair(opposite, trait.Key);
            }
        }

        return [.. traits.Select(t => closure.TryGetValue(t.Key, out var all)
            ? t with { Opposites = [.. all] }
            : t)];

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
