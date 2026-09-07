using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction;

/// <summary>
/// Turns the game's conditions into <see cref="Requirement"/> trees the designer can evaluate.
/// </summary>
/// <remarks>
/// <para>
/// Stellaris writes conditions in two grammars that look alike but mean different things. The
/// requirements list, used by <c>potential</c> and <c>possible</c> on authorities, civics and
/// origins, groups checks under category names such as <c>ethics</c> and <c>civics</c>. Ordinary
/// triggers, used by <c>playable</c> and <c>selectable</c>, are the game's general condition
/// language. Both are compiled here into one shape.
/// </para>
/// <para>
/// Scripted triggers are inlined, so <c>has_utopia = yes</c> becomes a check on owning Utopia
/// rather than an opaque name. Anything not recognised becomes an
/// <see cref="UnknownRequirement"/> that defaults to permitting the option and is counted, so a
/// game patch introducing new script surfaces as a warning rather than as a wrong answer.
/// </para>
/// </remarks>
public sealed class RequirementCompiler
{
    private const int MaxTriggerDepth = 16;

    /// <summary>Category names the requirements-list grammar groups checks under.</summary>
    private static readonly Dictionary<string, SelectionCategory> Categories = new(StringComparer.Ordinal)
    {
        ["ethics"] = SelectionCategory.Ethics,
        ["authority"] = SelectionCategory.Authority,
        ["civics"] = SelectionCategory.Civics,
        ["origin"] = SelectionCategory.Origin,
        ["species_archetype"] = SelectionCategory.SpeciesArchetype,
        ["species_class"] = SelectionCategory.SpeciesClass,
        ["traits"] = SelectionCategory.Traits,
        ["preferred_planet_class"] = SelectionCategory.PreferredPlanetClass,
        ["graphical_culture"] = SelectionCategory.GraphicalCulture,
        ["country_type"] = SelectionCategory.CountryType,
    };

    /// <summary>Trigger names that ask directly about one of the player's selections.</summary>
    private static readonly Dictionary<string, SelectionCategory> SelectionTriggers = new(StringComparer.Ordinal)
    {
        ["has_ethic"] = SelectionCategory.Ethics,
        ["has_authority"] = SelectionCategory.Authority,
        ["has_valid_civic"] = SelectionCategory.Civics,
        ["has_civic"] = SelectionCategory.Civics,
        ["has_origin"] = SelectionCategory.Origin,
        ["has_trait"] = SelectionCategory.Traits,
        ["has_species_class"] = SelectionCategory.SpeciesClass,

        // Answerable because a plan answers them. Both were unreadable while an empire was only a
        // design - nothing in a design says which perks a game would grant - and both become
        // ordinary questions once the plan says which are meant to be taken.
        ["has_ascension_perk"] = SelectionCategory.AscensionPerk,
        ["has_tradition"] = SelectionCategory.TraditionTree,

        // The same question with a different name, and the commoner of the two inside effect
        // conditions: thirty-seven modifier groups turn on it, and unread they were shown as
        // conditions on a game in progress rather than as something the plan already answers.
        ["has_active_tradition"] = SelectionCategory.TraditionTree,

        // The founder's class, which a design states outright. It was the one condition in the whole
        // extraction asking something the design knows and being told nobody knew.
        ["is_species_class"] = SelectionCategory.SpeciesClass,
    };

    /// <summary>
    /// Triggers asking how many of something has been taken, and what they are counting.
    /// </summary>
    /// <remarks>
    /// One of them, in an unmodified game: the twenty-five perks gated on how many perks come
    /// before them, which is what makes a perk unable to be first. Its obvious sibling,
    /// <c>num_tradition_categories</c>, is deliberately absent - see
    /// <see cref="DesignPredicates.UnknowableWhenPlanning"/> for why a plan cannot answer it.
    /// </remarks>
    private static readonly Dictionary<string, SelectionCategory> CountTriggers = new(StringComparer.Ordinal)
    {
        ["num_ascension_perks"] = SelectionCategory.AscensionPerk,
    };

    /// <summary>How the game writes each comparison.</summary>
    private static readonly Dictionary<string, CountComparison> Comparisons = new(StringComparer.Ordinal)
    {
        [">"] = CountComparison.Above,
        ["<"] = CountComparison.Below,
        [">="] = CountComparison.AtLeast,
        ["<="] = CountComparison.AtMost,
        ["="] = CountComparison.Exactly,
        ["=="] = CountComparison.Exactly,
    };

