using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads the personalities the game gives its AI empires.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as a government - a condition and a weight - and read the same way, with one
/// difference that matters: these weights are added rather than multiplied, which the game states
/// at the top of its own file. So a personality's weight is its base plus whichever of its
/// additions apply, and the game then draws one at random among everything the empire allows.
/// </para>
/// <para>
/// Twenty of the fifty-one belong to fallen empires, pre-FTL societies and the like, and are ruled
/// out for nothing: they ask <c>is_country_type</c> for something other than <c>default</c>, which
/// compiles to a constant against a design.
/// </para>
/// </remarks>
internal static class PersonalityExtractor
{
    /// <summary>Where the game keeps them - not <c>ai_personalities</c>, which does not exist.</summary>
    private const string Root = "common/personalities";

    /// <summary>Keys inside a weight modifier that are arithmetic rather than conditions.</summary>
    private static readonly string[] Arithmetic = ["weight", "add", "factor", "mult", "base"];

    /// <summary>
    /// How a personality carries itself, in the game's own grouping.
    /// </summary>
    /// <remarks>
    /// Taken from the comment block the files open with, which is the game explaining each of these
    /// at length - what aggressiveness does to a war declaration and to the share of a fleet
    /// committed, what trade willingness means at 1.0, what bravery picks a war target by. Listed
    /// rather than swept up, so a field the game adds arrives as a decision rather than as a column.
    /// </remarks>
    private static readonly string[] AttitudeFields =
    [
        "aggressiveness",
        "bravery",
        "combat_bravery",
        "trade_willingness",
        "military_spending",
        "colony_spending",
        "threat_modifier",
        "threat_others_modifier",
        "friction_modifier",
        "claims_modifier",
        "advanced_start_chance",
    ];

    /// <summary>What it will sign, each a number added directly to its chance of accepting.</summary>
    private static readonly string[] DiplomacyFields =
    [
        "federation_acceptance",
        "nap_acceptance",
        "commercial_pact_acceptance",
        "research_agreement_acceptance",
        "migration_pact_acceptance",
        "defensive_pact_acceptance",
        "loyalty_acceptance",
    ];

    /// <summary>And what it builds its ships out of, as the share of each it aims for.</summary>
    private static readonly string[] FleetFields = ["armor_ratio", "shields_ratio", "hull_ratio"];

    /// <summary>
    /// Reads the personalities, and how each of them plays.
    /// </summary>
    /// <param name="loader">The script loader.</param>
    /// <param name="requirements">The compiler for the conditions they state.</param>
    /// <param name="details">Filled with what the wiki's own file carries about each.</param>
    /// <returns>The personalities, for the database.</returns>
    public static List<PersonalityDefinition> Extract(
        ScriptLoader loader,
        RequirementCompiler requirements,
        List<PersonalityDetail> details)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(details);

        var results = new List<PersonalityDefinition>();
        var order = 0;

        foreach (var entry in loader.LoadDefinitions(Root))
        {
            var body = entry.Body;

            // Every personality has both. Anything with neither is not one - the files open with a
            // long comment block, and a stray entry would otherwise be read as a nameless empire
            // trait with no weight at all.
            if (body.GetBlock("allow") is null && body.GetBlock("weight_modifier") is null)
            {
                continue;
            }

            results.Add(new PersonalityDefinition(entry.Key, Weight(body, loader), order++)
            {
                Allow = requirements.CompileTrigger(body.GetBlock("allow")),
                Additions = Additions(body, loader, requirements),
            });

            details.Add(Detail(entry.Key, body, loader));
        }

        return results;
    }

    /// <summary>
    /// How one personality plays, for the wiki's own file.
    /// </summary>
    /// <remarks>
    /// Read alongside the database's own record rather than in a pass of its own, since both come
    /// out of the same block and the files are not small. Kept apart from it because an empire being
    /// designed has no AI and none of this belongs on a record every visitor fetches.
    /// </remarks>
    /// <param name="key">The personality.</param>
    /// <param name="body">Its block.</param>
    /// <param name="loader">The loader, for the <c>@</c> variables these use freely.</param>
    /// <returns>Its detail.</returns>
    private static PersonalityDetail Detail(string key, CwBlock body, ScriptLoader loader) =>
        new(key)
        {
            // Only the flags it answers yes to - see PersonalityDetail.Behaviours.
            Behaviours = body.GetBlock("behaviour") is { } behaviour
                ?
                [
                    .. behaviour.Nodes
                        .Where(n => n.Key is { Length: > 0 } && n.ScalarValue == "yes")
                        .Select(n => n.Key!),
                ]
                : [],

            Attitude = Numbers(body, loader, AttitudeFields),
            Diplomacy = Numbers(body, loader, DiplomacyFields),
            Fleet = Numbers(body, loader, FleetFields),
            Weapons = body.GetString("weapon_preferences"),
        };

    /// <summary>
    /// Whichever of a group of numeric fields a personality states.
    /// </summary>
    /// <remarks>
    /// Through the loader, because these are written as <c>@</c> variables as often as as numbers -
    /// the fallen empires share one set of values between four of them - and a raw read would leave
    /// a page saying "@fallen_aggressiveness".
    /// </remarks>
    /// <param name="body">The personality's block.</param>
    /// <param name="loader">The loader.</param>
    /// <param name="fields">The fields worth reading.</param>
    /// <returns>The ones it states, by field.</returns>
    private static Dictionary<string, double> Numbers(
        CwBlock body,
        ScriptLoader loader,
        IReadOnlyList<string> fields)
    {
        var found = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (loader.ResolveNumber(body.GetString(field)) is { } value)
            {
                found[field] = value;
            }
        }

        return found;
    }

    /// <summary>What a personality weighs before anything conditional is added to it.</summary>
    private static double Weight(CwBlock body, ScriptLoader loader) =>
        body.GetBlock("weight_modifier") is { } weight
            ? loader.ResolveNumber(weight.GetString("weight")) ?? 0
            : 0;

    /// <summary>
    /// What is added to that weight, and when.
    /// </summary>
    /// <remarks>
    /// The same shape the governments use, and read the same way: the arithmetic is taken out of a
    /// modifier block and whatever is left is the condition. Only the number differs - theirs
    /// multiplies and this one adds.
    /// </remarks>
    private static List<WeightFactor> Additions(
        CwBlock body,
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var found = new List<WeightFactor>();

        if (body.GetBlock("weight_modifier") is not { } weight)
        {
            return found;
        }

        foreach (var node in weight.Nodes)
        {
            if (node.Key != "modifier" || node.Block is not { } modifier)
            {
                continue;
            }

            var conditions = new CwBlock();

            foreach (var inner in modifier.Nodes)
            {
                if (inner.Key is null || !Arithmetic.Contains(inner.Key, StringComparer.Ordinal))
                {
                    conditions.Add(inner);
                }
            }

            found.Add(new WeightFactor(
                requirements.CompileTrigger(conditions),
                loader.ResolveNumber(modifier.GetString("add")) ?? 0));
        }

        return found;
    }
}
