using System.Reflection;
using System.Text;
using Sem.Extraction;
using Sem.GameData;

namespace Sem.Extraction.Tests;

/// <summary>
/// A portrait a content pack declares again, over the base game's.
/// </summary>
/// <remarks>
/// <para>
/// Keys repeat here - that is why the reader keeps a set of the ones it has seen - and the rule for
/// a key declared twice is that the last one wins, which is how the game loads them and how every
/// other resolution in this project works.
/// </para>
/// <para>
/// Three fields of one portrait were not obeying it together. The evolution stages were taken
/// last-wins, and the texture count and the attachment label were frozen at the moment the key was
/// first seen - so a portrait redeclared by a pack could come back with the pack's stages and the
/// base game's texture count. Too low a count hides skin variants the game accepts, and nothing
/// about it would look wrong.
/// </para>
/// </remarks>
public sealed class PortraitOverrideTests
{
    /// <summary>A game folder held in memory, so a redeclaration can be arranged exactly.</summary>
    private sealed class Files(Dictionary<string, string> content) : IContentSource
    {
        public string Name => "in memory";

        public bool Contains(string relativePath) => content.ContainsKey(relativePath);

        public byte[] Read(string relativePath) => Encoding.UTF8.GetBytes(content[relativePath]);

        public bool ContainsDirectory(string relativeDirectory) =>
            content.Keys.Any(k => k.StartsWith(relativeDirectory + "/", StringComparison.Ordinal));

        public IEnumerable<string> EnumerateFiles(
            string relativeDirectory, string pattern, bool recursive = false) =>
            content.Keys
                .Where(k => k.StartsWith(relativeDirectory + "/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the portraits, reaching the extractor the way the suite already reaches an internal.
    /// </summary>
    /// <remarks>
    /// The Extractors are internal on purpose - they are what GameDataExtractor is made of rather
    /// than a surface anything else should call - and the alternative to reflecting was either
    /// opening them up for a test or building a whole installation to reach one of them through the
    /// front door. KeptTests answers the same question the same way.
    /// </remarks>
    private static IReadOnlyList<PortraitDefinition> Portraits(ScriptLoader loader) =>
        (IReadOnlyList<PortraitDefinition>)typeof(ScriptLoader).Assembly
            .GetType("Sem.Extraction.Extractors.PortraitExtractor")!
            .GetMethod("ExtractPortraits", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [loader])!;

    /// <summary>One portrait, written the way the game writes them.</summary>
    private static string Portrait(string key, int textures, string label) =>
        $$"""
        portraits = {
            {{key}} = {
                character_textures = {
                    {{string.Join("\n", Enumerable.Range(0, textures).Select(i => $"\"face{i}.dds\""))}}
                }
                custom_attachment_label = "{{label}}"
            }
        }
        """;

    /// <summary>
    /// A pack that redeclares a portrait wins on every field, not on some of them.
    /// </summary>
    /// <remarks>
    /// The files sort so that the pack's is read second, which is load order. Read first-wins for
    /// two fields and last-wins for the third, one portrait came back as a mixture of two
    /// declarations - which is a shape no file on disk actually describes.
    /// </remarks>
    [Fact]
    public void APackThatRedeclaresAPortraitWinsOnEveryField()
    {
        var loader = new ScriptLoader(new LayeredContent([new Files(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gfx/portraits/portraits/00_base.txt"] = Portrait("hum1", textures: 2, label: "Hair"),
                ["gfx/portraits/portraits/99_pack.txt"] = Portrait("hum1", textures: 5, label: "Hat"),
            })]));

        var portrait = Assert.Single(Portraits(loader), p => p.Key == "hum1");

        Assert.Equal(5, portrait.TextureCount);
        Assert.Equal("Hat", portrait.AttachmentLabelKey);
    }

    /// <summary>A portrait nothing redeclares keeps what it said about itself.</summary>
    [Fact]
    public void APortraitNobodyRedeclaresIsUnchanged()
    {
        var loader = new ScriptLoader(new LayeredContent([new Files(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gfx/portraits/portraits/00_base.txt"] = Portrait("rep1", textures: 3, label: "Crest"),
            })]));

        var portrait = Assert.Single(Portraits(loader), p => p.Key == "rep1");

        Assert.Equal(3, portrait.TextureCount);
        Assert.Equal("Crest", portrait.AttachmentLabelKey);
    }
}