    private readonly Dictionary<string, CwBlock> _scriptedTriggers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _unrecognised = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _unrecognisedInEffects = new(StringComparer.Ordinal);

    /// <summary>Conditions that were not recognised, and how often each appeared.</summary>
    public IReadOnlyDictionary<string, int> Unrecognised => _unrecognised;

    /// <summary>
    /// Conditions on modifiers that were not recognised.
    /// </summary>
    /// <remarks>
    /// Kept apart from the conditions that gate an option, because they mean something different. A
    /// gating condition the compiler cannot read is a defect: an option may be offered that the game
    /// would refuse. A condition on a modifier is usually about the state of a game in progress —
    /// whether a tradition has been adopted, whether a planet exists — which cannot be known while
    /// an empire is only being designed, and which the designer shows as conditional rather than
    /// pretending to resolve.
    /// </remarks>
    public IReadOnlyDictionary<string, int> UnrecognisedInEffects => _unrecognisedInEffects;

    /// <summary>
    /// Whether unrecognised conditions are currently being attributed to modifiers.
    /// </summary>
    private bool _compilingEffects;

    /// <summary>
    /// Whether what is being compiled belongs to a plan rather than to a design.
    /// </summary>
    /// <remarks>
    /// The same condition means different things in the two places, which is why this exists rather
    /// than one answer being chosen for both. See
    /// <see cref="DesignPredicates.UnknowableWhenPlanning"/>.
    /// </remarks>
    private bool _compilingPlan;

    /// <summary>
    /// Whether what is being compiled is a tradition's own gate on being adopted.
    /// </summary>
    /// <remarks>
    /// The one place a flag naming an ascension may stand for the perk that leads to it. See
    /// <see cref="DesignPredicates.PerkBehindCountryFlag"/> for why the substitution is sound there
    /// and wrong everywhere else.
    /// </remarks>
    private bool _compilingAdoptionGate;

    /// <summary>
    /// Where the game's <c>@</c> variables are read from, so a threshold written as
    /// <c>@max_tradition_trees</c> compiles to the seven it stands for.
    /// </summary>
    private ScriptLoader? _loader;

    /// <summary>
    /// Compiles a condition that decides when a modifier applies, rather than one that decides
    /// whether an option may be chosen.
    /// </summary>
    public Requirement CompileEffectCondition(CwBlock? block)
    {
        var wasCompilingEffects = _compilingEffects;
        _compilingEffects = true;

        try
        {
            return CompileTrigger(block);
        }
        finally
        {
            _compilingEffects = wasCompilingEffects;
        }
    }

    /// <summary>
    /// Compiles a condition on something a plan names - an ascension perk, a tradition tree, or
    /// whether a civic could be taken on later - rather than on something a design holds now.
    /// </summary>
    /// <remarks>
    /// The difference is what happens to conditions about a game already under way. A design has
    /// researched nothing and joined nothing, so those are false for it. A plan describes an empire
    /// decades in, and answering false there does not restrict the plan, it forbids it: half the
    /// perks worth planning are gated on a technology. See
    /// <see cref="DesignPredicates.UnknowableWhenPlanning"/> for which ones, and for the one that
    /// is deliberately left out.
    /// </remarks>
    public Requirement CompilePlanRequirementsList(CwBlock? block) =>
        AsPlan(() => CompileRequirementsList(block));

    /// <summary>Compiles a trigger belonging to a plan, on the terms above.</summary>
    public Requirement CompilePlanTrigger(CwBlock? block) => AsPlan(() => CompileTrigger(block));

    /// <summary>
    /// Compiles what a tradition asks before it may be adopted.
    /// </summary>
    /// <remarks>
    /// A plan's condition, and the only one where a flag naming an ascension is read as the perk
    /// behind it. Six trees state their entry that way and were open to everyone until it was.
    /// </remarks>
    public Requirement CompileAdoptionGate(CwBlock? block) =>
        AsPlan(() => AsAdoptionGate(() => CompileTrigger(block)));

    private Requirement AsAdoptionGate(Func<Requirement> compile)
    {
        var was = _compilingAdoptionGate;
        _compilingAdoptionGate = true;

        try
        {
            return compile();
        }
        finally
        {
            _compilingAdoptionGate = was;
        }
    }

    private Requirement AsPlan(Func<Requirement> compile)
    {
        var was = _compilingPlan;
        _compilingPlan = true;

        try
        {
            return compile();
        }
        finally
        {
            _compilingPlan = was;
        }
    }

