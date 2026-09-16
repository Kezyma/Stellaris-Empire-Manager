using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// What to say in place of "never", where the extractor knows why it said so.
/// </summary>
/// <remarks>
/// <para>
/// A condition the compiler settles as false is drawn as a flat "Never", and most of the time that
/// is one bullet in the middle of a list of real requirements: a government asks for a hive empire,
/// then never, then not Evolutionary Predators. The reader is left to guess which of the three is
/// the problem, and the answer - that it waits on something an event sets - is nowhere on the line.
/// </para>
/// <para>
/// <see cref="AlwaysRequirement.Because"/> has carried the answer since it was added and nothing
/// drew it. Two and a half thousand of the three thousand falses the app ships name their cause;
/// the rest are the game's own <c>always = no</c>, which has nothing to explain and stays as it was.
/// </para>
/// <para>
/// Written out rather than prettified from the key. "Has Country Flag" is the script's word for it
/// and says nothing a reader can use, which is the same objection the civics' own badge already
/// makes about printing <c>caravaneer_fleet</c> on a card.
/// </para>
/// </remarks>
internal static class Unreachable
{
    /// <summary>
    /// Why a condition can never hold, where the extractor recorded it.
    /// </summary>
    /// <param name="because">The trigger that settled it, or null for the game's own refusal.</param>
    /// <returns>The words, which fall back to "never" for a cause with nothing written for it.</returns>
    internal static string Words(string? because) => because switch
    {
        // Things only the game's own empires are. A player is none of them, and the card's badge
        // says so too - this is the same fact where the condition is read rather than summarised.
        "is_ai" => "Only an empire the game runs itself",
        "is_country_type" => "Only a kind of empire nobody designs",
        "is_pirate" => "Only a pirate",
        "is_primitive" => "Only a pre-FTL empire",

        // Things a game reaches and a design cannot. Seventy-one governments wait on one of these:
        // the Mutation ascension sets bio_mutation partway through a game, and the empire is
        // renamed then rather than at creation.
        "has_country_flag" or "has_global_flag" or "has_planet_flag" => "Something an event sets",
        "has_menace_perk" => "A menace perk, which is taken during a game",
        "has_technology" => "A technology researched during a game",

        _ => "Never",
    };
}
