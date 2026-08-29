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

    private readonly Dictionary<string, TraitDefinition> _traits =
        database.Traits.GroupBy(t => t.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    private readonly Dictionary<string, EthicDefinition> _ethics =
        database.Ethics.ToDictionary(e => e.Key, StringComparer.Ordinal);

    private readonly Dictionary<string, CivicDefinition> _civics =
        database.Civics.ToDictionary(c => c.Key, StringComparer.Ordinal);

    private readonly Dictionary<string, ArchetypeDefinition> _archetypes =
        database.Archetypes.ToDictionary(a => a.Key, StringComparer.Ordinal);

    /// <summary>The extracted game data being enforced.</summary>
    public GameDatabase Database => _database;

    /// <summary>Builds a context from a design.</summary>
    public DesignContext CreateContext(EmpireDesign design, IReadOnlySet<string>? ownedDlc = null) =>
        DesignContext.FromDesign(design, _database, ownedDlc);

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
            var pointsKey = $"{archetypeKey}_species_trait_points_add";
            var picksKey = $"{archetypeKey}_species_trait_picks_add";

            foreach (var civic in SelectedCivicsAndOrigin(context))
            {
                points += (int)civic.TraitBudgetModifiers.GetValueOrDefault(pointsKey);
                picks += (int)civic.TraitBudgetModifiers.GetValueOrDefault(picksKey);
            }
        }

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

    /// <summary>How many civics the empire has taken against how many it may.</summary>
    public Budget GetCivicsBudget(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Budget(context.Civics.Count, _database.Defines.CivicPoints);
    }

    // ---------------------------------------------------------------------------------------
    // Derived values
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Works out what the empire's government is called.
    /// </summary>
    /// <remarks>
    /// The government is not chosen. The game takes the highest-weighted type whose conditions the
    /// design meets, and settles ties by which was defined first.
    /// </remarks>
    public GovernmentTypeDefinition? DeriveGovernment(DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GovernmentTypeDefinition? best = null;

        foreach (var government in _database.GovernmentTypes)
        {
            if (!_evaluator.IsSatisfied(government.Possible, context))
            {
                continue;
            }

            if (best is null ||
                government.Weight > best.Weight ||
                (government.Weight == best.Weight && government.FileOrder < best.FileOrder))
            {
                best = government;
            }
        }

        return best;
    }

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
            forced.AddRange(civic.ForcedTraits.Select(t => new ForcedTrait(t, civic.Key, kind)));
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

        return HabitabilityTraitFor(context.EffectivePlanetClass);
    }

    /// <summary>
    /// The habitability preference a given world would give, whether or not it is the one chosen.
    /// </summary>
    /// <remarks>
    /// Asked of every world in the picker, so that hovering one says what living there does to a
    /// species — which is the only thing a planet class has to say for itself. The game writes no
    /// description for them at all.
    /// </remarks>
    public string? HabitabilityTraitFor(string? planetClassKey)
    {
        if (planetClassKey is not { Length: > 0 } planetClass)
        {
            return null;
        }

        if (Trait($"trait_{planetClass}_preference") is { } exact)
        {
            return exact;
        }

        var climate = _database.PlanetClasses
            .FirstOrDefault(p => string.Equals(p.Key, planetClass, StringComparison.Ordinal))?.Climate;

        return climate is { Length: > 0 } ? Trait($"trait_auto_{climate}_preference") : null;

        string? Trait(string key) =>
            _database.Traits.Any(t => string.Equals(t.Key, key, StringComparison.Ordinal)) ? key : null;
    }

    /// <summary>
    /// Whether a trait is one the homeworld decides rather than one the player picks.
    /// </summary>
    /// <remarks>
    /// Offering these would be offering a choice the game does not have. They are shown among what
    /// the species already has, so a player can see what it is suited to.
    /// </remarks>
    public static bool IsHabitabilityPreference(string traitKey) =>
        traitKey is { Length: > 0 } &&
        traitKey.EndsWith("_preference", StringComparison.Ordinal) &&
        (traitKey.StartsWith("trait_pc_", StringComparison.Ordinal) ||
         traitKey.StartsWith("trait_auto_", StringComparison.Ordinal));

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
    public IReadOnlyList<OptionState> GetSpeciesClassOptions(DesignContext context) =>
        Options(
            _database.SpeciesClasses.Where(c => !c.IsAppearanceOnly && HasPortraits(c.Key)),
            c => c.Key,
            c => c.Playable,
            c => c.Possible,
            context);

    private bool HasPortraits(string speciesClass) =>
        (_classesWithPortraits ??= [.. _database.PortraitSets
            .Select(s => s.SpeciesClass)
            .OfType<string>()]).Contains(speciesClass);

    private HashSet<string>? _classesWithPortraits;

    /// <summary>The authorities the player may choose from.</summary>
    public IReadOnlyList<OptionState> GetAuthorityOptions(DesignContext context) =>
        Options(
            _database.Authorities.Where(a => !a.AiOnly),
            a => a.Key,
            a => a.Playable,
            a => a.Possible,
            context);

    /// <summary>The origins the player may choose from.</summary>
    public IReadOnlyList<OptionState> GetOriginOptions(DesignContext context) =>
        Options(
            _database.Civics.Where(c => c.IsOrigin),
            c => c.Key,
            c => Combine(c.Playable, c.Potential),
            c => c.Possible,
            context);

    /// <summary>The civics the player may choose from.</summary>
    public IReadOnlyList<OptionState> GetCivicOptions(DesignContext context) =>
        Options(
            _database.Civics.Where(c => !c.IsOrigin),
            c => c.Key,
            c => Combine(c.Playable, c.Potential),
            c => c.Possible,
            context);

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

            // Opposing ethics share a category, and only one may be taken from each.
            var conflicting = context.Ethics.FirstOrDefault(
                e => _ethics.TryGetValue(e, out var taken) &&
                     string.Equals(taken.Category, ethic.Category, StringComparison.Ordinal));

            if (conflicting is not null)
            {
                reasons.Add(RuleReasons.For(RuleReasons.EthicGroupTaken, conflicting));
            }

            if (budget.Remaining < ethic.Cost)
            {
                reasons.Add(RuleReasons.NotEnoughEthicsPoints);
            }

            options.Add(new OptionState(ethic.Key, true, reasons.Count == 0, reasons, ethic.Cost));
        }

        return options;
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

        if (design is not null && RequiresSecondarySpecies(context) && design.SecondarySpecies is null)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.SecondarySpecies,
                context.Origin,
                "This origin needs a second species, and the design has none.",
                []));
        }

        return new ValidationReport(problems);
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

    private void ValidateTraits(DesignContext context, List<ValidationProblem> problems)
    {
        var budget = GetTraitBudget(context);

        if (budget.Points.IsOverspent)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Traits,
                null,
                $"Traits cost {budget.Points.Spent} points but only {budget.Points.Available} are available.",
                []));
        }

        if (budget.Picks.IsOverspent)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Traits,
                null,
                $"The species has {budget.Picks.Spent} traits but may have {budget.Picks.Available}.",
                []));
        }

        foreach (var key in context.Traits)
        {
            if (!_traits.TryGetValue(key, out var trait))
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Traits, key, $"'{key}' is not a trait this game defines.", []));
                continue;
            }

            foreach (var reason in TraitBlockers(trait, context, budget, ignoreBudget: true))
            {
                problems.Add(new ValidationProblem(
                    ValidationArea.Traits, key, $"'{key}' cannot be taken by this species.", [reason]));
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

        if (budget.IsOverspent)
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Ethics,
                null,
                $"Ethics cost {budget.Spent} points but only {budget.Available} are available.",
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
                $"Only one ethic may be taken from the '{group.Key}' group, but the empire has " +
                $"{string.Join(" and ", group.Select(e => e.Key))}.",
                []));
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
                    $"This origin starts the empire on '{imposed}', so the '{key}' homeworld is ignored.",
                    [],
                    ValidationSeverity.Warning));
            }

            return;
        }

        if (!GetHomeworldOptions(context).Contains(key, StringComparer.Ordinal))
        {
            problems.Add(new ValidationProblem(
                ValidationArea.Homeworld,
                key,
                $"'{key}' is not a homeworld this empire can start on.",
                []));
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

    private IReadOnlyList<OptionState> Options<T>(
        IEnumerable<T> items,
        Func<T, string> key,
        Func<T, Requirement> visibility,
        Func<T, Requirement> availability,
        DesignContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new List<OptionState>();

        foreach (var item in items)
        {
            var visible = _evaluator.Evaluate(visibility(item), context);
            var enabled = _evaluator.Evaluate(availability(item), context);

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
            problems.Add(new ValidationProblem(area, key, $"'{key}' {description}.", verdict.Reasons));
        }
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
