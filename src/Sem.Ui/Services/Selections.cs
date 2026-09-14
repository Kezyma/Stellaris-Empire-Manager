using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// What a condition actually asks for, as opposed to what it merely mentions.
/// </summary>
/// <remarks>
/// The game writes both in the same grammar. "Militarist empires may take this" and "anybody who is
/// not a primitive may take this" are each a selection inside a condition, and only the first is
/// something to file the civic under. Reading them the same way puts Corporate on the list of things
/// for primitives, and puts every mutually exclusive pair on each other's lists.
/// </remarks>
public static class Selections
{
    /// <summary>
    /// Every condition at the bottom of a tree, with whether it is being asked for or asked against.
    /// </summary>
    /// <remarks>
    /// Polarity is the whole of it: each <c>NOT</c> flips whether what is inside it is wanted or
    /// unwanted, and a walk that ignores that reads "without Megacorp" as "with Megacorp". Both
    /// kinds of nesting are walked in the polarity they are in - an alternative is still something
    /// the civic wants, even where it wants only one of several.
    /// </remarks>
    /// <param name="requirement">The condition to read, or nothing.</param>
    /// <returns>The leaves, in the order they are written.</returns>
    public static IEnumerable<(Requirement Leaf, bool Wanted)> Leaves(Requirement? requirement) =>
        Walk(requirement, wanted: true);

    private static IEnumerable<(Requirement Leaf, bool Wanted)> Walk(Requirement? requirement, bool wanted)
    {
        switch (requirement)
        {
            case null:
                break;

            case NotRequirement not:
                foreach (var found in Walk(not.Item, !wanted))
                {
                    yield return found;
                }

                break;

            case AllRequirement all:
                foreach (var found in all.Items.SelectMany(i => Walk(i, wanted)))
                {
                    yield return found;
                }

                break;

            case AnyRequirement any:
                foreach (var found in any.Items.SelectMany(i => Walk(i, wanted)))
                {
                    yield return found;
                }

                break;

            default:
                yield return (requirement, wanted);
                break;
        }
    }

    /// <summary>
    /// Every selection a condition asks for, ignoring the ones it rules out.
    /// </summary>
    /// <param name="requirement">The condition to read, or nothing.</param>
    /// <returns>The selections it asks for, in the order they are written.</returns>
    public static IReadOnlyList<SelectionRequirement> Required(Requirement? requirement) =>
    [
        .. Leaves(requirement)
            .Where(l => l.Wanted)
            .Select(l => l.Leaf)
            .OfType<SelectionRequirement>(),
    ];
}
