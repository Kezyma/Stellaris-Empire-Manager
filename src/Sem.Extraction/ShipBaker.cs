using Sem.Assets;
using Sem.Clausewitz;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction;

/// <summary>What drawing the ships cost, and which ones would not draw.</summary>
public sealed record ShipBakeReport(int Rendered, long Bytes, IReadOnlyList<string> Failures)
{
    /// <summary>Nothing drawn, because nothing was asked for.</summary>
    public static ShipBakeReport None { get; } = new(0, 0, []);
}

/// <summary>
/// Draws one ship for each appearance set, so the picker can show what a set looks like.
/// </summary>
/// <remarks>
/// <para>
/// The game has no artwork for this. Its own picker spins the models live, and the only flat picture
/// anywhere near it is the panel's background — so a set is shown by rendering it, the way portraits
/// are.
/// </para>
/// <para>
/// One ship per set, and the same class of ship each time, since the point is to compare sets rather
/// than ships. A corvette is the right one: every set that builds ships builds one, it is a single
/// whole hull rather than a bow, a middle and a stern bolted together, and it is the ship a player
/// sees first.
/// </para>
/// </remarks>
public sealed class ShipBaker(LayeredContent content, SafeFile file)
{
    private const string ModelRoot = "gfx/models/ships";

    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly ModelRenderer _renderer = new();

    // A set's entity file runs to a hundred kilobytes and every set that falls back to it reads it
    // again, so the answer is kept rather than the parse repeated.
    private readonly Dictionary<string, IReadOnlySet<string>> _inService = new(StringComparer.Ordinal);

    /// <summary>
    /// The ships a set is drawn by, in the order they are looked for.
    /// </summary>
    /// <remarks>
    /// A corvette for every set that builds ships. BioGenesis grows its fleet instead of building
    /// it, so it has no corvette and its smallest warship is a mauler; asking for a corvette and
    /// giving up would have shown it a mammalian hull, which is the one thing a BioGenesis empire
    /// certainly does not fly.
    /// </remarks>
    private static readonly string[] ShipsWorthShowing = ["corvette", "mauler_ship_stage_1"];

    /// <summary>
    /// Draws a ship for each set and records where the picture went.
    /// </summary>
    /// <returns>The sets, with previews filled in where one could be drawn.</returns>
    public (IReadOnlyList<GraphicalCultureDefinition> Sets, ShipBakeReport Report) Bake(
        IReadOnlyList<GraphicalCultureDefinition> sets,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var byKey = sets.ToDictionary(s => s.Key, StringComparer.Ordinal);

        var results = new List<GraphicalCultureDefinition>(sets.Count);
        var failures = new List<string>();
        var rendered = 0;
        long bytes = 0;

        progress?.Report($"Drawing ships ({sets.Count} sets)");

        foreach (var set in sets)
        {
            if (HullFor(set, byKey) is not { } ship)
            {
                // Most sets that reach here are the ones only the galaxy uses — a fallen empire, a
                // marauder — and a set with no ships anywhere in its fallbacks is not a fault.
                results.Add(set);
                continue;
            }

            try
            {
                if (Draw(ship) is not { } png)
                {
                    failures.Add($"{set.Key}: nothing in {ship} could be drawn");
                    results.Add(set);
                    continue;
                }

                var destination = $"ships/{set.Key}.png";
                _file.WriteAllBytes(Path.Combine(outputDirectory, destination), png);

                rendered++;
                bytes += png.Length;
                results.Add(set with { ShipPreview = destination });
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                // One set that will not draw must not cost the player all the others.
                failures.Add($"{set.Key}: {ex.Message}");
                results.Add(set);
            }
        }

        return (results, new ShipBakeReport(rendered, bytes, failures));
    }

