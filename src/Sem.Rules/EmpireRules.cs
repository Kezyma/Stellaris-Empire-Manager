using Sem.Designs;
using Sem.GameData;

namespace Sem.Rules;

/// <summary>
/// Enforces the game's rules on an empire design: what may be chosen, what it costs, and why
/// something is unavailable.
/// </summary>
/// <remarks>
/// Everything here is a pure function of a <see cref="DesignContext"/>, so a change to any
/// selection is handled by rebuilding the context rather than by keeping state in step.
/// </remarks>
public sealed class EmpireRules(GameDatabase database)
{
    private readonly GameDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly RequirementEvaluator _evaluator = new();

    // All four indexed the same way: last definition wins, which is the game's own load order and
    // what the extractor already applies. Three of them used to build the dictionary directly and
    // threw on a repeated key — inside a field initialiser, so a database with one duplicate in it
    // brought the app down at start rather than being read the way the game reads it. Only traits
    // were defended, which said the asymmetry was an oversight rather than a decision.
    private readonly Dictionary<string, TraitDefinition> _traits = Index(database.Traits, t => t.Key);

    private readonly Dictionary<string, EthicDefinition> _ethics = Index(database.Ethics, e => e.Key);

    private readonly Dictionary<string, CivicDefinition> _civics = Index(database.Civics, c => c.Key);