    /// <summary>Scripted triggers available for inlining.</summary>
    public IReadOnlyDictionary<string, CwBlock> ScriptedTriggers => _scriptedTriggers;

    /// <summary>Loads the game's scripted triggers so they can be inlined during compilation.</summary>
    public void LoadScriptedTriggers(ScriptLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        // Kept for its variables as well as its triggers. Both are the same thing from here: script
        // written somewhere else that a condition refers to by name.
        _loader = loader;

        foreach (var entry in loader.LoadEntries("common/scripted_triggers"))
        {
            // Later definitions win, matching the game's own load order.
            _scriptedTriggers[entry.Key] = entry.Body;
        }
    }

    /// <summary>
    /// Compiles a <c>potential</c> or <c>possible</c> block, where checks are grouped under
    /// category names.
    /// </summary>
    public Requirement CompileRequirementsList(CwBlock? block)
    {
        if (block is null)
        {
            return new AlwaysRequirement(true);
        }

        var items = CompileRequirementsListItems(block, out var guard, out var text);
        var combined = WithText(Combine(items), text);

        return guard is null
            ? combined
            : new AnyRequirement([
                new NotRequirement(guard),
                new AllRequirement([guard, combined]),
            ]);
    }

    /// <summary>
    /// Compiles the entries of a requirements list without combining them, so the caller can
    /// decide whether they are joined by "and" or by "or".
    /// </summary>
    private List<Requirement> CompileRequirementsListItems(
        CwBlock block,
        out Requirement? guard,
        out string? text)
    {
        var items = new List<Requirement>();
        guard = null;
        text = null;

        foreach (var node in block.Nodes)
        {
            if (node.Key is not { } key)
            {
                continue;
            }

            if (key == "text")
            {
                text = node.ScalarValue;
                continue;
            }

            // A limit guards its siblings: they apply only when it holds. The MACHINE species
            // class uses this to state one set of rules with the Machine Age owned and a stricter
            // set without it. Treating the guard as merely another requirement would apply both
            // sets at once and wrongly block individual machine empires.
            if (key == "limit" && node.Block is not null)
            {
                guard = CompileTrigger(node.Block);
                continue;
            }

            if (Categories.TryGetValue(key, out var category) && node.Block is { } categoryBlock)
            {
                items.Add(CompileCategory(category, categoryBlock));
                continue;
            }

            switch (key)
            {
                case "always":
                    items.Add(new AlwaysRequirement(node.ScalarValue == "yes"));
                    break;

                // An AND is what a requirements list already means, so it just nests one.
                case "AND" when node.Block is not null:
                    items.Add(CompileRequirementsList(node.Block));
                    break;

                case "OR" when node.Block is not null:
                    items.Add(Group(node.Block, children => new AnyRequirement(children)));
                    break;

                case "NOT" when node.Block is not null:
                    items.Add(Group(node.Block, children => new NotRequirement(Combine(children))));
                    break;

                // NOR means none of them, which is not the same as "not all of them".
                case "NOR" when node.Block is not null:
                    items.Add(Group(node.Block, children => new NotRequirement(new AnyRequirement(children))));
                    break;

                // A bare scalar such as is_nomadic = no, which the 4.x grammar allows here.
                default:
                    items.Add(node.ScalarValue is { } value
                        ? new FieldRequirement(key, value)
                        : RecordUnrecognised(key));
                    break;
            }
        }

        return items;

        Requirement Group(CwBlock nested, Func<List<Requirement>, Requirement> combine)
        {
            var children = CompileRequirementsListItems(nested, out var nestedGuard, out var nestedText);
            var result = WithText(combine(children), nestedText);

            return nestedGuard is null
                ? result
                : new AnyRequirement([
                    new NotRequirement(nestedGuard),
                    new AllRequirement([nestedGuard, result]),
                ]);
        }
    }

    /// <summary>
    /// Compiles an ordinary trigger block, as used by <c>playable</c>, <c>selectable</c> and
    /// <c>randomized</c>.
    /// </summary>
    public Requirement CompileTrigger(CwBlock? block) => CompileTrigger(block, depth: 0);

