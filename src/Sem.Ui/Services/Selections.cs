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
    /// Every selection a condition asks for, ignoring the ones it rules out.
    /// </summary>
    /// <remarks>
    /// Polarity is the whole of it: each <c>NOT</c> flips whether what is inside it is being asked
    /// for or asked against, and only what survives in the positive is a requirement. Both kinds of
    /// nesting are walked in the polarity they are in - an alternative is still something the civic
    /// wants, even where it wants only one of several.
    /// </remarks>
    /// <param name="requirement">The condition to read, or nothing.</param>
    /// <returns>The selections it asks for, in the order they are written.</returns>
    public static IReadOnlyList<SelectionRequirement> Required(Requirement? requirement)
    {
        List<SelectionRequirement> found = [];
        Walk(requirement, positive: true, found);
        return found;
    }

    private static void Walk(Requirement? requirement, bool positive, List<SelectionRequirement> into)
    {
        switch (requirement)
        {
            case SelectionRequirement selection when positive:
                into.Add(selection);
                break;

            case NotRequirement not:
                Walk(not.Item, !positive, into);
                break;

            case AllRequirement all:
                foreach (var item in all.Items)
                {
                    Walk(item, positive, into);
                }

                break;

            case AnyRequirement any:
                foreach (var item in any.Items)
                {
                    Walk(item, positive, into);
                }

                break;

            default:
                break;
        }
    }
}
