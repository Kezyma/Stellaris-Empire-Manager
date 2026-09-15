using System.Text.Json.Serialization;

namespace Sem.GameData;

/// <summary>
/// Compiled reading and writing for the game database.
/// </summary>
/// <remarks>
/// <para>
/// The browser runs this code interpreted, and reflection-based serialisation there is slow enough
/// to be indistinguishable from a hang: two megabytes of deeply nested conditions never finished
/// loading. Generating the serialiser at build time turns that into a fraction of a second.
/// </para>
/// <para>
/// Both ends use this one context, so the shape written can never disagree with the shape read.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GameDatabase))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Requirement))]

// The wardrobe is written beside the database rather than inside it: thousands of pictures serve a
// leader designer, and the empire designer should not read them to show one face per portrait.
[JsonSerializable(typeof(IReadOnlyList<PortraitOutfit>))]

// And the wiki's own files, one per domain, for the same reason carried further: what the wiki shows
// is mostly of no use to a designer, and a reader who never opens a page should not fetch it. One
// line each as they arrive.
[JsonSerializable(typeof(LeaderTraitPack))]

// And the wiki's own files, one per domain, for the same reason carried further: what the wiki shows
// is mostly of no use to a designer, and a reader who never opens a page should not fetch it. One
// line each as they arrive.
[JsonSerializable(typeof(LeaderTraitPack))]
[JsonSerializable(typeof(ShipsetPack))]
[JsonSerializable(typeof(PersonalityPack))]
[JsonSerializable(typeof(EthicPack))]
[JsonSerializable(typeof(AuthorityPack))]
[JsonSerializable(typeof(GovernmentPack))]
[JsonSerializable(typeof(CivicPack))]
[JsonSerializable(typeof(SpeciesTraitPack))]
[JsonSerializable(typeof(SpeciesClassPack))]
public sealed partial class GameDataJsonContext : JsonSerializerContext;
