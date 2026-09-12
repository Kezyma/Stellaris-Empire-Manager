using Sem.Designs;
using Sem.GameData;

namespace Sem.Rules;

/// <summary>
/// Judging a finished design: everything the game would refuse, and why.
/// </summary>
/// <remarks>
/// The other half of these rules decides what may be chosen; this one decides whether what
/// was chosen holds together. They share a database and a requirement evaluator and nothing
/// else, and at two thousand lines in one file the seam between them was a banner comment.
/// </remarks>
public sealed partial class EmpireRules
{
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

        // Asked of the context rather than derived again: CreateContext settled this before the
        // validator ever saw it, and deriving it a second time was the single most expensive thing
        // a validation did.
        if (context.Government is null)
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

        foreach (var key in context.RulerTraits)
        {
            if (_database.Trait(key) is not { Kind: TraitKind.StartingRuler } trait)
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
            if (_database.Trait(key) is not { } trait)
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

        foreach (var key in context.Ethics.Where(e => _database.Ethic(e) is null))
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
                     .Select(e => _database.Ethic(e))
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

        var authority = _database.Authority(key);

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
            if (_database.Civic(key) is not { } civic || civic.IsOrigin)
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

        if (_database.Civic(key) is not { } origin || !origin.IsOrigin)
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

        // A nomadic empire lives aboard an arkship whatever its design records. The game's own
        // nomadic empire writes pc_ark and starts there; one written with a planet still loads and
        // still starts there, so there is nothing here to refuse.
        //
        // Nor anything to say. This used to warn that the recorded world was being ignored, on the
        // reasoning that the player should know the toggle had done it. But the homeworld is not
        // theirs to change while the toggle is on - it is ignored whatever they set it to - so the
        // warning named a planet class at them and offered nothing to do about it, which is the
        // definition of noise on a panel whose other entries are all actionable.
        if (context.IsNomadic && HasPlanetClass(Arkship))
        {
            return;
        }

        // An origin that supplies its own homeworld simply overrides whatever the design recorded.
        // The game loads such a design and uses the origin's world, so this is worth mentioning
        // but is not a reason to reject the empire.
        if (OriginOf(context) is { } origin &&
            (origin.HabitabilityPreference ?? origin.StartingColony) is { Length: > 0 } imposed)
        {
            // Only where the design could have carried the origin's world itself. Where it could
            // not - a tomb world, a habitat, a relic world, none of them a class the designer
            // offers - the design is meant to hold an ordinary world and the game changes it on the
            // way in, so the two disagreeing is the arrangement working rather than a mistake.
            if (!string.Equals(key, imposed, StringComparison.Ordinal) &&
                GetSelectableHomeworlds(context).Contains(imposed, StringComparer.Ordinal))
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

    /// <summary>Weighs a list of options against a design, saying of each whether it may be taken.</summary>
    /// <remarks>
    /// <c>whenTaken</c> is the empire as it would be at the moment an option is taken, where that is
    /// not the empire the plan ends with. Whether an option is ever possible and whether it is
    /// possible yet are different questions, and a plan is the one place they come apart.
    /// </remarks>
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
            ? _database.SpeciesClass(key)
            : null;

    private CivicDefinition? OriginOf(DesignContext context) =>
        context.Origin is { } key && _database.Civic(key) is { } origin && origin.IsOrigin ? origin : null;

    /// <summary>The selected civics together with the origin, which behaves like one.</summary>
    private IEnumerable<CivicDefinition> SelectedCivicsAndOrigin(DesignContext context)
    {
        foreach (var key in context.Civics)
        {
            if (_database.Civic(key) is { } civic)
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
