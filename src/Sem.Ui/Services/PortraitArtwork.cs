using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// Finds the picture to show for a portrait a design names.
/// </summary>
/// <remarks>
/// A design usually names a group rather than a likeness — the game's own United Nations of Earth
/// records <c>portrait = "human"</c> — because a group holds a face for each gender and the game
/// chooses between them when it draws. Nothing about the design changes when the gender does; only
/// which of the group's faces is shown.
/// </remarks>
public static class PortraitArtwork
{
    /// <summary>
    /// The key of the likeness actually worn, which for a group is one of its faces.
    /// </summary>
    /// <remarks>
    /// The picture is not always what a caller wants. Anything keyed by the likeness rather than by
    /// the group — the wardrobe is, since a group has no pieces of its own — needs the name of the
    /// face, not the path of its thumbnail.
    /// </remarks>
    public static string? Resolve(GameDatabase database, string? key, string? gender)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (key is not { Length: > 0 })
        {
            return null;
        }

        var portrait = database.Portraits.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.Ordinal));

        // A key naming no portrait at all is still worth returning: a design may name a likeness
        // from a version we did not read, and the caller's own lookup will say so.
        return portrait is null ? key : portrait.For(gender) ?? portrait.Key;
    }

    /// <summary>
    /// The rendered likeness for a portrait key, given the gender of the species wearing it.
    /// </summary>
    public static string? For(GameDatabase database, string? key, string? gender)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (key is not { Length: > 0 })
        {
            return null;
        }

        var portrait = database.Portraits.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.Ordinal));

        if (portrait is null)
        {
            return null;
        }

        if (portrait.Thumbnail is { Length: > 0 } own)
        {
            return own;
        }

        return portrait.For(gender) is { Length: > 0 } member
            ? database.Portraits
                .FirstOrDefault(p => string.Equals(p.Key, member, StringComparison.Ordinal))?.Thumbnail
            : null;
    }
}
