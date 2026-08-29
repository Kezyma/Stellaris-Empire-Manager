using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// Writes a name into a design the way the game writes it.
/// </summary>
/// <remarks>
/// <para>
/// The game keeps a distinction the designer has to keep with it. A name chosen from one of its own
/// lists is stored as a localisation key — <c>species_name = { key="SPEC_Oxanalytor" }</c> — while a
/// name the player typed is stored as text with <c>literal=yes</c>. Storing everything as text
/// works, but it quietly translates the empire into English for good: a player who then loads it in
/// another language sees words the game would have had in theirs.
/// </para>
/// <para>
/// The adjective is stranger still. The game does not store one at all. It stores the template it
/// forms adjectives with and the species name to form it from, and builds the word each time.
/// </para>
/// </remarks>
public static class NameWriter
{
    /// <summary>
    /// Writes a species' three names, as keys where the choice came from the game's own list.
    /// </summary>
    public static void Species(SpeciesDesign species, SpeciesNameSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(suggestion);

        Write(species.Name, suggestion.NameKey, suggestion.Name);
        Write(species.Plural, suggestion.PluralKey ?? suggestion.NameKey, suggestion.Plural ?? suggestion.Name);

        if (suggestion.NameKey is { Length: > 0 } key)
        {
            species.Adjective.SetAdjectiveOf(key);
        }
        else
        {
            species.Adjective.SetLiteral(NameGenerator.Adjective(suggestion.Name));
        }
    }

    /// <summary>
    /// Writes one name, as a key where there is one and as text otherwise.
    /// </summary>
    /// <remarks>
    /// A suggestion whose name the game spells out rather than keying — a few lists do — falls back
    /// to the text, which is the same thing the player typing it would produce.
    /// </remarks>
    public static void Write(LocRef field, string? key, string? text)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (key is { Length: > 0 } && !string.Equals(key, text, StringComparison.Ordinal))
        {
            field.SetKey(key);
            return;
        }

        field.SetLiteral(text ?? string.Empty);
    }
}