    private readonly Dictionary<string, ArchetypeDefinition> _archetypes =
        Index(database.Archetypes, a => a.Key);

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key) =>
        items.GroupBy(key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    /// <summary>The extracted game data being enforced.</summary>
    public GameDatabase Database => _database;

    /// <summary>Builds a context from a design.</summary>
    public DesignContext CreateContext(EmpireDesign design, IReadOnlySet<string>? ownedDlc = null)
    {
        var context = DesignContext.FromDesign(design, _database, ownedDlc);

        // Worked out here rather than inside the context, which has no rules of its own: the
        // government follows from the authority, the ethics and the civics, and deriving it needs
        // every government's conditions read against the empire. It stays null while that is
        // happening, so a government whose own condition asked after the government would be told
        // there is not one yet rather than being asked again for ever.
        context.Government = DeriveGovernment(context)?.Key;

        // The traits it really has, once the ones nothing writes down are added. Forced traits are
        // worked out against the finished context, so this follows the government rather than
        // sharing a line with it.
        context.EffectiveTraits = new HashSet<string>(
            context.Traits.Concat(GetForcedTraits(context)),
            StringComparer.Ordinal);

        return context;
    }

    // ---------------------------------------------------------------------------------------
    // Budgets
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// What the founder species may spend on traits, after the civics and origin that change the
    /// allowance.
    /// </summary>
    public TraitBudget GetTraitBudget(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var archetype = context.SpeciesArchetype is { } key && _archetypes.TryGetValue(key, out var found)
            ? found
            : null;

        var points = archetype?.TraitPoints ?? 0;
        var picks = archetype?.MaxTraits ?? 0;

        if (context.SpeciesArchetype is { } archetypeKey)
        {
            // Natural Design, Overtuned, Shroud-Forged and Unplugged all widen the allowance, and
            // the budget is wrong before they are applied.
            //
            // Read from the option's own effects rather than from a list of just its always-on
            // modifiers, because a civic may state the bonus inside a swap. The hive mind's Innate
            // Design states it in nothing else: both its swaps grant two points and two picks and it
            // has no top-level modifier block at all, so a budget built from the always-on numbers
            // came to zero. Going through the same filter as the modifier panel also settles which
            // of two mutually exclusive swaps applies, instead of summing both.
            var pointsKey = $"{archetypeKey}_species_trait_points_add";
            var picksKey = $"{archetypeKey}_species_trait_picks_add";

            foreach (var civic in SelectedCivicsAndOrigin(context))
            {
                foreach (var (modifier, value) in DesignEffects.Applying(civic.Effects, context))
                {
                    if (string.Equals(modifier, pointsKey, StringComparison.Ordinal))
                    {
                        points += (int)value;
                    }
                    else if (string.Equals(modifier, picksKey, StringComparison.Ordinal))
                    {
                        picks += (int)value;
                    }
                }
            }
        }

        // Only what the design records, which is also what the game charges. Traits an origin forces
        // are shown separately and deliberately not added here: of the twenty-two the game forces,
        // nineteen cost nothing, and the three that do — Malleable Genes at six, Aquatic at two and
        // Syncretic Proles at one — are written into the design's own trait list by the game, so
        // they arrive through this loop already. Adding the forced list on top would charge them
        // twice.
        var spent = 0;
        var used = 0;

        foreach (var trait in context.Traits)
        {
            if (!_traits.TryGetValue(trait, out var definition))
            {
                continue;
            }

            spent += definition.Cost;

            // A trait costing nothing, such as the one a species class carries, is not a pick.
            if (definition.Cost != 0)
            {
                used++;
            }
        }

        return new TraitBudget(new Budget(spent, points), new Budget(used, picks));
    }

    /// <summary>What the empire has spent on ethics against the three points it has.</summary>
    public Budget GetEthicsBudget(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var spent = context.Ethics.Sum(e => _ethics.TryGetValue(e, out var ethic) ? ethic.Cost : 0);
        return new Budget(spent, _database.Defines.EthicsPoints);
    }

    /// <summary>
    /// How many civics the empire has taken against how many it may.
    /// </summary>
    /// <remarks>
    /// The define and nothing else. Five civics carry
    /// <c>country_government_civic_points_add</c>, and none of them can be reached while designing:
    /// Fanatic Purifiers grants it inside a swap gated on a country flag, and the other four have a
    /// <c>potential</c> requiring the empire to hold the civic already — the game's own comment
    /// beside it says the trigger is there to stop anyone picking it, because an event adds it. So
    /// there is nothing to sum, and this says so rather than looking like an omission.
    /// </remarks>
    public Budget GetCivicsBudget(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Budget(context.Civics.Count, _database.Defines.CivicPoints);
    }

    // ---------------------------------------------------------------------------------------
    // Derived values
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The personalities the game could give this empire, likeliest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list rather than one answer, which is where this parts company with the government. The
    /// government is the single highest-weighted type that fits; a personality is drawn at random
    /// from every one that fits, weighted - and the conditions overlap heavily on ethics, so an
    /// empire usually fits several. Saying which one it "will" get would be the app settling
    /// something the game rolls.
    /// </para>
    /// <para>
    /// The weights are added rather than multiplied, which the game states at the top of its own
    /// file. A share is that total against the total of everything else allowed, so the shares of
    /// what comes back always add to one.
    /// </para>
    /// <para>
    /// Read against the design as it stands, plan and all left out - which is the caller's business
    /// and is what a plain context already is. A personality is given when the empire first appears,
    /// long before any of the plan has happened. Two of them ask for an ascension perk and weigh ten
    /// thousand apiece, so a context carrying a plan would have had them bury everything else.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PersonalityChance> DerivePersonalities(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var allowed = new List<(PersonalityDefinition Personality, double Weight)>();

        foreach (var personality in _database.Personalities)
        {
            if (!_evaluator.IsSatisfied(personality.Allow, context))
            {
                continue;
            }

            var weight = personality.Additions
                .Where(a => _evaluator.IsSatisfied(a.When, context))
                .Aggregate(personality.Weight, (running, a) => running + a.Factor);

            // A personality the game would never draw is not one this empire might be given.
            if (weight > 0)
            {
                allowed.Add((personality, weight));
            }
        }

        var total = allowed.Sum(a => a.Weight);

        if (total <= 0)
        {
            return [];
        }

        return
        [
            .. allowed
                .OrderByDescending(a => a.Weight)
                .ThenBy(a => a.Personality.FileOrder)
                .Select(a => new PersonalityChance(a.Personality, a.Weight, a.Weight / total))
        ];
    }

    /// <summary>
    /// Works out what the empire's government is called.
    /// </summary>
    /// <remarks>
    /// The government is not chosen. The game takes the highest-weighted type whose conditions the
    /// design meets, and settles ties by which was defined first.
    ///
    /// The weight is not always the number written at the top of the block. Thirteen governments
    /// double their own against a civic - Star Empire for Distinguished Admiralty, Holy Tribunal for
    /// Exalted Priesthood - and reading only the base had those thirteen arguing their case at half
    /// strength.
    /// </remarks>
    public GovernmentTypeDefinition? DeriveGovernment(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GovernmentTypeDefinition? best = null;
        var bestWeight = double.NegativeInfinity;

        foreach (var government in _database.GovernmentTypes)
        {
            if (!_evaluator.IsSatisfied(government.Possible, context))
            {
                continue;
            }

            var weight = WeightOf(government, context);

            if (best is null ||
                weight > bestWeight ||
                (weight == bestWeight && government.FileOrder < best.FileOrder))
            {
                best = government;
                bestWeight = weight;
            }
        }

        return best;
    }

    /// <summary>What a government weighs for this design, once its own conditions are applied.</summary>
    private double WeightOf(GovernmentTypeDefinition government, DesignContext context) =>
        government.Factors.Count == 0
            ? government.Weight
            : government.Factors
                .Where(f => _evaluator.IsSatisfied(f.When, context))
                .Aggregate(government.Weight, (running, f) => running * f.Factor);

    /// <summary>The world a nomadic empire begins on, which is its ship.</summary>
    private const string Arkship = "pc_ark";

    private bool HasPlanetClass(string key) =>
        _database.PlanetClasses.Any(p => string.Equals(p.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The homeworld types this empire may start on.
    /// </summary>
    /// <remarks>
    /// Normally the classes the game flags as starting worlds, but an origin can replace that
    /// outright, which is how Void Dwellers begin on a habitat, and civics, origins and species
    /// classes can each add or remove types. A nomadic empire has no world at all and begins on its
    /// ship.
    /// </remarks>
    public IReadOnlyList<string> GetHomeworldOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A nomadic empire lives aboard an arkship, whatever else it chose. Nothing in the script
        // says so — the arkship class is marked as no starting planet, and no origin or civic names
        // it — but the game's own nomadic empires all begin there, and the toggle is what puts them
        // there.
        if (context.IsNomadic && HasPlanetClass(Arkship))
        {
            return [Arkship];
        }

        return GetSettledHomeworldOptions(context);
    }

    /// <summary>
    /// The worlds this empire could start on if it stayed still.
    /// </summary>
    /// <remarks>
    /// The same question asked without the nomad toggle, which is what the toggle itself needs when
    /// it is turned off: something has to go back where the arkship was, and the arkship is the only
    /// answer the ordinary list will give while the design still says nomadic.
    /// </remarks>
    public IReadOnlyList<string> GetSettledHomeworldOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // An origin that supplies its own world leaves nothing to choose.
        if (OriginOf(context) is { } chosen &&
            (chosen.HabitabilityPreference ?? chosen.StartingColony) is { Length: > 0 } forced)
        {
            return [forced];
        }

        var candidates = new List<string>();

        foreach (var planet in _database.PlanetClasses)
        {
            if (planet.IsStartingWorld && _evaluator.IsSatisfied(planet.Potential, context))
            {
                candidates.Add(planet.Key);
            }
        }

        foreach (var added in SelectedCivicsAndOrigin(context).SelectMany(c => c.AddedPlanetClasses)
                     .Concat(SpeciesClassOf(context)?.AddedPlanetClasses ?? []))
        {
            if (!candidates.Contains(added, StringComparer.Ordinal))
            {
                candidates.Add(added);
            }
        }

        foreach (var removed in SelectedCivicsAndOrigin(context).SelectMany(c => c.RemovedPlanetClasses)
                     .Concat(SpeciesClassOf(context)?.RemovedPlanetClasses ?? []))
        {
            candidates.RemoveAll(c => string.Equals(c, removed, StringComparison.Ordinal));
        }

        return candidates;
    }

    /// <summary>
    /// The starting systems this empire may use: those an origin names, or the ones open to any
    /// custom empire.
    /// </summary>
    public IReadOnlyList<string> GetStartingSystemOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (OriginOf(context) is { Initializers.Count: > 0 } origin)
        {
            return origin.Initializers;
        }

        return [.. _database.Initializers
            .Where(i => i.Usage == InitializerUsage.CustomEmpire)
            .Select(i => i.Key)];
    }

    /// <summary>Traits the design forces onto the founder species and the player cannot remove.</summary>
    public IReadOnlyList<string> GetForcedTraits(DesignContext context) =>
        [.. GetForcedTraitSources(context).Select(f => f.Trait).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The forced traits a design has to carry in the file, which is not all of them.
    /// </summary>
    /// <remarks>
    /// A habitability preference is forced by the homeworld and is never written down: not one of
    /// the fifty-two empires the game ships carries a <c>trait_pc_*_preference</c>, because the game
    /// works it out from the planet class as it reads the design. Everything else is written and is
    /// checked - the species class's Organic or Lithoid, an authority's Hive Mind, whatever a civic
    /// or an origin imposes.
    ///
    /// Told apart by source rather than by the trait's name, because the distinction is where the
    /// trait came from and not what it is called.
    /// </remarks>
    public IReadOnlyList<string> GetWrittenForcedTraits(DesignContext context) =>
    [
        .. GetForcedTraitSources(context)
            .Where(f => f.Source != ForcedTraitSource.Homeworld)
            .Select(f => f.Trait)
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// The same traits, each with whatever put it there.
    /// </summary>
    /// <remarks>
    /// A player told only that a trait is "fixed by the species class, authority, civics or origin"
    /// is being asked to work out which, from a list of four things any of which might be to blame.
    /// The answer is known here, where the list is built, and costs nothing to carry out.
    /// </remarks>
    public IReadOnlyList<ForcedTrait> GetForcedTraitSources(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var forced = new List<ForcedTrait>();

        if (SpeciesClassOf(context) is { ForcedTrait: { Length: > 0 } classTrait } speciesClass)
        {
            forced.Add(new ForcedTrait(classTrait, speciesClass.Key, ForcedTraitSource.SpeciesClass));
        }

        if (context.Authority is { } authorityKey)
        {
            var authority = _database.Authorities
                .FirstOrDefault(a => string.Equals(a.Key, authorityKey, StringComparison.Ordinal));

            forced.AddRange((authority?.ForcedTraits ?? [])
                .Select(t => new ForcedTrait(t, authorityKey, ForcedTraitSource.Authority)));
        }

        foreach (var civic in SelectedCivicsAndOrigin(context))
        {
            var kind = civic.IsOrigin ? ForcedTraitSource.Origin : ForcedTraitSource.Civic;

            // An origin that calls for two species names a trait for each. Syncretic Evolution makes
            // its founders Intelligent and its subjects Proles, and giving the second species the
            // first one's trait would be the wrong answer rather than a missing one.
            //
            // The soft ones are forced here too, which reads oddly beside the field's own name. The
            // game's comment - "can be removed without making the government invalid" - is about the
            // game, where a species may drop a trait later; it is not about the designer, which is
            // the only thing this app writes. Teachers of the Shroud shows Latent Psionic greyed at
            // the top of the chosen traits and will not let it go, exactly as it shows the rest.
            IReadOnlyList<string> traits = civic.IsOrigin && context.IsSecondarySpecies
                ? civic.SecondarySpeciesTraits
                : [.. civic.ForcedTraits, .. civic.SoftTraits];

            forced.AddRange(traits.Select(t => new ForcedTrait(t, civic.Key, kind)));
        }

        if (HabitabilityTraitFor(context) is { Length: > 0 } preference)
        {
            forced.Add(new ForcedTrait(preference, context.EffectivePlanetClass, ForcedTraitSource.Homeworld));
        }

        // First claim wins, so a trait held for two reasons names the more specific of them.
        return [.. forced
            .GroupBy(f => f.Trait, StringComparer.Ordinal)
            .Select(g => g.First())];
    }

    /// <summary>
    /// The habitability preference the homeworld gives the species.
    /// </summary>
    /// <remarks>
    /// A species does not choose what it is suited to; the world it evolved on decides. The game
    /// names these traits after the thing that grants them — <c>trait_pc_continental_preference</c>
    /// for a planet class, or <c>trait_auto_wet_preference</c> for a climate where several classes
    /// share one — so the trait is found by name rather than by a list that would need maintaining.
    /// </remarks>
    public string? HabitabilityTraitFor(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return HabitabilityTraitFor(context.EffectivePlanetClass, context.SpeciesArchetype);
    }

    /// <summary>
    /// The habitability preference a given world would give, whether or not it is the one chosen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of every world in the picker, so that hovering one says what living there does to a
    /// species — which is the only thing a planet class has to say for itself. The game writes no
    /// description for them at all.
    /// </para>
    /// <para>
    /// The archetype decides between traits that suit the same world, and leaving it out was wrong:
    /// <c>trait_pc_desert_preference</c> is <c>allowed_archetypes = { BIOLOGICAL PRESAPIENT LITHOID
    /// }</c>, so a machine empire on a desert world was shown a trait its species cannot hold. The
    /// game gives it <c>trait_dry_planet_preference</c> instead, which is bound to the same three
    /// classes and allowed to <c>{ MACHINE ROBOT }</c>.
    /// </para>
    /// </remarks>
    /// <param name="planetClassKey">The world the species evolved on.</param>
    /// <param name="archetype">
    /// What the species is made of. Null asks only what the world offers, which is what a picker
    /// wants before a species class has been chosen.
    /// </param>
    public string? HabitabilityTraitFor(string? planetClassKey, string? archetype = null)
    {
        if (planetClassKey is not { Length: > 0 } planetClass)
        {
            return null;
        }

        // The game names a preference after the thing that grants it: a planet class, or a climate
        // where several classes share one. Those two answers are kept first so that a species the
        // trait suits still gets the trait the game names after its world.
        if (Named($"trait_{planetClass}_preference") is { } exact)
        {
            return exact;
        }

        var climate = _database.PlanetClasses
            .FirstOrDefault(p => string.Equals(p.Key, planetClass, StringComparison.Ordinal))?.Climate;

        if (climate is { Length: > 0 } && Named($"trait_auto_{climate}_preference") is { } shared)
        {
            return shared;
        }

        // Nothing named for this world that this species may hold, so ask the data instead: of the
        // preferences the archetype is allowed, whichever makes this world most habitable.
        var wanted = $"{planetClass}_habitability";

        return _database.Traits
            .Where(t => IsPreference(t) && Allows(t, archetype))
            .Select(t => (Key: t.Key, Habitability: t.Effects.Modifiers.GetValueOrDefault(wanted)))
            .Where(candidate => candidate.Habitability > 0)
            .OrderByDescending(candidate => candidate.Habitability)
            .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
            .Select(candidate => candidate.Key)
            .FirstOrDefault();

        string? Named(string key) =>
            _traits.TryGetValue(key, out var trait) && Allows(trait, archetype) ? key : null;
    }

    /// <summary>Whether a species of this archetype may hold the trait. An empty list allows any.</summary>
    private static bool Allows(TraitDefinition trait, string? archetype) =>
        archetype is null ||
        trait.AllowedArchetypes.Count == 0 ||
        trait.AllowedArchetypes.Contains(archetype, StringComparer.Ordinal);

    /// <summary>
    /// Whether a trait is one of the habitability preferences.
    /// </summary>
    /// <remarks>
    /// Asked of the trait's own effects rather than of its name. The two prefixes this used —
    /// <c>trait_pc_</c> and <c>trait_auto_</c> — matched 36 of the 48 the game defines and missed
    /// the whole <c>trait_machine_pc_*</c> family along with the four
    /// <c>trait_{cold,dry,wet,volcanic}_planet_preference</c>. All 48 grant a habitability modifier
    /// and no other trait ending in <c>_preference</c> exists, so that pair of facts names them
    /// exactly, and keeps naming them when the game adds another.
    /// </remarks>
    private static bool IsPreference(TraitDefinition trait) =>
        trait.Key.EndsWith(PreferenceSuffix, StringComparison.Ordinal) &&
        trait.Effects.Modifiers.Keys.Any(k => k.EndsWith(HabitabilitySuffix, StringComparison.Ordinal));

    private const string PreferenceSuffix = "_preference";
    private const string HabitabilitySuffix = "_habitability";

    /// <summary>
    /// Whether a trait is one the homeworld decides rather than one the player picks.
    /// </summary>
    /// <remarks>
    /// Offering these would be offering a choice the game does not have, and 32 of the 48 are
    /// <c>initial = yes</c> so they would otherwise reach the picker. They are shown among what the
    /// species already has, so a player can see what it is suited to.
    /// </remarks>
    public bool IsHabitabilityPreference(string traitKey) =>
        traitKey is { Length: > 0 } &&
        _traits.TryGetValue(traitKey, out var trait) &&
        IsPreference(trait);

    /// <summary>
    /// The portraits this species may wear, grouped as the game's picker groups them.
    /// </summary>
    /// <remarks>
    /// Order is the game's own and must be left alone. Sets use conditional groups with no
    /// condition purely to arrange the picker, so sorting here would rearrange the list for no
    /// reason. Sets belonging to other species classes are left out, and an origin that dictates a
    /// portrait narrows the choice to that one.
    /// </remarks>
    public IReadOnlyList<PortraitGroup> GetPortraitOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var groups = new List<PortraitGroup>();
        var setsByKey = _database.PortraitSets.ToDictionary(s => s.Key, StringComparer.Ordinal);

        foreach (var category in _database.PortraitCategories)
        {
            var options = new List<OptionState>();

            foreach (var setKey in category.Sets)
            {
                if (!setsByKey.TryGetValue(setKey, out var set))
                {
                    continue;
                }

                // A set belongs to one species class; showing another class's portraits would
                // offer a choice the game would not accept.
                if (set.SpeciesClass is { Length: > 0 } speciesClass &&
                    context.SpeciesClass is not null &&
                    !string.Equals(speciesClass, context.SpeciesClass, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var portrait in set.Portraits)
                {
                    var verdict = _evaluator.Evaluate(portrait.Playable, context);
                    options.Add(new OptionState(portrait.Key, true, verdict.Passed, verdict.Reasons));
                }
            }

            if (options.Count > 0)
            {
                groups.Add(new PortraitGroup(category.Key, category.NameKey, options));
            }
        }

        return groups;
    }

    /// <summary>Whether the chosen origin requires the player to design a second species.</summary>
    public bool RequiresSecondarySpecies(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return OriginOf(context)?.RequiresSecondarySpecies ?? false;
    }

    // ---------------------------------------------------------------------------------------
    // Options
    // ---------------------------------------------------------------------------------------

    /// <summary>The species classes the player may choose from.</summary>
    /// <summary>
    /// The species classes the player may choose from.
    /// </summary>
    /// <remarks>
    /// A class needs both an archetype and a face. The archetype rules out the several classes that
    /// exist only to carry a ship or city set — the game says as much in its own comments — but two
    /// that have one, Spinovore and Solarpunk, still have no portraits anywhere, and a species with
    /// no possible likeness is not a choice. Requiring a portrait set also keeps out AI, which has
    /// portraits but no archetype and is nobody's species.
    /// </remarks>
    public IReadOnlyList<OptionState> GetSpeciesClassOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Options(
            _database.SpeciesClasses.Where(c => !c.IsAppearanceOnly && HasPortraits(c.Key)),
            c => c.Key,
            c => c.Playable,

            // The game states a separate condition for the second species an origin calls for, and
            // this ignored it — a rule extracted, shipped and then not enforced, so the second
            // species could be given a class the game refuses.
            c => context.IsSecondarySpecies ? Combine(c.Possible, c.PossibleSecondary) : c.Possible,
            context);
    }

    private bool HasPortraits(string speciesClass) =>
        (_classesWithPortraits ??= [.. _database.PortraitSets
            .Select(s => s.SpeciesClass)
            .OfType<string>()]).Contains(speciesClass);

    private HashSet<string>? _classesWithPortraits;

    /// <summary>
    /// The authorities the player may choose from.
    /// </summary>
    /// <remarks>
    /// No <c>potential</c> here, unlike every other gated option, and that is deliberate. Two
    /// authorities declare one: Machine Intelligence requires <c>country_type = ai_empire</c> and
    /// Corporate requires <c>NOT = { country_type = primitive }</c>. Machine Intelligence is plainly
    /// playable, so the game does not read authority <c>potential</c> in its designer at all —
    /// honouring it faithfully would hide an authority the player is entitled to. Pinned by a test
    /// against the installation, so a patch that starts using the field is noticed rather than
    /// quietly ignored.
    /// </remarks>
    public IReadOnlyList<OptionState> GetAuthorityOptions(DesignContext context) =>
        Options(
            _database.Authorities.Where(a => !a.AiOnly),
            a => a.Key,
            a => a.Playable,
            a => a.Possible,
            context);

    /// <summary>
    /// The origins the player may choose from, with the rest closed off once one is chosen.
    /// </summary>
    /// <remarks>
    /// An empire has one origin, and choosing another used to swap it on a single press. That is a
    /// great deal to do quietly: an origin sets the homeworld and may add a second species, so
    /// landing on the wrong icon discarded a species the player had dressed and named. Held shut
    /// instead, and cleared deliberately — which the design allows, and which the validation already
    /// has words for.
    /// </remarks>
    public IReadOnlyList<OptionState> GetOriginOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = Options(
            _database.Civics.Where(c => c.IsOrigin),
            c => c.Key,
            c => Combine(c.Playable, c.Potential),
            c => c.Possible,
            context);

        return context.Origin is not { Length: > 0 } chosen
            ? options
            : [.. options.Select(o => string.Equals(o.Key, chosen, StringComparison.Ordinal)
                ? o
                : Blocked(o, RuleReasons.OriginAlreadyChosen))];
    }

    /// <summary>
    /// The traits the empire's ruler may be given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Filtered by the ruler's class, which is the thing this did not do. A ruler trait names the
    /// classes it is for and the game holds to it - every one of the fifty-two empires the game
    /// ships that gives its ruler a trait gives them a class that trait allows - so an official
    /// offered the commander's warlike was being offered something the game would refuse.
    /// </para>
    /// <para>
    /// A ruler holds one trait, so once they have it the rest are closed rather than swapped. The
    /// held one stays open, which is how it is let go of.
    /// </para>
    /// </remarks>
    public IReadOnlyList<OptionState> GetRulerTraitOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var held = context.RulerTraits;
        var options = new List<OptionState>();

        foreach (var trait in _database.Traits.Where(t => t.Kind == TraitKind.StartingRuler))
        {
            var has = held.Contains(trait.Key);
            var reasons = RulerTraitObjections(trait, context);

            // The ruler has their one already, and it is not this one.
            if (!has && held.Count > 0)
            {
                reasons.Add(RuleReasons.RulerTraitTaken);
            }

            options.Add(new OptionState(
                trait.Key,
                Visible: true,
                Enabled: reasons.Count == 0 || has,
                Reasons: has ? [] : reasons,
                RequiredDlc: trait.RequiredDlc is { } dlc && !context.OwnedDlc.Contains(dlc) ? dlc : null));
        }

        return options;
    }

    /// <summary>
    /// Why this empire's ruler could not take a trait: their class, the origin, the ethics.
    /// </summary>
    /// <remarks>
    /// Shared by the picker and by validation, which want the same answer for opposite reasons - the
    /// picker to grey out something not yet taken, validation to object to something already held.
    /// The picker's own "you already have one" rule is not here, because it is about the ruler
    /// rather than about the trait, and a held trait must not object to itself.
    /// </remarks>
    private static List<string> RulerTraitObjections(TraitDefinition trait, DesignContext context)
    {
        var reasons = new List<string>();

        if (trait.AllowedLeaderClasses.Count > 0 &&
            (context.RulerClass is null || !trait.AllowedLeaderClasses.Contains(context.RulerClass)))
        {
            reasons.Add(RuleReasons.For(
                RuleReasons.WrongLeaderClass,
                string.Join(", ", trait.AllowedLeaderClasses)));
        }

        if (context.Origin is { } origin && trait.ForbiddenOrigins.Contains(origin))
        {
            reasons.Add(RuleReasons.For(RuleReasons.ForbiddenByOrigin, origin));
        }

        if (trait.AllowedOrigins.Count > 0 &&
            (context.Origin is null || !trait.AllowedOrigins.Contains(context.Origin)))
        {
            reasons.Add(RuleReasons.For(
                RuleReasons.WrongOrigin,
                string.Join(", ", trait.AllowedOrigins)));
        }

        if (trait.AllowedEthics.Count > 0 && !context.Ethics.Any(trait.AllowedEthics.Contains))
        {
            reasons.Add(RuleReasons.For(
                RuleReasons.WrongEthics,
                string.Join(", ", trait.AllowedEthics)));
        }

        return reasons;
    }

    /// <summary>
    /// The civics the player may choose from, with the rest closed off once the slots are full.
    /// </summary>
    /// <remarks>
    /// The allowance is two, and a third choice used to take the place of the oldest. Nothing said
    /// so, so a press meant to add a civic removed one — and the one it removed was whichever had
    /// been chosen first, which is not a thing anybody keeps track of.
    /// </remarks>
    public IReadOnlyList<OptionState> GetCivicOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = Options(
            _database.Civics.Where(c => !c.IsOrigin),
            c => c.Key,
            c => Combine(c.Playable, c.Potential),
            c => c.Possible,
            context);

        return context.Civics.Count < _database.Defines.CivicPoints
            ? options
            : [.. options.Select(o => context.Civics.Contains(o.Key)
                ? o
                : Blocked(o, RuleReasons.NoCivicSlotsLeft))];
    }

    /// <summary>
    /// The ascension perks a plan may name, with the ones the game would refuse disabled.
    /// </summary>
    /// <remarks>
    /// The chosen perks are handed in rather than read from the design, because a design has no
    /// field for them - they live in a plan. They go into the context, which is what makes the
    /// game's own exclusions work: nearly every one is written "not if that other perk is taken",
    /// and now something can answer that.
    /// </remarks>
    public IReadOnlyList<OptionState> GetAscensionPerkOptions(
        DesignContext context,
        IReadOnlyCollection<string> chosen,
        IReadOnlyList<string> trees)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(trees);

        // Every tree the plan names, because the two lists are chosen independently rather than in
        // turn: the traditions are settled in one go and the perks in another. What that answers is
        // "does the plan open this tree", which is what the perks asking after a named tradition
        // want - and not "how many were open at this moment", which a plan does not sequence.
        var planned = context.WithPlan(chosen, Opened(trees));

        var options = Options(
            _database.AscensionPerks,
            p => p.Key,
            p => p.Potential,
            p => p.Possible,
            planned);

        // A perk that hands over a tradition tree needs the plan to have somewhere to put it.
        options =
        [
            .. options.Select(o => chosen.Contains(o.Key) || HasRoomForGrantedTree(TreesGrantedBy(o.Key), trees)
                ? o
                : Blocked(o, RuleReasons.NoTraditionSlotsLeft))
        ];

        return chosen.Count < _database.Defines.AscensionPerkSlots
            ? options
            : [.. options.Select(o => chosen.Contains(o.Key) ? o : Blocked(o, RuleReasons.NoPerkSlotsLeft))];
    }

    /// <summary>
    /// How many ascension perks are named against how many a game grants.
    /// </summary>
    /// <remarks>
    /// All eight, rather than however many the planned trees would unlock. A game does earn them
    /// one tree at a time, but a plan is not made one tree at a time - the traditions are settled
    /// in one go and the perks in another - and tying the count to the trees meant opening the
    /// perks tab first offered a single slot with no way to see why.
    /// </remarks>
    public Budget GetAscensionPerkBudget(int chosen) =>
        new(chosen, _database.Defines.AscensionPerkSlots);

    /// <summary>
    /// The tradition trees a plan may open, with the ones this empire could not disabled.
    /// </summary>
    /// <remarks>
    /// The perks come in too, because the trees ask about them: an ascension tree's own condition is
    /// a NOR over every other ascension, and those expand to the perks that grant them. So planning
    /// a cybernetic perk takes the psionic and genetic trees off the list, exactly as taking it in
    /// the game would.
    ///
    /// A tree has only a potential and no possible, so what an empire cannot open is hidden rather
    /// than shown struck out - which is what the game does with them.
    /// </remarks>
    public IReadOnlyList<OptionState> GetTraditionTreeOptions(
        DesignContext context,
        IReadOnlyCollection<string> trees,
        IReadOnlyCollection<string> perks)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentNullException.ThrowIfNull(perks);

        // Two moments, because the two questions are different. Whether a tree is one this
        // empire could ever open is asked of the whole plan - its potential rules out the other
        // ascension paths, and a perk named later still rules them out. Whether it can be opened
        // now is asked of the plan so far.
        //
        // So far means: as many perks as there are trees already planned. Finishing a tree is what
        // grants an ascension perk slot - all thirty-two of them do, sixteen outright and sixteen
        // through whichever variant fits the empire - so a plan opening its fourth tree has had
        // three slots and can have spent at most three perks. Read against the whole list instead,
        // Purity was open from the first slot to an empire whose plan named Biomorphosis third,
        // which is an order the game could not play.
        var planned = context.WithPlan(perks, Opened(trees));
        var sofar = context.WithPlan(perks.Take(trees.Count), Opened(trees));

        var options = Options(
            _database.TraditionTrees,
            t => t.Key,
            t => t.Potential,

            // What it takes to open it, which the game states on the tradition that opens it rather
            // than on the tree. Nothing on an ascension tree asks for its ascension perk;
            // tr_cybernetics_adopt does, and so a plan that has not named The Flesh is Weak cannot
            // open Cybernetics - which is the rule in the game and was missing here.
            t => Adopting(t),
            planned,
            whenTaken: sofar);

        // The last slot is spoken for where a planned perk still owes the plan a tree. Anything
        // else taken there leaves the grant with nowhere to land, and the perk loses it.
        var owed = perks
            .SelectMany(TreesGrantedBy)
            .Where(t => !trees.Contains(t))
            .ToHashSet(StringComparer.Ordinal);

        if (owed.Count > 0 &&
            trees.Count == _database.Defines.TraditionSlots - 1 &&
            !perks.SelectMany(TreesGrantedBy).Any(trees.Contains))
        {
            options =
            [
                .. options.Select(o => owed.Contains(o.Key) || trees.Contains(o.Key)
                    ? o
                    : Blocked(o, RuleReasons.NoTraditionSlotsLeft))
            ];
        }

        return trees.Count < _database.Defines.TraditionSlots
            ? options
            : [.. options.Select(o => trees.Contains(o.Key)
                ? o
                : Blocked(o, RuleReasons.NoTraditionSlotsLeft))];
    }

    /// <summary>
    /// The tradition trees an ascension perk hands over, where it hands over any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six trees arrive this way rather than being chosen. Biomorphosis starts a situation whose
    /// last event runs <c>add_tradition</c> for whichever of Purity, Cloning and Mutation the
    /// player picks there; the Synthetic Age does the same for Nanotech, Modularity and Virtuality.
    /// So the perk does not unlock the tree for selection - it gives it.
    /// </para>
    /// <para>
    /// Not every tree that asks for a perk is one the perk gives. Biomorphosis is asked for by four
    /// and gives three: Genetics asks for it too and is chosen like any other tree once it is held.
    /// The two look the same once a flag has been compiled into the perk behind it, so which is
    /// which is written down - see <see cref="DesignPredicates.TreesGrantedByPerk"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> TreesGrantedBy(string perk)
    {
        ArgumentException.ThrowIfNullOrEmpty(perk);

        return DesignPredicates.TreesGrantedByPerk.TryGetValue(perk, out var granted)
            ? [.. granted.Where(t => _database.TraditionTrees.Any(x => x.Key == t))]
            : [];
    }

    /// <summary>
    /// Whether a plan has somewhere to put the tree a perk would hand it.
    /// </summary>
    /// <remarks>
    /// The game checks this when the situation finishes, not when the perk is taken: with every
    /// tradition slot already full it simply skips the grant, and the perk keeps whatever else it
    /// does. A plan is a whole statement of intent though, so one that takes the perk and leaves
    /// the tree nowhere to go is a plan that throws it away - which is stricter than the game and
    /// is the point of planning.
    ///
    /// A granted tree the plan already names needs no room of its own: it is the tree, and it has
    /// its slot already.
    /// </remarks>
    public bool HasRoomForGrantedTree(IReadOnlyList<string> granted, IReadOnlyCollection<string> trees)
    {
        ArgumentNullException.ThrowIfNull(granted);
        ArgumentNullException.ThrowIfNull(trees);

        return granted.Count == 0
            || granted.Any(trees.Contains)
            || trees.Count < _database.Defines.TraditionSlots;
    }

    /// <summary>
    /// What the game asks before a tree may be opened, read off the tradition that opens it.
    /// </summary>
    /// <remarks>
    /// Every tree has one and it is where the ascension trees state their perk. The ordinary trees
    /// ask nothing, so this is an always-true for all but a dozen of them.
    /// </remarks>
    private Requirement Adopting(TraditionTreeDefinition tree) =>
        tree.AdoptionBonus is { } adopt &&
        _database.Traditions.FirstOrDefault(t => t.Key == adopt) is { } opening
            ? opening.Possible
            : AlwaysAllowed;

    /// <summary>How many tradition trees are named against how many a game allows.</summary>
    public Budget GetTraditionBudget(int chosen) => new(chosen, _database.Defines.TraditionSlots);

    /// <summary>
    /// Whether every perk in a plan would be allowed in the place it sits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The question reordering asks. Twenty-five perks are gated on how many come before them -
    /// "you must already have three" - which read against an ordered plan is a rule about position:
    /// such a perk cannot be fourth or earlier. So each is judged against a context holding only
    /// what precedes it, and the count falls out of that set's size with nothing extra to keep in
    /// step.
    /// </para>
    /// <para>
    /// Walked alongside the trees, one for one. Finishing a tradition tree is what grants an
    /// ascension perk slot, and every tree in the game grants one, so the perk in the nth place is
    /// the one taken once the nth tree is done - which is what lets a plan of two lists be read as
    /// a single order at all.
    /// </para>
    /// </remarks>
    public bool IsLegalPerkOrder(
        DesignContext context,
        IReadOnlyList<string> perks,
        IReadOnlyList<string> trees)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(perks);
        ArgumentNullException.ThrowIfNull(trees);

        var opened = Opened(trees).ToList();

        for (var at = 0; at < perks.Count; at++)
        {
            if (_database.AscensionPerks.FirstOrDefault(p => p.Key == perks[at]) is not { } perk)
            {
                continue;
            }

            // The trees finished by then, and no more. A perk slot comes from finishing a tree,
            // so the perk in the third place is the one taken after the third tree - and a perk
            // asking for a tradition out of the fourth tree cannot be third.
            var before = context.WithPlan(perks.Take(at), Opened(trees.Take(at + 1)));

            if (!_evaluator.Evaluate(perk.Possible, before).Passed ||
                !_evaluator.Evaluate(perk.Potential, before).Passed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether every tree in a plan could be opened in the place it sits.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="IsLegalPerkOrder"/>, and it only started to matter when six trees
    /// turned out to be gated on an ascension perk. Purity asks for Biomorphosis; Biomorphosis is a
    /// perk, and a perk costs a slot that only a finished tree grants - so a plan naming
    /// Biomorphosis third cannot open Purity until its fourth tree, whatever order the two lists
    /// are written in.
    ///
    /// Only the adoption gate is asked. A tree's potential is about the empire rather than about
    /// when it is opened, and judging it here would refuse a tree for holding a perk the plan takes
    /// later - which is the whole point of planning one.
    /// </remarks>
    public bool IsLegalTreeOrder(
        DesignContext context,
        IReadOnlyList<string> trees,
        IReadOnlyList<string> perks)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentNullException.ThrowIfNull(perks);

        for (var at = 0; at < trees.Count; at++)
        {
            if (_database.TraditionTrees.FirstOrDefault(t => t.Key == trees[at]) is not { } tree)
            {
                continue;
            }

            // As many perks as there are trees before this one, for the reason above.
            var before = context.WithPlan(perks.Take(at), Opened(trees.Take(at)));

            if (!_evaluator.Evaluate(Adopting(tree), before).Passed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The civics an empire cannot give up, which a plan has to spend slots on whether it names
    /// them or not.
    /// </summary>
    /// <remarks>
    /// The game's own <c>modification</c> field, whose comment says it is there "to prevent adding
    /// or removing this after creation of the empire". Ninety-six civics say so outright and a few
    /// dozen more only for removal. These are not written into a plan - the design already says
    /// which civics it has - but they are shown, because a player choosing what to reform into
    /// needs to see what is not up for discussion.
    /// </remarks>
    public IReadOnlyList<OptionState> GetLockedCivics(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var locked = new List<OptionState>();

        foreach (var civic in _database.Civics.Where(c => !c.IsOrigin && context.Civics.Contains(c.Key)))
        {
            var verdict = _evaluator.Evaluate(civic.CanRemoveLater, context);

            if (!verdict.Passed)
            {
                // Carrying why, which is the game's own sentence about the civic rather than
                // anything invented here - a row that cannot be pressed and does not say so is just
                // a row that appears broken.
                locked.Add(new OptionState(civic.Key, true, false, verdict.Reasons));
            }
        }

        return locked;
    }

    /// <summary>
    /// The civics a plan may name, for the empire a government reform would leave behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <see cref="GetCivicOptions"/> with a different budget. That one blocks everything the
    /// design does not already hold the moment its slots are full, which is exactly the empire a
    /// player opens this editor to change - it would answer every question with "you have no room",
    /// which is true now and is the thing being planned away.
    /// </para>
    /// <para>
    /// A civic the game will not let a reform add is left out rather than shown struck through,
    /// because it is not a choice this editor can offer at all. One already held is kept regardless,
    /// so a locked civic still has an option to be drawn as.
    /// </para>
    /// </remarks>
    public IReadOnlyList<OptionState> GetPlanCivicOptions(
        DesignContext context,
        IReadOnlyCollection<string> planned)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(planned);

        var locked = GetLockedCivics(context).Select(o => o.Key).ToList();

        // What the empire would end up holding, which is what the game's own exclusions have to be
        // asked about - a planned civic judged against the government it replaces is judged against
        // the wrong empire.
        var after = context.WithPlan(
            context.AscensionPerks,
            context.TraditionTrees,
            locked.Concat(planned));

        var reachable = _database.Civics.Where(c => !c.IsOrigin
            && (context.Civics.Contains(c.Key) || _evaluator.Evaluate(c.CanAddLater, context).Passed));

        var options = Options(
            reachable,
            c => c.Key,
            c => Combine(c.Playable, c.Potential),
            c => c.Possible,
            after);

        return locked.Count + planned.Count < _database.Defines.PlannedCivicPoints
            ? options
            : [.. options.Select(o => planned.Contains(o.Key) || locked.Contains(o.Key)
                ? o
                : Blocked(o, RuleReasons.NoCivicSlotsLeft))];
    }

    /// <summary>How many civics a plan has spoken for against how many a game ends with.</summary>
    /// <remarks>
    /// Against the three an empire can reach rather than the two it starts with, since the third
    /// slot - the one <c>tech_galactic_administration</c> unlocks - is the reason players plan a
    /// reform at all.
    /// </remarks>
    public Budget GetPlanCivicBudget(int taken) => new(taken, _database.Defines.PlannedCivicPoints);

    /// <summary>
    /// The trees a plan opens, said both ways the game says it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three Machine Age trees rule each other out, and they do it by asking whether the
    /// tradition that opens the other has been taken - <c>has_tradition = tr_nanotech_adopt</c>, not
    /// the tree's own name. So both names go in, or the condition looks at a set that cannot contain
    /// what it is asking about and every one of those exclusions quietly never fires.
    /// </para>
    /// <para>
    /// The tradition that finishes it as well, because the perks ask for those:
    /// <c>tr_unyielding_finish</c>, <c>tr_supremacy_finish</c> and three more. A plan names trees
    /// rather than the traditions inside them, and there is no such thing as a half-opened tree at
    /// that level - meaning to open one is meaning to finish it, so both ends of it answer yes.
    /// </para>
    /// </remarks>
    private IEnumerable<string> Opened(IEnumerable<string> trees)
    {
        foreach (var key in trees)
        {
            yield return key;

            if (_database.TraditionTrees.FirstOrDefault(t => t.Key == key) is not { } tree)
            {
                continue;
            }

            if (tree.AdoptionBonus is { } adopt)
            {
                yield return adopt;
            }

            if (tree.FinishBonus is { } finish)
            {
                yield return finish;
            }
        }
    }

    /// <summary>Nothing standing in the way, for things the game gates only on being listed.</summary>
    private static readonly Requirement AlwaysAllowed = new AlwaysRequirement(true);

    /// <summary>
    /// Closes an option off for a reason of the designer's rather than the game's.
    /// </summary>
    /// <remarks>
    /// The reason is added to whatever the game already had to say, and put last: what the empire
    /// is may be a permanent objection, while being full is undone by letting something go.
    /// </remarks>
    private static OptionState Blocked(OptionState option, string reason) =>
        option with { Enabled = false, Reasons = [.. option.Reasons, reason] };

    /// <summary>
    /// The ethics the player may choose from, with the ones that would break a rule disabled.
    /// </summary>
    public IReadOnlyList<OptionState> GetEthicOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var budget = GetEthicsBudget(context);
        var options = new List<OptionState>(_database.Ethics.Count);

        foreach (var ethic in _database.Ethics)
        {
            if (context.Ethics.Contains(ethic.Key))
            {
                options.Add(OptionState.Available(ethic.Key, ethic.Cost));
                continue;
            }

            var reasons = new List<string>();

            // Gestalt consciousness replaces an empire's whole ethos rather than joining it.
            if ((ethic.IsGestalt && context.Ethics.Count > 0) || (!ethic.IsGestalt && context.IsGestalt))
            {
                reasons.Add(RuleReasons.GestaltExclusive);
            }

            // The same ethic held at the other strength is not a conflict, it is the thing being
            // changed. Militarist and Fanatic Militarist share a category, so taking one while
            // holding the other was reported as blocked — and the game moves freely between them,
            // which is what the fanatic_variant and regular_variant fields are for.
            var replaced = context.Ethics.FirstOrDefault(e => IsSameEthicAtAnotherStrength(e, ethic));

            // Opposing ethics share a category, and only one may be taken from each.
            var conflicting = context.Ethics.FirstOrDefault(
                e => !IsSameEthicAtAnotherStrength(e, ethic) &&
                     _ethics.TryGetValue(e, out var taken) &&
                     string.Equals(taken.Category, ethic.Category, StringComparison.Ordinal));

            if (conflicting is not null)
            {
                reasons.Add(RuleReasons.For(RuleReasons.EthicGroupTaken, conflicting));
            }

            // A swap costs only the difference, since what it replaces is given back.
            var refunded = replaced is not null && _ethics.TryGetValue(replaced, out var previous)
                ? previous.Cost
                : 0;

            if (budget.Remaining < ethic.Cost - refunded)
            {
                reasons.Add(RuleReasons.NotEnoughEthicsPoints);
            }

            options.Add(new OptionState(ethic.Key, true, reasons.Count == 0, reasons, ethic.Cost));
        }

        return options;
    }

    /// <summary>
    /// Whether an ethic already held is the same conviction as another, at the other strength.
    /// </summary>
    /// <remarks>
    /// The game states the pairing itself, in <c>fanatic_variant</c> and <c>regular_variant</c>, so
    /// this reads it rather than inferring it from a shared category or from the word "fanatic".
    /// </remarks>
    private bool IsSameEthicAtAnotherStrength(string held, EthicDefinition wanted) =>
        string.Equals(wanted.FanaticVariant, held, StringComparison.Ordinal) ||
        string.Equals(wanted.RegularVariant, held, StringComparison.Ordinal);

    /// <summary>The ethic this one would replace, where the empire holds it at the other strength.</summary>
    public string? EthicReplacedBy(DesignContext context, string ethicKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _ethics.TryGetValue(ethicKey, out var wanted)
            ? context.Ethics.FirstOrDefault(e => IsSameEthicAtAnotherStrength(e, wanted))
            : null;
    }

    /// <summary>
    /// The traits the founder species may take, with the ones that would break a rule disabled.
    /// </summary>
    public IReadOnlyList<OptionState> GetSpeciesTraitOptions(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var budget = GetTraitBudget(context);
        var options = new List<OptionState>();

        foreach (var trait in _database.Traits.Where(t => t.Kind == TraitKind.Species))
        {
            // Hidden traits and ones the game never offers at creation are not choices at all.
            if (trait.Hidden || !trait.Initial)
            {
                continue;
            }

            var selected = context.Traits.Contains(trait.Key);
            var reasons = selected ? [] : TraitBlockers(trait, context, budget);

            options.Add(new OptionState(
                trait.Key,
                Visible: IsTraitRelevant(trait, context),
                Enabled: selected || reasons.Count == 0,
                reasons,
                trait.Cost,
                trait.RequiredDlc is { } dlc && !context.OwnedDlc.Contains(dlc) ? dlc : null));
        }

        return options;
    }

    // ---------------------------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Checks a whole design and reports everything the game would reject.</summary>
    public ValidationReport Validate(EmpireDesign design, IReadOnlySet<string>? ownedDlc = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        return Validate(CreateContext(design, ownedDlc), design);
    }

    /// <summary>Checks a design that has already been reduced to a context.</summary>
    public ValidationReport Validate(DesignContext context, EmpireDesign? design = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problems = new List<ValidationProblem>();

        ValidateSpeciesClass(context, problems);
        ValidateTraits(context, problems);
        ValidateEthics(context, problems);
        ValidateAuthority(context, problems);
        ValidateCivics(context, problems);
        ValidateOrigin(context, problems);
        ValidateHomeworld(context, problems);
        ValidateRuler(context, problems);
        ValidateForcedTraits(context, problems);
        ValidateGovernment(context, problems);

        // Needs the design rather than the context: what the game refuses here are empty fields, and
        // the context carries the empire's choices rather than the text on it.
        if (design is not null)
        {
            ValidateRequiredNames(design, problems);
        }

        if (design is not null && RequiresSecondarySpecies(context))
        {
            if (design.SecondarySpecies is not { } secondary)
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.SecondarySpecies,
                    context.Origin,
                    "This origin needs a second species, and the design has none.",
                    []));
            }
            else
            {
                // Judged as its own species rather than as the founders. Nothing checked it before,
                // so a second species could carry any number of traits at any cost and the design
                // still read as valid.
                ValidateTraits(
                    context.ForSpecies(secondary, secondary: true),
                    problems,
                    ValidationArea.SecondarySpecies);
            }
        }

        return new ValidationReport(problems);
    }

    /// <summary>
    /// The empire has to add up to a government the game recognises.
    /// </summary>
    /// <remarks>
    /// The game names a government from the authority, the ethics and the civics together, shows the
    /// answer under the authority grid, and refuses the design when nothing matches -
    /// <c>GAMESETUP_COUNTRY_GOVERNMENT_TYPE_INVALID</c>. We derived the same answer for the header
    /// and never objected when it came back with nothing.
    ///
    /// Only asked once the three things it is derived from are present, because "no government yet"
    /// is the ordinary state of a half-built empire and the sections it comes from are already
    /// saying so themselves.
    /// </remarks>
    private void ValidateGovernment(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.Authority is not { Length: > 0 } || context.Ethics.Count == 0)
        {
            return;
        }

        if (DeriveGovernment(context) is null)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Authority,
                null,
                "These ethics, authority and civics do not add up to a government the game has.",
                []));
        }
    }

    /// <summary>
    /// The traits the empire's own choices impose on its founders.
    /// </summary>
    /// <remarks>
    /// The game states the contract in the authority file: a <c>traits</c> list on an authority
    /// forces those traits on the founder species, and is "only verified for empire designs, no
    /// effect after game start". Verified for empire designs is precisely the case this app
    /// produces, and its own empires comply - the prescripted hive minds write
    /// <c>trait_hive_mind</c> into the species block rather than relying on the authority.
    ///
    /// These were computed for the picker, which showed them among the chosen traits and refused to
    /// let them go, and never written into the design. So an empire switched to Hive Mind displayed
    /// the trait and did not carry it.
    /// </remarks>
    private void ValidateForcedTraits(DesignContext context, List<ValidationProblem> problems)
    {
        foreach (var forced in GetWrittenForcedTraits(context).Where(t => !context.Traits.Contains(t)))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Traits,
                forced,
                $"The species must have '{forced}' and the design does not carry it.",
                []));
        }
    }

    /// <summary>
    /// The starting ruler, whose trait has to be one this empire's ruler could hold.
    /// </summary>
    /// <remarks>
    /// The rules for this were written for the picker and never run at validation time, so a design
    /// that arrived by import or by hand - an official holding a commander's trait, say - was
    /// reported as ready to play.
    /// </remarks>
    private void ValidateRuler(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.RulerTraits.Count > 1)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ruler,
                null,
                $"The ruler has {context.RulerTraits.Count} traits but may have one.",
                []));
        }

        var traits = _database.Traits
            .Where(t => t.Kind == TraitKind.StartingRuler)
            .ToDictionary(t => t.Key, StringComparer.Ordinal);

        foreach (var key in context.RulerTraits)
        {
            if (!traits.TryGetValue(key, out var trait))
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Ruler,
                    key,
                    $"'{key}' is not a trait a starting ruler may have.",
                    []));
                continue;
            }

            if (RulerTraitObjections(trait, context) is { Count: > 0 } reasons)
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Ruler, key, $"The ruler may not have '{key}'.", reasons));
            }
        }
    }

    /// <summary>
    /// The fields the game insists on before it will offer a design at all.
    /// </summary>
    /// <remarks>
    /// Each of these has a refusal string of its own in the game -
    /// <c>GAMESETUP_COUNTRY_INVALID_EMPIRE_NAME</c> and its four neighbours - and none of them was
    /// checked here. They are what a half-finished empire trips, and the failure is silent: the game
    /// does not complain, the empire simply stops appearing in the list.
    /// </remarks>
    private void ValidateRequiredNames(EmpireDesign design, List<ValidationProblem> problems)
    {
        if (design.Name.IsEmpty)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Empire, null, "The empire has no name.", []));
        }

        if (design.PlanetName.IsEmpty)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Homeworld, null, "The homeworld has no name.", []));
        }

        // Stored either whole or split into two, and the game's own empires use both forms.
        var ruler = design.Ruler.Name;
        var named = ruler.FullNames is { IsEmpty: false }
            || ruler.FirstName is { IsEmpty: false }
            || ruler.SecondName is { IsEmpty: false };

        if (!named)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ruler, null, "The ruler has no name.", []));
        }

        if (string.IsNullOrWhiteSpace(design.Species.NameList))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Species, null, "The species has no name list.", []));
        }

        if (string.IsNullOrWhiteSpace(design.Species.Portrait))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Species, null, "The species has no portrait.", []));
        }
    }

    private void ValidateSpeciesClass(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.SpeciesClass is not { Length: > 0 } key)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Species, null, "No species class is set.", []));
            return;
        }

        var speciesClass = SpeciesClassOf(context);
        if (speciesClass is null)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Species, key, $"'{key}' is not a species class this game defines.", []));
            return;
        }

        Check(ValidationArea.Species, key, speciesClass.Playable, "is not available", context, problems);
        Check(ValidationArea.Species, key, speciesClass.Possible, "cannot be used by this empire", context, problems);
    }

    private void ValidateTraits(
        DesignContext context,
        List<ValidationProblem> problems,
        ValidationArea area = ValidationArea.Traits)
    {
        var budget = GetTraitBudget(context);

        if (budget.Points.IsOverspent)
        {
            problems.Add(new ValidationProblem(
                area,
                null,
                $"Traits cost {budget.Points.Spent} points but only {budget.Points.Available} are available.",
                []));
        }

        if (budget.Picks.IsOverspent)
        {
            problems.Add(new ValidationProblem(
                area,
                null,
                $"The species has {budget.Picks.Spent} traits but may have {budget.Picks.Available}.",
                []));
        }

        foreach (var key in context.Traits)
        {
            if (!_traits.TryGetValue(key, out var trait))
            {
                problems.Add(new ValidationProblem(
                    area, key, $"'{key}' is not a trait this game defines.", []));
                continue;
            }

            foreach (var reason in TraitBlockers(trait, context, budget, ignoreBudget: true))
            {
                problems.Add(new ValidationProblem(
                    area, key, "{0} cannot be taken by this species.", [reason])
                {
                    Arguments = [key],
                });
            }
        }
    }

    private void ValidateEthics(DesignContext context, List<ValidationProblem> problems)
    {
        var budget = GetEthicsBudget(context);

        if (context.Ethics.Count == 0)
        {
            problems.Add(new ValidationProblem(ValidationArea.Ethics, null, "The empire has no ethics.", []));
            return;
        }

        foreach (var key in context.Ethics.Where(e => !_ethics.ContainsKey(e)))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ethics, key, $"'{key}' is not an ethic this game defines.", []));
        }

        // Exactly, not at most. ETHOS_MAX_POINTS is 3 and the game spends all three: a fanatic ethic
        // costs 2, a regular one 1, gestalt consciousness all 3, so the only legal spends are 1+1+1,
        // 2+1 and 3 - and every one of the fifty-two empires the game ships spends exactly three.
        // Checking only for overspend called an empire holding a single ethic valid, and the game
        // does not offer such an empire at all. The civics check below has always read this way.
        if (budget.Spent != budget.Available)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ethics,
                null,
                budget.IsOverspent
                    ? $"Ethics cost {budget.Spent} points but only {budget.Available} are available."
                    : $"Ethics cost {budget.Spent} points of {budget.Available}, and all of them must be spent.",
                []));
        }

        // A gestalt has no ethos beyond being a gestalt.
        if (context.IsGestalt && context.Ethics.Count > 1)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ethics,
                "ethic_gestalt_consciousness",
                "Gestalt consciousness cannot be combined with other ethics.",
                []));
        }

        foreach (var group in context.Ethics
                     .Select(e => _ethics.GetValueOrDefault(e))
                     .OfType<EthicDefinition>()
                     .GroupBy(e => e.Category, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ethics,
                group.Key,
                "Only one ethic may be taken from the {0} group, but the empire has " +
                string.Join(" and ", group.Select((_, i) => $"{{{i + 1}}}")) + ".",
                [])
            {
                Arguments = [group.Key, .. group.Select(e => e.Key)],
            });
        }
    }

    private void ValidateAuthority(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.Authority is not { Length: > 0 } key)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Authority, null, "No authority is set.", []));
            return;
        }

        var authority = _database.Authorities
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal));

        if (authority is null)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Authority, key, $"'{key}' is not an authority this game defines.", []));
            return;
        }

        Check(ValidationArea.Authority, key, authority.Playable, "is not available", context, problems);
        Check(ValidationArea.Authority, key, authority.Possible, "cannot be used by this empire", context, problems);
    }

    private void ValidateCivics(DesignContext context, List<ValidationProblem> problems)
    {
        var budget = GetCivicsBudget(context);

        if (budget.Spent != budget.Available)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Civics,
                null,
                $"The empire has {budget.Spent} civics but must have exactly {budget.Available}.",
                []));
        }

        foreach (var key in context.Civics)
        {
            if (!_civics.TryGetValue(key, out var civic) || civic.IsOrigin)
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Civics, key, $"'{key}' is not a civic this game defines.", []));
                continue;
            }

            Check(ValidationArea.Civics, key, civic.Playable, "is not available", context, problems);
            Check(ValidationArea.Civics, key, civic.Potential, "does not apply to this empire", context, problems);
            Check(ValidationArea.Civics, key, civic.Possible, "cannot be combined with the rest of this empire", context, problems);
        }
    }

    private void ValidateOrigin(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.Origin is not { Length: > 0 } key)
        {
            problems.Add(new ValidationProblem(ValidationArea.Origin, null, "No origin is set.", []));
            return;
        }

        if (!_civics.TryGetValue(key, out var origin) || !origin.IsOrigin)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Origin, key, $"'{key}' is not an origin this game defines.", []));
            return;
        }

        Check(ValidationArea.Origin, key, origin.Playable, "is not available", context, problems);
        Check(ValidationArea.Origin, key, origin.Potential, "does not apply to this empire", context, problems);
        Check(ValidationArea.Origin, key, origin.Possible, "cannot be combined with the rest of this empire", context, problems);
    }

    private void ValidateHomeworld(DesignContext context, List<ValidationProblem> problems)
    {
        if (context.PlanetClass is not { Length: > 0 } key)
        {
            return;
        }

        // A nomadic empire lives aboard an arkship whatever its design records, exactly as an
        // origin's own world overrides one - so the same warning rather than a refusal. The game's
        // own nomadic empire writes pc_ark and starts there; one written with a planet still loads
        // and still starts there.
        //
        // It matters because of how a design gets here: turning the toggle on is what invalidates
        // the world, and being told the world "is not one this empire can start on" reads as
        // something the player did wrong rather than something the toggle did.
        if (context.IsNomadic && HasPlanetClass(Arkship))
        {
            if (!string.Equals(key, Arkship, StringComparison.Ordinal))
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Homeworld,
                    key,
                    "A nomadic empire starts aboard an arkship, so the {0} homeworld is ignored.",
                    [],
                    ValidationSeverity.Warning)
                {
                    Arguments = [key],
                });
            }

            return;
        }

        // An origin that supplies its own homeworld simply overrides whatever the design recorded.
        // The game loads such a design and uses the origin's world, so this is worth mentioning
        // but is not a reason to reject the empire.
        if (OriginOf(context) is { } origin &&
            (origin.HabitabilityPreference ?? origin.StartingColony) is { Length: > 0 } imposed)
        {
            if (!string.Equals(key, imposed, StringComparison.Ordinal))
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Homeworld,
                    key,
                    "This origin starts the empire on {0}, so the {1} homeworld is ignored.",
                    [],
                    ValidationSeverity.Warning)
                {
                    Arguments = [imposed, key],
                });
            }

            return;
        }

        if (!GetHomeworldOptions(context).Contains(key, StringComparer.Ordinal))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Homeworld,
                key,
                "{0} is not a homeworld this empire can start on.",
                [])
            {
                Arguments = [key],
            });
        }
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>Everything about a trait that would stop this species taking it.</summary>
    private List<string> TraitBlockers(
        TraitDefinition trait,
        DesignContext context,
        TraitBudget budget,
        bool ignoreBudget = false)
    {
        var reasons = new List<string>();

        if (trait.RequiredDlc is { } dlc && !context.OwnedDlc.Contains(dlc))
        {
            reasons.Add(RuleReasons.For(RuleReasons.MissingDlc, dlc));
        }

        if (trait.AllowedArchetypes.Count > 0 &&
            (context.SpeciesArchetype is null || !trait.AllowedArchetypes.Contains(context.SpeciesArchetype)))
        {
            reasons.Add(RuleReasons.For(RuleReasons.WrongArchetype, string.Join(", ", trait.AllowedArchetypes)));
        }

        // A portrait in the override list lifts the class restriction, which is how the game's own
        // psionic empires carry traits nominally reserved for the psionic species class.
        if (trait.AllowedSpeciesClasses.Count > 0 &&
            (context.SpeciesClass is null || !trait.AllowedSpeciesClasses.Contains(context.SpeciesClass)) &&
            (context.Portrait is null || !trait.PortraitOverride.Contains(context.Portrait)))
        {
            reasons.Add(RuleReasons.For(
                RuleReasons.WrongSpeciesClass,
                string.Join(", ", trait.AllowedSpeciesClasses)));
        }

        // Aquatic needs an ocean world, and a few others are tied to a homeworld in the same way.
        // Judged against what the origin actually gives the species, not what the design records.
        if (trait.AllowedPlanetClasses.Count > 0 &&
            context.EffectivePlanetClass is { } planet &&
            !trait.AllowedPlanetClasses.Contains(planet))
        {
            reasons.Add(RuleReasons.For(RuleReasons.WrongPlanetClass, string.Join(", ", trait.AllowedPlanetClasses)));
        }

        if (trait.AllowedOrigins.Count > 0 &&
            (context.Origin is null || !trait.AllowedOrigins.Contains(context.Origin)))
        {
            reasons.Add(RuleReasons.For(RuleReasons.WrongOrigin, string.Join(", ", trait.AllowedOrigins)));
        }

        if (context.Origin is { } origin && trait.ForbiddenOrigins.Contains(origin))
        {
            reasons.Add(RuleReasons.For(RuleReasons.ForbiddenByOrigin, origin));
        }

        if (trait.AllowedEthics.Count > 0 && !context.Ethics.Any(trait.AllowedEthics.Contains))
        {
            reasons.Add(RuleReasons.For(RuleReasons.WrongEthics, string.Join(", ", trait.AllowedEthics)));
        }

        if (context.Ethics.FirstOrDefault(trait.ForbiddenEthics.Contains) is { } forbidden)
        {
            reasons.Add(RuleReasons.For(RuleReasons.ForbiddenByEthics, forbidden));
        }

        if (trait.AllowedCivics.Count > 0 && !context.Civics.Any(trait.AllowedCivics.Contains))
        {
            reasons.Add(RuleReasons.For(RuleReasons.WrongCivics, string.Join(", ", trait.AllowedCivics)));
        }

        foreach (var opposite in trait.Opposites.Where(context.Traits.Contains))
        {
            reasons.Add(RuleReasons.For(RuleReasons.Opposite, opposite));
        }

        if (!ignoreBudget)
        {
            if (budget.Points.Remaining < trait.Cost)
            {
                reasons.Add(RuleReasons.NotEnoughPoints);
            }

            if (trait.Cost != 0 && budget.Picks.Remaining < 1)
            {
                reasons.Add(RuleReasons.NoPicksLeft);
            }
        }

        return reasons;
    }

    /// <summary>
    /// Whether a trait belongs in this species' list at all. The game shows a biological species
    /// biological traits, not the machine ones it could never take.
    /// </summary>
    private static bool IsTraitRelevant(TraitDefinition trait, DesignContext context) =>
        trait.AllowedArchetypes.Count == 0 ||
        context.SpeciesArchetype is null ||
        trait.AllowedArchetypes.Contains(context.SpeciesArchetype);

    /// <param name="whenTaken">
    /// The empire as it would be at the moment this option is taken, where that is not the empire
    /// the plan ends with. Whether an option is ever possible and whether it is possible yet are
    /// different questions, and a plan is the one place they come apart.
    /// </param>
    private IReadOnlyList<OptionState> Options<T>(
        IEnumerable<T> items,
        Func<T, string> key,
        Func<T, Requirement> visibility,
        Func<T, Requirement> availability,
        DesignContext context,
        DesignContext? whenTaken = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new List<OptionState>();

        foreach (var item in items)
        {
            var visible = _evaluator.Evaluate(visibility(item), context);
            var enabled = _evaluator.Evaluate(availability(item), whenTaken ?? context);

            options.Add(new OptionState(
                key(item),
                visible.Passed,
                visible.Passed && enabled.Passed,
                enabled.Passed ? [] : enabled.Reasons));
        }

        return options;
    }

    private void Check(
        ValidationArea area,
        string key,
        Requirement requirement,
        string description,
        DesignContext context,
        List<ValidationProblem> problems)
    {
        var verdict = _evaluator.Evaluate(requirement, context);

        if (!verdict.Passed)
        {
            problems.Add(new ValidationProblem(area, key, $"{{0}} {description}.", verdict.Reasons)
            {
                Arguments = [key],
            });
        }
    }

    /// <summary>
    /// The wording this empire is shown for an option, where a swap puts one in place of its own.
    /// </summary>
    /// <remarks>
    /// The first whose condition holds, which is how the game reads them - they are written as
    /// alternatives, one per kind of empire, and a tradition with three of them has one for the
    /// hive, one for the machine and one for everybody else. An empire matching none keeps the
    /// option's own name and description, which is the ordinary case.
    ///
    /// Settled and satisfied, not merely permitted. Everywhere else an unread condition lets the
    /// option through, because hiding something the player could have had is the worse mistake -
    /// here the worse mistake is the other way round. Four swaps turn on being in a federation,
    /// which is a thing no design is and no design can rule out either, and read permissively they
    /// would have renamed those options for every empire in the game.
    /// </remarks>
    public OptionVariant? VariantOf(IReadOnlyList<OptionVariant> variants, DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var variant in variants)
        {
            if (_evaluator.CanDecide(variant.When, context) &&
                _evaluator.IsSatisfied(variant.When, context))
            {
                return variant;
            }
        }

        return null;
    }

    private static Requirement Combine(Requirement first, Requirement second) =>
        new AllRequirement([first, second]);

    private SpeciesClassDefinition? SpeciesClassOf(DesignContext context) =>
        context.SpeciesClass is { } key
            ? _database.SpeciesClasses.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal))
            : null;

    private CivicDefinition? OriginOf(DesignContext context) =>
        context.Origin is { } key && _civics.TryGetValue(key, out var origin) && origin.IsOrigin ? origin : null;

    /// <summary>The selected civics together with the origin, which behaves like one.</summary>
    private IEnumerable<CivicDefinition> SelectedCivicsAndOrigin(DesignContext context)
    {
        foreach (var key in context.Civics)
        {
            if (_civics.TryGetValue(key, out var civic))
            {
                yield return civic;
            }
        }

        if (OriginOf(context) is { } origin)
        {
            yield return origin;
        }
    }
}