    /// <summary>
    /// Compiles a trigger written as a bare name, which is how prescripted empires spell
    /// <c>playable = has_megacorp</c>.
    /// </summary>
    public Requirement CompileTriggerByName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new AlwaysRequirement(true);
        }

        return CompileNamedTrigger(name, expected: true, depth: 0);
    }

    private Requirement CompileTrigger(CwBlock? block, int depth)
    {
        if (block is null)
        {
            return new AlwaysRequirement(true);
        }

        // A chain this deep is permitted, like anything else the compiler cannot read to the end of
        // — but counted, so it shows up in the unrecognised list rather than becoming an
        // "always allowed" that nothing can tell apart from a rule that really is.
        if (depth > MaxTriggerDepth)
        {
            return RecordUnrecognised("(scripted trigger nested too deeply)");
        }

        var items = new List<Requirement>();

        foreach (var node in block.Nodes)
        {
            if (node.Key is not { } key)
            {
                continue;
            }

            items.Add(CompileTriggerEntry(key, node, depth));
        }

        return Combine(items);
    }

    private Requirement CompileTriggerEntry(string key, CwNode node, int depth)
    {
        switch (key)
        {
            case "always":
                return new AlwaysRequirement(node.ScalarValue == "yes");

            case "host_has_dlc" or "local_has_dlc" when node.ScalarValue is { } dlc:
                return new DlcRequirement(dlc);

            // The government is not a thing the design holds but a thing derived from what it holds,
            // so it is asked for as a field rather than as a selection. Almost every empire name the
            // game can generate is gated on one of these.
            case "has_government" when node.ScalarValue is { } government:
                return new FieldRequirement("government", government);

            case "OR" when node.Block is not null:
                return new AnyRequirement(CompileTriggerChildren(node.Block, depth));

            case "AND" when node.Block is not null:
                return CompileTrigger(node.Block, depth + 1);

            case "NOT" when node.Block is not null:
                return new NotRequirement(CompileTrigger(node.Block, depth + 1));

            case "NOR" when node.Block is not null:
                return new NotRequirement(new AnyRequirement(CompileTriggerChildren(node.Block, depth)));

            case "NAND" when node.Block is not null:
                return new NotRequirement(CompileTrigger(node.Block, depth + 1));

            // The opposite of custom_tooltip: a rule the game checks but does not explain, used
            // where the reason would give away something the player is not meant to see yet. It
            // still decides, so it compiles like an AND and only its wording is missing.
            case "hidden_trigger" when node.Block is not null:
                return CompileTrigger(node.Block, depth + 1);

            // Wording, not conditions. custom_tooltip pulls its own fail_text out by name because
            // it needs the value, and a civic's modification block does the same with its
            // moddable_conditions_custom_tooltip; these are the same keys met anywhere else,
            // including the success_text that sits beside a fail_text and used to be compiled as
            // though it were a rule.
            case "success_text" or "fail_text" or "moddable_conditions_custom_tooltip":
                return new AlwaysRequirement(true);

            // Triggers that name a scope rather than a condition; the design is always the country.
            // owner_species among them: the species a design's questions are about is its founder,
            // which is the species this context already answers for.
            case "country" or "owner" or "this" or "root" or "from" or "owner_species"
                when node.Block is not null:
                return CompileTrigger(node.Block, depth + 1);

            // "custom_tooltip = { fail_text = "..." <conditions> }" is the game giving one rule a
            // sentence of its own. The conditions inside are the rule and the text is what to say
            // when it fails, which is exactly the shape a requirement already has - so unwrapping it
            // is what turns almost every ascension perk's exclusions from unreadable into enforced.
            case "custom_tooltip" when node.Block is not null:
                return WithText(
                    CompileTriggerExcluding(node.Block, "fail_text", depth + 1),
                    node.Block.Nodes.FirstOrDefault(n => n.Key == "fail_text")?.ScalarValue);

            // The other half of an if, which the game uses to say "and otherwise, this instead".
            // Read permissively: an else branch is an alternative rather than a requirement, and
            // treating one as a rule would block an option the game would allow.
            case "else" or "else_if" when node.Block is not null:
                return new AlwaysRequirement(true);

            // An empire being designed is always an ordinary playable country.
            case "is_country_type" when node.ScalarValue is { } countryType:
                return new AlwaysRequirement(countryType == "default");

            // "if = { limit = { condition } rest }" means the rest applies only when the limit
            // holds, so it is satisfied either by the limit failing or by everything holding.
            case "if" when node.Block is not null:
            {
                var limit = CompileTrigger(node.Block.GetBlock("limit"), depth + 1);
                var body = CompileTriggerExcluding(node.Block, "limit", depth + 1);

                return new AnyRequirement([
                    new NotRequirement(limit),
                    new AllRequirement([limit, body]),
                ]);
            }
        }

        if (SelectionTriggers.TryGetValue(key, out var category) && node.ScalarValue is { } selection)
        {
            return new SelectionRequirement(category, selection);
        }

        // A flag that only one ascension perk leads to, asked by the gate that wants it. Read here
        // because the condition still has its value: the two branches below see the key alone, and
        // so give up on every flag equally.
        if (_compilingAdoptionGate &&
            string.Equals(key, "has_country_flag", StringComparison.Ordinal) &&
            node.ScalarValue is { } flag &&
            DesignPredicates.PerkBehindCountryFlag.TryGetValue(flag, out var behind))
        {
            return new SelectionRequirement(SelectionCategory.AscensionPerk, behind);
        }

        if (node.ScalarValue is { } scalar && scalar is "yes" or "no")
        {
            return CompileNamedTrigger(key, expected: scalar == "yes", depth);
        }

        // A count of what has been taken so far. In a game this asks about the past; in a plan it
        // asks about position, because the thing in the fourth place has three before it.
        if (CountTriggers.TryGetValue(key, out var counted) && CompileCount(counted, node) is { } count)
        {
            return count;
        }

        // Something a running game would know and this empire will find out. Only while compiling a
        // plan, and only as an assumption rather than a constant, so that a negation stays as
        // permissive as the condition it wraps.
        if (_compilingPlan && DesignPredicates.UnknowableWhenPlanning.Contains(key))
        {
            return new UnknownRequirement(key);
        }

        // Conditions naming a value rather than answering yes or no, such as
        // "has_country_flag = some_flag", which the designer can still decide.
        if (DesignPredicates.NeverTrueInDesigner.Contains(key))
        {
            return new AlwaysRequirement(false);
        }

        if (DesignPredicates.AssumedTrueInDesigner.Contains(key))
        {
            return new AlwaysRequirement(true);
        }

        return RecordUnrecognised(key);
    }

    /// <summary>
    /// Compiles "how many of these do I have" against a number, or nothing when either half of it
    /// cannot be read.
    /// </summary>
    /// <remarks>
    /// The threshold is often a variable rather than a literal - <c>@max_tradition_trees</c>, which
    /// the game's own comment says is there "in case modders increase the limit" - so it is resolved
    /// through the loader rather than parsed. Returning nothing when it will not resolve sends the
    /// condition on to be recorded as unrecognised, which is the visible failure this file prefers
    /// to a silently wrong number.
    /// </remarks>
    private CountRequirement? CompileCount(SelectionCategory of, CwNode node)
    {
        if (node.Operator is not { } written || !Comparisons.TryGetValue(written, out var comparison))
        {
            return null;
        }

        return _loader?.ResolveInt(node.ScalarValue) is { } value
            ? new CountRequirement(of, comparison, value)
            : null;
    }

    /// <summary>Compiles every entry of a block except one, used for the body of an if.</summary>
    private Requirement CompileTriggerExcluding(CwBlock block, string excludedKey, int depth)
    {
        var items = new List<Requirement>();

        foreach (var node in block.Nodes)
        {
            if (node.Key is { } key && key != excludedKey)
            {
                items.Add(CompileTriggerEntry(key, node, depth));
            }
        }

        return Combine(items);
    }

    private List<Requirement> CompileTriggerChildren(CwBlock block, int depth)
    {
        var items = new List<Requirement>();

        foreach (var node in block.Nodes)
        {
            if (node.Key is { } key)
            {
                items.Add(CompileTriggerEntry(key, node, depth + 1));
            }
        }

        return items;
    }

    /// <summary>
    /// Resolves a condition written as a name, by inlining the scripted trigger it refers to or
    /// recognising it as something the designer can answer itself.
    /// </summary>
    private Requirement CompileNamedTrigger(string name, bool expected, int depth)
    {
        Requirement result;

        // Asked as "is_subject = no" rather than by naming a value, and answered the same way: a
        // plan does not know, and an unknown survives the negation applied at the end of this method
        // where a constant would flip into a block.
        if (_compilingPlan && DesignPredicates.UnknowableWhenPlanning.Contains(name))
        {
            result = new UnknownRequirement(name);
        }
        else if (DesignPredicates.NeverTrueInDesigner.Contains(name))
        {
            result = new AlwaysRequirement(false);
        }
        else if (DesignPredicates.AssumedTrueInDesigner.Contains(name))
        {
            result = new AlwaysRequirement(true);
        }
        else if (DesignPredicates.All.Contains(name))
        {
            result = new PredicateRequirement(name);
        }
        else if (depth < MaxTriggerDepth && _scriptedTriggers.TryGetValue(name, out var body))
        {
            result = CompileTrigger(body, depth + 1);
        }
        else
        {
            // Unknown conditions default to permitting the option: hiding something the player
            // should be able to pick is worse than showing something they cannot.
            result = RecordUnrecognised(name);
        }

        return expected ? result : new NotRequirement(result);
    }

    /// <summary>
    /// Compiles one category group, where bare <c>value</c> entries are all required and
    /// <c>OR</c>, <c>NOT</c> and <c>NOR</c> qualify them.
    /// </summary>
    private Requirement CompileCategory(SelectionCategory category, CwBlock block)
    {
        var items = new List<Requirement>();
        string? text = null;

        foreach (var node in block.Nodes)
        {
            switch (node.Key)
            {
                case "value" when node.ScalarValue is { } value:
                    items.Add(new SelectionRequirement(category, value));
                    break;

                case "text":
                    text = node.ScalarValue;
                    break;

                case "OR" when node.Block is not null:
                    items.Add(WithText(
                        new AnyRequirement(CategoryValues(category, node.Block)),
                        TextOf(node.Block)));
                    break;

                case "AND" when node.Block is not null:
                    items.Add(WithText(
                        new AllRequirement(CategoryValues(category, node.Block)),
                        TextOf(node.Block)));
                    break;

                case "NOT" when node.Block is not null:
                    items.Add(WithText(
                        new NotRequirement(new AllRequirement(CategoryValues(category, node.Block))),
                        TextOf(node.Block)));
                    break;

                case "NOR" when node.Block is not null:
                    items.Add(WithText(
                        new NotRequirement(new AnyRequirement(CategoryValues(category, node.Block))),
                        TextOf(node.Block)));
                    break;

                // Everywhere else in this compiler counts what it cannot read. This alone had no
                // default, so a key the game adds inside a category block vanished with no warning
                // and no entry in the unrecognised list — the one place a new rule could be dropped
                // and leave no trace of having been.
                default:
                    if (node.Key is { Length: > 0 } unread)
                    {
                        items.Add(RecordUnrecognised($"{category.ToString().ToLowerInvariant()}.{unread}"));
                    }

                    break;
            }
        }

        return WithText(Combine(items), text);
    }

    /// <summary>
    /// The selections named inside a category's <c>OR</c>, <c>AND</c>, <c>NOT</c> or <c>NOR</c>.
    /// </summary>
    /// <remarks>
    /// A group nested inside one of those is compiled rather than skipped. Reading only the direct
    /// <c>value</c> children collapsed <c>NOT = { OR = { value value } }</c> to a negation of
    /// nothing, and a negation of nothing is always false — so the clause refused the option
    /// outright, silently. Vanilla has no such nesting today, which is why it went unseen.
    /// </remarks>
    private List<Requirement> CategoryValues(SelectionCategory category, CwBlock block)
    {
        var items = new List<Requirement>();

        foreach (var node in block.Nodes)
        {
            if (node.Key == "value" && node.ScalarValue is { } value)
            {
                items.Add(new SelectionRequirement(category, value));
            }
            else if (node.Key is "OR" or "AND" or "NOT" or "NOR" && node.Block is not null)
            {
                items.Add(CompileCategory(category, WrapAsCategoryGroup(node)));
            }
        }

        return items;
    }

    /// <summary>Puts one nested group back into the shape <see cref="CompileCategory"/> reads.</summary>
    private static CwBlock WrapAsCategoryGroup(CwNode group)
    {
        var block = new CwBlock();
        block.Add(CwNode.Assignment(group.Key!, group.Block!));
        return block;
    }

    private static string? TextOf(CwBlock block) =>
        block.Nodes.FirstOrDefault(n => n.Key == "text")?.ScalarValue;

    private static Requirement WithText(Requirement requirement, string? text) =>
        text is null ? requirement : requirement with { FailureText = text };

    /// <summary>Collapses a list of conditions, avoiding a pointless wrapper around one item.</summary>
    private static Requirement Combine(List<Requirement> items) => items.Count switch
    {
        0 => new AlwaysRequirement(true),
        1 => items[0],
        _ => new AllRequirement(items),
    };

    private UnknownRequirement RecordUnrecognised(string name)
    {
        var into = _compilingEffects ? _unrecognisedInEffects : _unrecognised;
        into[name] = into.GetValueOrDefault(name) + 1;
        return new UnknownRequirement(name);
    }
}
