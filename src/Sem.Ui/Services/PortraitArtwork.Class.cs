using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// The faces a species class wears.
/// </summary>
/// <remarks>
/// A class has no artwork of its own - the game gives it none - so the thing that stands for it is
/// one of its own portraits. Which one is not recorded either, so it is the first the game lists,
/// which is the one its own picker opens on.
/// </remarks>
public static class SpeciesFaces
{
    /// <summary>Every portrait a class can wear, in the order the game lists them.</summary>
    /// <param name="database">The extracted game.</param>
    /// <param name="speciesClass">The class.</param>
    /// <returns>The portrait keys, which may be none.</returns>
    public static IReadOnlyList<string> All(GameDatabase database, string? speciesClass)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (speciesClass is not { Length: > 0 })
        {
            return [];
        }

        return
        [
            .. database.PortraitSets
                .Where(s => string.Equals(s.SpeciesClass, speciesClass, StringComparison.Ordinal))
                .SelectMany(s => s.Portraits)
                .Select(p => p.Key),
        ];
    }

    /// <summary>
    /// The thumbnail that stands for a class, or nothing where the game gives it no faces.
    /// </summary>
    /// <remarks>
    /// Through <see cref="PortraitArtwork.For"/> rather than reading the portrait's own thumbnail,
    /// because the first face of a class is often a group rather than a likeness - all thirty of the
    /// humans are one - and a group has no picture of its own. That lookup already knows to take a
    /// member's.
    /// </remarks>
    /// <param name="database">The extracted game.</param>
    /// <param name="speciesClass">The class.</param>
    /// <returns>The image path, or null.</returns>
    public static string? Of(GameDatabase database, string? speciesClass)
    {
        ArgumentNullException.ThrowIfNull(database);

        return All(database, speciesClass)
            .Select(key => PortraitArtwork.For(database, key, gender: null))
            .FirstOrDefault(found => found is { Length: > 0 });
    }
}
