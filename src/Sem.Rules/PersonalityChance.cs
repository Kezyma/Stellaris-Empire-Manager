using Sem.GameData;

namespace Sem.Rules;

/// <summary>
/// One personality an empire might be given, and how likely it is to be the one.
/// </summary>
/// <remarks>
/// The share is against every personality the same empire allows, so a set of these always adds to
/// one. An empire allowing a single personality gets one of these at 1.0, which reads the way the
/// government does - a name, with no odds worth mentioning.
/// </remarks>
/// <param name="Personality">The personality itself.</param>
/// <param name="Weight">What it weighs for this empire, base plus whatever applied.</param>
/// <param name="Share">Its share of the draw, between zero and one.</param>
public sealed record PersonalityChance(PersonalityDefinition Personality, double Weight, double Share);
