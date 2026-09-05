namespace Sem.Designs;

/// <summary>
/// How a player means to play an empire: the ascension path, the tradition trees and the ascension
/// perks they intend to take.
/// </summary>
/// <remarks>
/// <para>
/// None of this is part of a design as the game understands one. The game has no field for any of
/// it, and an invented field would not survive: it rewrites every design from its own model when it
/// closes, and anything the model has no slot for is gone. So a plan is written as prose into one of
/// the two biographies, which the game does model and therefore does keep - and which it shows to
/// the player, so the field earns its keep rather than being quietly filled with machine text.
/// </para>
/// <para>
/// Keys, not names. What is written into a biography is the player's own language, and what is
/// stored here is what the game calls things - so the same plan reads correctly whatever language it
/// was written in, and a plan does not change meaning because the reader's game is set to another.
/// </para>
/// </remarks>
public sealed record EmpirePlan(
    PlanPath Path,
    IReadOnlyList<string> Trees,
    IReadOnlyList<string> Perks)
{
    /// <summary>A plan that says nothing, which is what an empire without one has.</summary>
    public static EmpirePlan Empty { get; } = new(PlanPath.Unset, [], []);

    /// <summary>Whether anything has actually been planned.</summary>
    public bool Any => Path is not PlanPath.Unset || Trees.Count > 0 || Perks.Count > 0;

    /// <summary>
    /// Two plans are the same when they say the same thing.
    /// </summary>
    /// <remarks>
    /// Written out because a record compares a list by reference, so two plans naming the same
    /// trees in the same order would otherwise be different for no reason a reader could see - and
    /// the question this type exists to answer, "has the plan changed", would always be yes.
    /// </remarks>
    public bool Equals(EmpirePlan? other) =>
        other is not null
        && Path == other.Path
        && Trees.SequenceEqual(other.Trees, StringComparer.Ordinal)
        && Perks.SequenceEqual(other.Perks, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var code = new HashCode();
        code.Add(Path);

        foreach (var key in Trees.Concat(Perks))
        {
            code.Add(key, StringComparer.Ordinal);
        }

        return code.ToHashCode();
    }
}

/// <summary>
/// The ascension paths, which are the game's own seven and not a list made up here.
/// </summary>
/// <remarks>
/// <para>
/// Taken from the scripted triggers rather than from the ascension perks, because a path is not the
/// same thing as a perk: <c>has_cybernetic_ascension</c> is satisfied by any of three perks, by the
/// Cybernetic Creed origin, or by the Augmentation Bazaars civic - two of which are chosen in the
/// designer, before a perk exists to take. A picker built as "one of four perks" would be wrong for
/// every empire that arrives on a path by its origin.
/// </para>
/// <para>
/// Seven, and no more: <c>common/scripted_triggers</c> defines exactly
/// <c>has_cloning_ascension</c>, <c>has_cybernetic_ascension</c>, <c>has_genetic_ascension</c>,
/// <c>has_mutation_ascension</c>, <c>has_psionic_ascension</c>, <c>has_purity_ascension</c> and
/// <c>has_synthetic_ascension</c>. Virtuality, Nanotech and Modularity look like paths and are
/// tradition trees; <c>has_ascension_path</c> itself is only the first four.
/// </para>
/// <para>
/// The numbers are written down because they travel in a share link. Appending is safe; renumbering
/// silently repoints every link ever shared.
/// </para>
/// </remarks>
public enum PlanPath
{
    /// <summary>Nothing decided, which is different from having decided against one.</summary>
    Unset = 0,

    /// <summary>Decided against ascending, which is a plan and should be sayable.</summary>
    None = 1,

    Cybernetic = 2,

    Genetic = 3,

    Purity = 4,

    Cloning = 5,

    Mutation = 6,

    Psionic = 7,

    Synthetic = 8,
}

/// <summary>The paths as a list, and what the game calls each of them.</summary>
/// <remarks>
/// A path has no name of its own in the game's text - it is a shape a country is in, asked about by
/// a trigger, never printed. What a player calls it is the tradition tree they take to get there,
/// which is the word the game does print and the one they will recognise. So each path borrows its
/// tree's name rather than being given one invented here.
/// </remarks>
public static class PlanPaths
{
    /// <summary>Every path, in the order a picker should offer them.</summary>
    public static IReadOnlyList<PlanPath> All { get; } =
    [
        PlanPath.Unset,
        PlanPath.None,
        PlanPath.Cybernetic,
        PlanPath.Genetic,
        PlanPath.Purity,
        PlanPath.Cloning,
        PlanPath.Mutation,
        PlanPath.Psionic,
        PlanPath.Synthetic,
    ];

    /// <summary>
    /// The game's key for what a path is called, or nothing for the two that are answers rather
    /// than paths.
    /// </summary>
    public static string? NameKey(PlanPath path) => path switch
    {
        PlanPath.Cybernetic => "tradition_cybernetics",
        PlanPath.Genetic => "tradition_genetics",
        PlanPath.Purity => "tradition_purity",
        PlanPath.Cloning => "tradition_cloning",
        PlanPath.Mutation => "tradition_mutation",
        PlanPath.Psionic => "tradition_psionics",
        PlanPath.Synthetic => "tradition_synthetics",
        _ => null,
    };

    /// <summary>Every key this needs from the game's text, for whatever decides what to keep.</summary>
    public static IEnumerable<string> NameKeys =>
        All.Select(NameKey).OfType<string>();
}

/// <summary>Which of an empire's two biographies is carrying its plan.</summary>
/// <remarks>
/// Both are free text the game keeps, and neither is the obvious one: a plan is about the empire
/// rather than about its species or its ruler, so the player says which of theirs to spend. Nothing
/// records the answer - it is wherever the plan is found.
/// </remarks>
public enum PlanHome
{
    /// <summary>The founding species' biography, <c>species_bio</c>.</summary>
    Species,

    /// <summary>The ruler's biography, <c>custom_biography</c> on the ruler.</summary>
    Ruler,
}
