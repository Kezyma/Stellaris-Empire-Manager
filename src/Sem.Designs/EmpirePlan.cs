namespace Sem.Designs;

/// <summary>
/// How a player means to play an empire: the tradition trees they intend to open, the ascension
/// perks they intend to take, and the civics they mean to reform their government into.
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
/// <para>
/// All three lists are ordered, and the order is the plan. An ascension perk that the game will not
/// grant until three others have been taken cannot be first, so where something sits is as much a
/// part of the plan as whether it is in it.
/// </para>
/// </remarks>
public sealed record EmpirePlan(
    IReadOnlyList<string> Trees,
    IReadOnlyList<string> Perks,
    IReadOnlyList<string> Civics)
{
    /// <summary>A plan that says nothing, which is what an empire without one has.</summary>
    public static EmpirePlan Empty { get; } = new([], [], []);

    /// <summary>Whether anything has actually been planned.</summary>
    public bool Any => Trees.Count > 0 || Perks.Count > 0 || Civics.Count > 0;

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
        && Trees.SequenceEqual(other.Trees, StringComparer.Ordinal)
        && Perks.SequenceEqual(other.Perks, StringComparer.Ordinal)
        && Civics.SequenceEqual(other.Civics, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var code = new HashCode();

        foreach (var key in Trees.Concat(Perks).Concat(Civics))
        {
            code.Add(key, StringComparer.Ordinal);
        }

        return code.ToHashCode();
    }
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
