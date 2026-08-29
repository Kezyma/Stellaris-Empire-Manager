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
