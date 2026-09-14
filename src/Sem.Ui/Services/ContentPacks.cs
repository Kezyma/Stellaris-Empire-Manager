using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// Which content packs a condition depends on.
/// </summary>
/// <remarks>
/// Around two hundred of the three hundred and fifty-eight civics and origins are behind a pack, and
/// the check is left in the data on purpose: which packs are owned is a fact about the person using
/// the app rather than about the game files, so it is answered at runtime and the web build has to
/// let somebody say so.
/// </remarks>
public static class ContentPacks
{
    /// <summary>
    /// Every pack named anywhere in a condition, once each, in the order they read in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat walk is right here, which is the difference between this and <see cref="CivicReach"/>.
    /// The question is "which packs decide anything about this" - it does not matter whether a pack
    /// is being required or ruled out, only that owning it changes the answer - and
    /// <see cref="Requirement.AndNested"/> is written for exactly that: its own remarks name "which
    /// content packs decide anything" as the caller it exists for.
    /// </para>
    /// <para>
    /// Order is the order they appear rather than alphabetical, because a civic behind two packs
    /// almost always names the one that introduced it first, and that is the one a reader is
    /// looking for.
    /// </para>
    /// </remarks>
    /// <param name="requirement">The condition to read, or nothing.</param>
    /// <returns>The pack names, exactly as the game spells them.</returns>
    public static IReadOnlyList<string> Named(Requirement? requirement) =>
        requirement is null
            ? []
            : [.. requirement.AndNested()
                .OfType<DlcRequirement>()
                .Select(d => d.Name)
                .Distinct(StringComparer.Ordinal)];
}