    /// <summary>
    /// The mesh a set is drawn by, following its fallbacks when it has no models of its own.
    /// </summary>
    /// <remarks>
    /// Falling back is the game's own arrangement, declared by the set: a set without artwork of its
    /// own is played with the artwork of the one it names. Solarpunk has no ship models at all and
    /// is flown with fungoid hulls, so a fungoid hull is what its picker entry should show.
    /// </remarks>
    public string? HullFor(
        GraphicalCultureDefinition set,
        IReadOnlyDictionary<string, GraphicalCultureDefinition> sets)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(sets);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = set; current is not null && seen.Add(current.Key);)
        {
            if (Own(current.Key) is { } mesh)
            {
                return mesh;
            }

            current = current.Fallback is { Length: > 0 } next ? sets.GetValueOrDefault(next) : null;
        }

        return null;
    }

    /// <summary>
    /// The best ship a set models itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A set names its meshes after itself, so its own are the ones that start with its key; a
    /// borrowed hull is not counted as its own, which is what keeps the fallback honest.
    /// </para>
    /// <para>
    /// A folder holds more meshes of a ship than there are ships, because the game models a hull
    /// once per weapon loadout — <c>S3</c>, <c>M1S1</c> and the rest — and any of them is the ship.
    /// What is not the ship is a mesh no entity names: the reptilian set keeps one called
    /// <c>_test</c>, which sorts last and so was the one being drawn. An entity is the game's own
    /// statement that a model is in service, so a mesh without one is passed over.
    /// </para>
    /// <para>
    /// Among the rest the last by name is taken, since the files are ordered and the later ones
    /// carry more of the fittings that make a set recognisable.
    /// </para>
    /// </remarks>
    private string? Own(string key)
    {
        var directory = $"{ModelRoot}/{key}";

        if (!_content.ContainsDirectory(directory))
        {
            return null;
        }

        var flown = InService(directory, key);

        var meshes = _content
            .EnumerateFiles(directory, "*.mesh")
            .Where(path => Path.GetFileName(path).StartsWith(key, StringComparison.Ordinal))
            // A set that declares no entities at all has none to be excluded by, and is better drawn
            // from whatever it models than not drawn.
            .Where(path => flown.Count == 0 || flown.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var wanted in ShipsWorthShowing)
        {
            if (Best(meshes, wanted) is { } match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>The last mesh of the wanted kind, by name.</summary>
    /// <remarks>
    /// A <c>_frame</c> is excluded: it is the armature the game hangs a hull's sections on, three
    /// vertices that draw nothing, and it matches every other test.
    /// </remarks>
    private static string? Best(IReadOnlyList<string> meshes, string wanted) => meshes
        .LastOrDefault(path => Path.GetFileNameWithoutExtension(path) is { } name
            && name.Contains(wanted, StringComparison.Ordinal)
            && !name.EndsWith("_frame", StringComparison.Ordinal));

    /// <summary>
    /// The set's own meshes that one of its entities names, which is the game saying a mesh is flown.
    /// </summary>
    /// <remarks>
    /// An entity is the game's unit of a thing in the world: it names the mesh to draw, the
    /// animations it can play and the places weapons attach. The <c>.asset</c> files declare them,
    /// and a mesh no entity names is an offcut left in the folder.
    /// </remarks>
    private IReadOnlySet<string> InService(string directory, string key)
    {
        if (_inService.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var files = MeshFiles(directory, key);

        // The game spells a path as it likes; the content index does not care, and neither should
        // matching against it.
        var flown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in _content.EnumerateFiles(directory, "*_entities.asset"))
        {
            var document = CwDocument.Parse(_content.Read(asset), CwParseOptions.Lenient);

            foreach (var entity in document.Nodes.Where(n => n.Key == "entity"))
            {
                // An entity naming a mesh from another set is how a set borrows a hull it never
                // modelled, and is not one of this set's own.
                if (entity.Block?.GetString("pdxmesh") is { Length: > 0 } mesh &&
                    files.GetValueOrDefault(mesh) is { Length: > 0 } file &&
                    Path.GetFileName(file).StartsWith(key, StringComparison.Ordinal) &&
                    _content.Contains(file))
                {
                    flown.Add(file);
                }
            }
        }

        _inService[key] = flown;
        return flown;
    }

    /// <summary>Which file each of a set's declared meshes is, by the name entities refer to it by.</summary>
    private IReadOnlyDictionary<string, string> MeshFiles(string directory, string set)
    {
        var settings = $"{directory}/_{set}_ships_meshes.gfx";
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!_content.Contains(settings))
        {
            return files;
        }

        var document = CwDocument.Parse(_content.Read(settings), CwParseOptions.Lenient);

        foreach (var node in Declarations(document))
        {
            if (node.Block?.GetString("name") is { Length: > 0 } name &&
                node.Block.GetString("file") is { Length: > 0 } file)
            {
                files[name] = file;
            }
        }

        return files;
    }

    /// <summary>
    /// The mesh declarations in a set's <c>.gfx</c>, which are one <c>objectTypes</c> block of
    /// <c>pdxmesh</c> entries.
    /// </summary>
    private static IEnumerable<CwNode> Declarations(CwDocument document) => document.Nodes
        .Where(n => n.Key == "objectTypes")
        .SelectMany(n => n.Block?.Nodes ?? [])
        .Where(n => n.Key == "pdxmesh");

    /// <summary>Draws one mesh, with whichever textures its parts ask for.</summary>
    private byte[]? Draw(string meshPath)
    {
        var mesh = Dress(PortraitMesh.Load(_content.Read(meshPath)), meshPath);
        var folder = meshPath[..meshPath.LastIndexOf('/')];

        var textures = new Dictionary<string, DdsImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in mesh.Parts
                     .Where(ModelRenderer.IsVisible)
                     .Select(p => p.Texture!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // A part names its texture by file name alone, which the game resolves beside the mesh.
            var path = $"{folder}/{name}";

            if (_content.Contains(path))
            {
                textures[name] = DdsReader.Read(_content.Read(path));
            }
        }

        return _renderer.Render(mesh, textures) is { } image ? PngWriter.Encode(image) : null;
    }

    /// <summary>
    /// Gives each part the texture the set says it wears.
    /// </summary>
    /// <remarks>
    /// Older meshes name their own texture and need nothing here. The newer sets — the psionic and
    /// mindwarden hulls of Shadows of the Shroud among them — carry a material with a shader and
    /// nothing else, and declare the textures in the set's <c>meshsettings</c> instead, keyed by the
    /// part's name. Read only where the mesh is silent, so a mesh that knows its own texture keeps
    /// it.
    /// </remarks>
    private PortraitMesh Dress(PortraitMesh mesh, string meshPath)
    {
        if (mesh.Parts.All(p => p.Texture is { Length: > 0 }))
        {
            return mesh;
        }

        var declared = Declared(meshPath);

        return new PortraitMesh(
        [
            .. mesh.Parts.Select(part => part.Texture is { Length: > 0 }
                ? part
                : part with
                {
                    // The settings name a shape and which of its meshes, since a shape painted with
                    // several materials is several meshes under one name. Older sets give no index
                    // and mean the shape entire.
                    Texture = declared.GetValueOrDefault((part.Name, part.Index))
                        ?? declared.GetValueOrDefault((part.Name, 0)),
                })
        ])
        {
            Bones = mesh.Bones,
        };
    }

    /// <summary>What the set's own mesh settings say each part of a mesh is textured with.</summary>
    private IReadOnlyDictionary<(string Name, int Index), string> Declared(string meshPath)
    {
        var folder = meshPath[..meshPath.LastIndexOf('/')];
        var set = folder[(folder.LastIndexOf('/') + 1)..];
        var settings = $"{folder}/_{set}_ships_meshes.gfx";

        var textures = new Dictionary<(string, int), string>();

        if (!_content.Contains(settings))
        {
            return textures;
        }

        var document = CwDocument.Parse(_content.Read(settings), CwParseOptions.Lenient);

        foreach (var node in Declarations(document))
        {
            if (node.Block is not { } body ||
                body.GetString("file") is not { } file ||
                !string.Equals(file, meshPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var part in body.Nodes.Where(n => n.Key == "meshsettings"))
            {
                if (part.Block is { } settingsBlock &&
                    settingsBlock.GetString("name") is { Length: > 0 } name &&
                    settingsBlock.GetString("texture_diffuse") is { Length: > 0 } diffuse)
                {
                    var index = int.TryParse(settingsBlock.GetString("index"), out var declared)
                        ? declared
                        : 0;

                    textures[(name, index)] = diffuse;
                }
            }
        }

        return textures;
    }
}
