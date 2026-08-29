using Sem.Assets;
using Sem.Clausewitz;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction;

/// <summary>What came of drawing the portraits.</summary>
/// <param name="Rendered">How many likenesses were produced.</param>
/// <param name="Bytes">Their total size.</param>
/// <param name="Failures">Portraits that could not be drawn, with the reason.</param>
public sealed record PortraitBakeReport(int Rendered, long Bytes, IReadOnlyList<string> Failures);

/// <summary>
/// Draws every portrait the game defines, once, so the designer has faces to show.
/// </summary>
/// <remarks>
/// Getting from a portrait's key to something that can be drawn takes three hops through the
/// game's files: the portrait names an entity, the entity names a mesh, and a separate file says
/// where that mesh lives. All three are ordinary script, so all three are read the same way as
/// everything else.
/// </remarks>
public sealed class PortraitBaker(LayeredContent content, SafeFile file)
{
    private const string ModelRoot = "gfx/models/portraits";

    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly PortraitRenderer _renderer = new();

    /// <summary>Decoded textures are shared between portraits, and there are far fewer of them.</summary>
    private readonly Dictionary<string, DdsImage?> _textures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Draws each portrait in the database and records where its likeness went.
    /// </summary>
    /// <returns>The portraits, with thumbnails filled in where one could be drawn.</returns>
    public (IReadOnlyList<PortraitDefinition> Portraits, PortraitBakeReport Report) Bake(
        IReadOnlyList<PortraitDefinition> portraits,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(portraits);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var entities = ReadEntities();
        var meshes = ReadMeshPaths();
        var portraitEntities = ReadPortraitEntities();

        var results = new List<PortraitDefinition>(portraits.Count);
        var failures = new List<string>();
        var rendered = 0;
        long bytes = 0;

        for (var i = 0; i < portraits.Count; i++)
        {
            var portrait = portraits[i];

            if (i % 50 == 0)
            {
                progress?.Report($"Drawing portraits ({i} of {portraits.Count})");
            }

            // A group has no model of its own and wears the likeness of the portrait it defaults to.
            if (portrait.IsGroup)
            {
                results.Add(portrait);
                continue;
            }

            try
            {
                if (Resolve(portrait.Key, portraitEntities, entities, meshes) is not { } meshPath)
                {
                    failures.Add($"{portrait.Key}: no model");
                    results.Add(portrait);
                    continue;
                }

                var png = Draw(meshPath);
                var destination = $"portraits/{portrait.Key}.png";

                _file.WriteAllBytes(Path.Combine(outputDirectory, destination), png);

                rendered++;
                bytes += png.Length;
                results.Add(portrait with { Thumbnail = destination });
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // One portrait that will not draw must not cost the player all the others.
                failures.Add($"{portrait.Key}: {ex.Message}");
                results.Add(portrait);
            }
        }

        return (results, new PortraitBakeReport(rendered, bytes, failures));
    }

    private byte[] Draw(string meshPath)
    {
        var mesh = PortraitMesh.Load(_content.Read(meshPath));
        var directory = meshPath[..meshPath.LastIndexOf('/')];

        var textures = new Dictionary<string, DdsImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in mesh.Parts.Select(p => p.Texture).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (LoadTexture($"{directory}/{name}") is { } texture)
            {
                textures[name] = texture;
            }
        }

        return PngWriter.Encode(_renderer.Render(mesh, textures));
    }

    private DdsImage? LoadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var cached))
        {
            return cached;
        }

        DdsImage? image = null;

        if (_content.Contains(path))
        {
            try
            {
                image = DdsReader.Read(_content.Read(path));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                image = null;
            }
        }

        _textures[path] = image;
        return image;
    }

    /// <summary>Portrait key to the entity it wears, from the portrait definitions.</summary>
    private Dictionary<string, string> ReadPortraitEntities()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles("gfx/portraits/portraits", "*.txt"))
        {
            var document = TryParse(path);

            foreach (var node in document?.Nodes ?? [])
            {
                if (node.Key != "portraits" || node.Block is null)
                {
                    continue;
                }

                foreach (var portrait in node.Block.Nodes)
                {
                    if (portrait.Key is { Length: > 0 } key &&
                        portrait.Block?.GetString("entity") is { Length: > 0 } entity)
                    {
                        map.TryAdd(key, entity);
                    }
                }
            }
        }

        return map;
    }

    /// <summary>Entity name to the mesh it uses, from the entity definitions.</summary>
    private Dictionary<string, string> ReadEntities() =>
        ReadNameToValue("*.asset", blockKey: "entity", valueKey: "pdxmesh");

    /// <summary>Mesh name to the file that holds it, from the mesh declarations.</summary>
    private Dictionary<string, string> ReadMeshPaths()
    {
        var map = ReadNameToValue("*.gfx", blockKey: "pdxmesh", valueKey: "file");

        foreach (var key in map.Keys)
        {
            map[key] = map[key].Replace('\\', '/');
        }

        return map;
    }

    /// <summary>
    /// Collects <c>name</c> to some other field from every block of a given kind, wherever it sits.
    /// </summary>
    /// <remarks>
    /// Entities are declared at the top of their file while meshes are wrapped in an
    /// <c>objectTypes</c> block, so the search covers the whole tree rather than assuming a depth.
    /// </remarks>
    private Dictionary<string, string> ReadNameToValue(string pattern, string blockKey, string valueKey)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _content.EnumerateFiles(ModelRoot, pattern, recursive: true))
        {
            if (TryParse(path) is not { } document)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                Collect(node);
            }
        }

        return map;

        void Collect(CwNode node)
        {
            if (node.Block is not { } body)
            {
                return;
            }

            if (string.Equals(node.Key, blockKey, StringComparison.Ordinal) &&
                body.GetString("name") is { Length: > 0 } name &&
                body.GetString(valueKey) is { Length: > 0 } value)
            {
                map.TryAdd(name, value);
                return;
            }

            foreach (var child in body.Nodes)
            {
                Collect(child);
            }
        }
    }

    private string? Resolve(
        string portraitKey,
        Dictionary<string, string> portraitEntities,
        Dictionary<string, string> entities,
        Dictionary<string, string> meshes)
    {
        if (!portraitEntities.TryGetValue(portraitKey, out var entity) ||
            !entities.TryGetValue(entity, out var mesh) ||
            !meshes.TryGetValue(mesh, out var path))
        {
            return null;
        }

        return _content.Contains(path) ? path : null;
    }

    private CwDocument? TryParse(string path)
    {
        try
        {
            return CwDocument.Parse(_content.Read(path), CwParseOptions.Lenient);
        }
        catch (Exception ex) when (ex is CwSyntaxException or IOException)
        {
            return null;
        }
    }
}
