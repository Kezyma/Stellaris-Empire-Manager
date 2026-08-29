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
public sealed partial class GameDataJsonContext : JsonSerializerContext;
