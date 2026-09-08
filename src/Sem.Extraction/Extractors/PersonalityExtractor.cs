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

    public static List<PersonalityDefinition> Extract(ScriptLoader loader, RequirementCompiler requirements)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(requirements);

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
        }

        return results;
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
