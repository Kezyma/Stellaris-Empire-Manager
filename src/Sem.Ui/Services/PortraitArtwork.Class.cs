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

/// <summary>
/// The trait every species of an archetype carries, and so the picture an archetype wears.
/// </summary>
/// <remarks>
/// <para>
/// An archetype has no artwork either - <c>ArchetypeDefinition</c> is a trait budget and a flag -
/// so chips reading "Machine", "Lithoid" or "Biological" were bare words sitting beside chips that
/// all carried pictures. The game does give every species of an archetype a trait, and traits have
/// icons, so that is what stands for it.
/// </para>
/// <para>
/// Derived from the classes rather than written down here, and derived rather than extracted: a
/// property on the definition would read better and would cost a schema bump, which sends every
/// desktop player back through thirty-five thousand files to learn something the file they already
/// have can tell them.
/// </para>
/// </remarks>
public static class ArchetypeMarks
{
    /// <summary>
    /// The one archetype the game leaves implicit.
    /// </summary>
    /// <remarks>
    /// Every other archetype has classes that name a forced trait - Biological and Pre-Sapient say
    /// <c>trait_organic</c>, Lithoid <c>trait_lithoid</c>, Machine <c>trait_machine_unit</c>, Other
    /// <c>trait_exd</c>. The <c>ROBOT</c> class names none, so there is nothing to read; the game
    /// calls the archetype "Mechanical" and ships <c>trait_mechanical</c> under the same word, which
    /// is the trait every robotic species carries. Named here rather than matched by display text,
    /// which would be a guess dressed up as a rule and would break in any other language.
    /// </remarks>
    private const string Robotic = "trait_mechanical";

    /// <summary>The icon that stands for an archetype, or nothing where none can be found.</summary>
    /// <param name="database">The extracted game.</param>
    /// <param name="archetype">The archetype key, such as <c>MACHINE</c>.</param>
    /// <returns>The image path, or null.</returns>
    public static string? Of(GameDatabase database, string? archetype)
    {
        ArgumentNullException.ThrowIfNull(database);

        return Trait(database, archetype) is { } trait ? database.Trait(trait)?.Icon : null;
    }

    /// <summary>
    /// Which trait an archetype is known by.
    /// </summary>
    /// <remarks>
    /// The commonest of the forced traits among its classes, rather than the first: Pre-Sapient has
    /// nine classes forcing <c>trait_organic</c> and one apiece forcing the lithoid and thermophile
    /// traits, and the answer wanted is the one they nearly all share.
    /// </remarks>
    /// <param name="database">The extracted game.</param>
    /// <param name="archetype">The archetype key.</param>
    /// <returns>The trait key, or null.</returns>
    public static string? Trait(GameDatabase database, string? archetype)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (archetype is not { Length: > 0 })
        {
            return null;
        }

        var forced = database.SpeciesClasses
            .Where(c => string.Equals(c.Archetype, archetype, StringComparison.Ordinal))
            .Select(c => c.ForcedTrait)
            .OfType<string>()
            .Where(t => t.Length > 0)
            .GroupBy(t => t, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();

        return forced ?? (database.Archetype(archetype) is { IsRobotic: true } ? Robotic : null);
    }
}
