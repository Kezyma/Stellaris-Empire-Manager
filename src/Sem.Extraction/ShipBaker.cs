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

    /// <summary>
    /// Every ship drawn, by the set that models it and the class it is.
    /// </summary>
    /// <remarks>
    /// Returned rather than written into the database. The designer needs one picture per set and
    /// has a field for it; a gallery of every class is the wiki's business, and putting it in
    /// <c>gamedb.json</c> would cost a schema bump for something no empire reads.
    /// </remarks>
    public IReadOnlyList<ShipRender> Fleet { get; init; } = [];
}

/// <summary>One drawn ship: which set models it, which class it is, and where the picture went.</summary>
/// <param name="Set">The graphical culture that owns the models.</param>
/// <param name="ShipClass">The ship size's key, such as <c>battleship</c>.</param>
/// <param name="Image">Where the picture was written, within the extracted assets.</param>
public sealed record ShipRender(string Set, string ShipClass, string Image);

/// <summary>
/// Draws the ships each appearance set flies, so a page can show what a set looks like.
/// </summary>
/// <remarks>
/// <para>
/// The game has no artwork for this. Its own picker spins the models live, and the only flat picture
/// anywhere near it is the panel's background — so a set is shown by rendering it, the way portraits
/// are.
/// </para>
/// <para>
/// Which ships is the game's own answer rather than a list kept here: eighteen ship sizes carry
/// <c>enable_3dview_in_ship_browser = yes</c>, which is the flag its picker's spinner reads, and
/// each states how it is put together. Nine of those are a single hull and five are a bow, a middle
/// and a stern in separate files.
/// </para>
/// <para>
/// The sections need no assembling. Each is modelled already in place — a humanoid battleship's bow
/// runs from z -14.2 to 0.7, its middle from -8.93 to 5.99 and its stern from -1.53 to 9.26 — so
/// drawing the three into one image is the whole of it. The frame they hang on draws nothing at all
/// and carries locators for weapons and explosions, which is what it is for.
/// </para>
/// </remarks>
public sealed class ShipBaker(LayeredContent content, SafeFile file)
{
    private const string ModelRoot = "gfx/models/ships";

    /// <summary>Where the game states which ships it shows and how they are put together.</summary>
    private const string SizeRoot = "common/ship_sizes";

    /// <summary>And where it states the pieces each of the sectioned ones is made of.</summary>
    private const string SectionRoot = "common/section_templates";

    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));
    private readonly ModelRenderer _renderer = new();

    // A set's entity file runs to a hundred kilobytes and every set that falls back to it reads it
    // again, and now every ship class asks as well, so the answer is kept rather than the parse
    // repeated. Keyed by entity name, which is how the game names a ship: a size and a section
    // template each name one, and the mesh they draw is whatever it says.
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _inService =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Draws every ship each set flies, and records where the pictures went.
    /// </summary>
    /// <remarks>
    /// A set that models nothing of its own flies its fallback's ships, so its gallery is that set's
    /// and the pictures are not drawn twice. Which is why the render says who owns it.
    /// </remarks>
    /// <param name="sets">The graphical cultures.</param>
    /// <param name="outputDirectory">Where the extracted assets go.</param>
    /// <param name="progress">Told what is being drawn.</param>
    /// <returns>The sets, with previews filled in where one could be drawn, and the whole fleet.</returns>
    public (IReadOnlyList<GraphicalCultureDefinition> Sets, ShipBakeReport Report) Bake(
        IReadOnlyList<GraphicalCultureDefinition> sets,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var byKey = sets.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var classes = ShipClasses();
        var sections = Sections();

        var results = new List<GraphicalCultureDefinition>(sets.Count);
        var failures = new List<string>();
        var fleet = new List<ShipRender>();

        // One picture per owning set and class, however many sets borrow it.
        var drawn = new Dictionary<string, string>(StringComparer.Ordinal);
        var rendered = 0;
        long bytes = 0;

        progress?.Report($"Drawing ships ({sets.Count} sets, {classes.Count} classes)");

        foreach (var set in sets)
        {
            var flown = new List<ShipRender>();

            foreach (var ship in classes)
            {
                if (HullOf(set, ship, sections, byKey) is not { } hull)
                {
                    continue;
                }

                var destination = $"ships/{hull.Owner}/{ship.Key}.png";

                if (drawn.TryGetValue(destination, out var already))
                {
                    if (already.Length > 0)
                    {
                        flown.Add(new ShipRender(hull.Owner, ship.Key, destination));
                    }

                    continue;
                }

                try
                {
                    if (Draw(hull.Meshes, hull.Owner) is not { } png)
                    {
                        failures.Add($"{hull.Owner} {ship.Key}: nothing in its meshes could be drawn");
                        drawn[destination] = string.Empty;
                        continue;
                    }

                    _file.WriteAllBytes(Path.Combine(outputDirectory, destination), png);

                    rendered++;
                    bytes += png.Length;
                    drawn[destination] = destination;
                    flown.Add(new ShipRender(hull.Owner, ship.Key, destination));
                }
                catch (Exception ex)
                    when (ex is InvalidDataException or NotSupportedException or IOException

                        // Skia answers a bitmap it will not encode by returning null, and the writer
                        // turns that into this - so every bake path could throw one, and none of them
                        // caught it. One icon that would not encode cost the player every other one,
                        // which is the opposite of what the sentence below says happens.
                        or InvalidOperationException)
                {
                    // One ship that will not draw must not cost the player all the others.
                    failures.Add($"{hull.Owner} {ship.Key}: {ex.Message}");
                    drawn[destination] = string.Empty;
                }
            }

            fleet.AddRange(flown.Select(f => f with { Set = set.Key }));

            // The designer wants one picture, and the same class of ship each time so that its
            // picker compares sets rather than ships. A corvette where there is one: every set that
            // builds ships builds one, and it is the ship a player sees first. BioGenesis grows its
            // fleet instead and has no corvette, so it shows whatever it flies smallest.
            var preview = flown.FirstOrDefault(f => f.ShipClass == "corvette") ?? flown.FirstOrDefault();

            results.Add(preview is null ? set : set with { ShipPreview = preview.Image });
        }

        return (results, new ShipBakeReport(rendered, bytes, failures) { Fleet = fleet });
    }

    /// <summary>
    /// A ship class the game's own browser shows, and the sections it is built from.
    /// </summary>
    /// <param name="Key">The ship size's key, such as <c>battleship</c>.</param>
    /// <param name="Slots">Which slots it has, in the order the game lists them.</param>
    private sealed record ShipClassShape(string Key, IReadOnlyList<string> Slots);

    /// <summary>
    /// Which ships to draw, which is the ones the game shows in its own browser.
    /// </summary>
    /// <remarks>
    /// <c>enable_3dview_in_ship_browser</c> is the flag the picker's spinner reads, and eighteen
    /// sizes carry it. Read rather than listed here, so a patch that adds a class adds it to the
    /// page - and because the same block states the slots, which is how the sectioned ones are
    /// found.
    /// </remarks>
    /// <returns>The classes, in the order the game declares them.</returns>
    private IReadOnlyList<ShipClassShape> ShipClasses()
    {
        var classes = new List<ShipClassShape>();

        foreach (var path in _content.EnumerateFiles(SizeRoot, "*.txt"))
        {
            var document = CwDocument.Parse(_content.Read(path), CwParseOptions.Lenient);

            foreach (var size in document.Nodes)
            {
                if (size.Key is not { Length: > 0 } key ||
                    size.Block is not { } body ||
                    !body.GetBool("enable_3dview_in_ship_browser"))
                {
                    continue;
                }

                var slots = body.GetBlock("section_slots")?.Nodes
                    .Select(n => n.Key?.Trim('"') ?? string.Empty)
                    .Where(k => k.Length > 0)
                    .ToList() ?? [];

                classes.Add(new ShipClassShape(key, slots));
            }
        }

        return classes;
    }

    /// <summary>
    /// The entities each ship class's sections are drawn by, one list per slot.
    /// </summary>
    /// <remarks>
    /// A section template names its entity without the set in front of it -
    /// <c>battleship_bow_XL1_entity</c> - and the game puts the set there when it draws one. So the
    /// same template serves every set, which is why these are read once rather than per set.
    /// </remarks>
    /// <returns>The entity names, keyed by ship size and slot.</returns>
    private IReadOnlyDictionary<(string Size, string Slot), IReadOnlyList<string>> Sections()
    {
        var found = new Dictionary<(string, string), List<string>>();

        foreach (var path in _content.EnumerateFiles(SectionRoot, "*.txt"))
        {
            var document = CwDocument.Parse(_content.Read(path), CwParseOptions.Lenient);

            foreach (var template in document.Nodes.Where(n => n.Key == "ship_section_template"))
            {
                if (template.Block is not { } body ||
                    body.GetString("ship_size") is not { Length: > 0 } size ||
                    body.GetString("fits_on_slot") is not { Length: > 0 } slot ||
                    body.GetString("entity") is not { Length: > 0 } entity)
                {
                    continue;
                }

                if (!found.TryGetValue((size, slot), out var list))
                {
                    found[(size, slot)] = list = [];
                }

                list.Add(entity);
            }
        }

        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
    }

    /// <summary>
    /// The meshes one class of ship is drawn from, and the set that models them.
    /// </summary>
    /// <param name="Owner">The set whose folder the meshes are in.</param>
    /// <param name="Meshes">What to draw, which for a sectioned ship is several.</param>
    private sealed record ShipHull(string Owner, IReadOnlyList<string> Meshes);

    /// <summary>
    /// What to draw for one class of ship in one set, following the fallbacks until something has it.
    /// </summary>
    /// <remarks>
    /// A set that models no ships at all flies its fallback's, which is what the game does and what
    /// the page says beside it. Walking the chain here rather than giving up means those sets have a
    /// gallery, and pointing at the owner's pictures means it costs nothing to draw.
    /// </remarks>
    private ShipHull? HullOf(
        GraphicalCultureDefinition set,
        ShipClassShape ship,
        IReadOnlyDictionary<(string Size, string Slot), IReadOnlyList<string>> sections,
        IReadOnlyDictionary<string, GraphicalCultureDefinition> sets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = set; current is not null && seen.Add(current.Key);)
        {
            if (Modelled(current.Key, ship, sections) is { Count: > 0 } meshes)
            {
                return new ShipHull(current.Key, meshes);
            }

            current = current.Fallback is { Length: > 0 } next ? sets.GetValueOrDefault(next) : null;
        }

        return null;
    }

    /// <summary>
    /// The meshes a set models for one class of ship, or none where it models none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Through the entities rather than by guessing at mesh names, because the two do not match: the
    /// construction ship's size is <c>constructor</c> and its mesh is <c>construction_ship</c>, and
    /// the entity is the game's own statement of which is which.
    /// </para>
    /// <para>
    /// Every one of them is a frame with sections hung on it, a corvette as much as a battleship -
    /// the corvette simply has one slot where the battleship has three. So the size's own entity is
    /// read only as the set saying it flies the class at all, and what is drawn comes from the
    /// slots: one section each, the last by name, since the files are ordered and the later ones
    /// carry the heavier guns.
    /// </para>
    /// <para>
    /// The frame itself is never drawn and must not be looked for. It has no geometry, and the sets
    /// share them freely - <c>humanoid_01_corvette_entity</c> names
    /// <c>molluscoid_01_corvette_frame_mesh</c> - so treating a one-slot ship as its entity's mesh
    /// drew nothing at all for four classes across every set.
    /// </para>
    /// </remarks>
    private IReadOnlyList<string> Modelled(
        string key,
        ShipClassShape ship,
        IReadOnlyDictionary<(string Size, string Slot), IReadOnlyList<string>> sections)
    {
        var flown = InService(key);

        if (flown.Count == 0)
        {
            return [];
        }

        // A class this set has no entity for is a class it does not fly.
        if (!flown.ContainsKey($"{key}_{ship.Key}_entity"))
        {
            return [];
        }

        var meshes = new List<string>(ship.Slots.Count);

        foreach (var slot in ship.Slots)
        {
            var wanted = sections.GetValueOrDefault((ship.Key, slot)) ?? [];

            var mesh = wanted
                .Select(entity => flown.GetValueOrDefault($"{key}_{entity}"))
                .LastOrDefault(found => found is { Length: > 0 });

            if (mesh is { Length: > 0 })
            {
                meshes.Add(mesh);
            }
        }

        // A bow with no stern is half a ship, and half a ship drawn is worse than none.
        return meshes.Count == ship.Slots.Count ? meshes : [];
    }

    /// <summary>
    /// Draws each arkship from its own model, the way a shipset is drawn from a corvette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="Bake"/> because an arkship is not reached the way a set is. A set
    /// names its meshes after itself and keeps them in a folder of its own name; all nine arkships
    /// sit together in <c>gfx/models/ships/other</c>, sharing it with the enclaves and the crystal
    /// stations, and are named after neither the ship size nor any set. The ship size's
    /// <c>entity</c> is the game's own link across, so it is followed rather than guessed at.
    /// </para>
    /// <para>
    /// The three arkship graphical cultures are no help here and are a trap: they carry the
    /// lighting rig and nothing else, declare no models, and fall back to <c>avian_01</c> - so
    /// running them through <see cref="Bake"/> produced three identical avian corvettes.
    /// </para>
    /// </remarks>
    public (IReadOnlyList<ArkshipDefinition> Arkships, ShipBakeReport Report) BakeArkships(
        IReadOnlyList<ArkshipDefinition> arkships,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(arkships);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var results = new List<ArkshipDefinition>(arkships.Count);
        var failures = new List<string>();
        var rendered = 0;
        long bytes = 0;

        progress?.Report($"Drawing arkships ({arkships.Count})");

        foreach (var arkship in arkships)
        {
            if (ArkshipHull(arkship) is not { } mesh)
            {
                failures.Add($"{arkship.Key}: no model found for entity {arkship.Entity}");
                results.Add(arkship);
                continue;
            }

            try
            {
                if (Draw(mesh) is not { } png)
                {
                    failures.Add($"{arkship.Key}: nothing in {mesh} could be drawn");
                    results.Add(arkship);
                    continue;
                }

                var destination = $"ships/arkships/{arkship.Key}.png";
                _file.WriteAllBytes(Path.Combine(outputDirectory, destination), png);

                rendered++;
                bytes += png.Length;
                results.Add(arkship with { Preview = destination });
            }
            catch (Exception ex)
                when (ex is InvalidDataException or NotSupportedException or IOException
                    or InvalidOperationException)
            {
                failures.Add($"{arkship.Key}: {ex.Message}");
                results.Add(arkship);
            }
        }

        return (results, new ShipBakeReport(rendered, bytes, failures));
    }

    /// <summary>
    /// The hull an arkship's entity stands for.
    /// </summary>
    /// <remarks>
    /// The entity a ship size names is the <em>frame</em> - the armature the hull's sections hang
    /// on, a few vertices that draw nothing - and the hull is its sibling. Both are named after the
    /// same stem, so the stem is what is wanted and the frame is what must not be taken. Searched
    /// for rather than composed into a path, since nothing in the files says which folder under
    /// <c>gfx/models/ships</c> an arkship lives in.
    /// </remarks>
    private string? ArkshipHull(ArkshipDefinition arkship)
    {
        if (arkship.Entity is not { Length: > 0 } entity)
        {
            return null;
        }

        const string EntitySuffix = "_entity";

        var stem = entity.EndsWith(EntitySuffix, StringComparison.Ordinal)
            ? entity[..^EntitySuffix.Length]
            : entity;

        return _content
            .EnumerateFiles(ModelRoot, $"{stem}.mesh", recursive: true)
            .FirstOrDefault();
    }

    /// <summary>
    /// The meshes a set's own preview is drawn from, following its fallbacks as the picker does.
    /// </summary>
    /// <remarks>
    /// Falling back is the game's own arrangement, declared by the set: a set without artwork of its
    /// own is played with the artwork of the one it names. Solarpunk has no ship models at all and
    /// is flown with fungoid hulls, so a fungoid hull is what its picker entry should show.
    /// </remarks>
    /// <param name="set">The graphical culture.</param>
    /// <param name="sets">Every set, so the fallbacks can be followed.</param>
    /// <returns>What the preview is drawn from, which may be nothing.</returns>
    public IReadOnlyList<string> PreviewMeshes(
        GraphicalCultureDefinition set,
        IReadOnlyDictionary<string, GraphicalCultureDefinition> sets)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(sets);

        var sections = Sections();

        // A corvette where there is one, and otherwise whatever the set flies smallest - which is
        // the same choice Bake makes, and is made here so a test can ask what would be drawn.
        foreach (var ship in ShipClasses().OrderBy(c => c.Key == "corvette" ? 0 : 1))
        {
            if (HullOf(set, ship, sections, sets) is { } hull)
            {
                return hull.Meshes;
            }
        }

        return [];
    }

    /// <summary>
    /// What each of a set's entities draws, which is the game saying a mesh is flown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entity is the game's unit of a thing in the world: it names the mesh to draw, the
    /// animations it can play and the places weapons attach. The <c>.asset</c> files declare them,
    /// and a mesh no entity names is an offcut left in the folder - the reptilian set keeps one
    /// called <c>_test</c>.
    /// </para>
    /// <para>
    /// Keyed by the entity's name rather than gathered into a set of files, because that name is
    /// how everything else refers to a ship: a ship size is drawn by <c>&lt;set&gt;_&lt;size&gt;_entity</c>
    /// and a section template names its own. A frame, which draws nothing, answers with an empty
    /// string - it is still the game saying the class is flown.
    /// </para>
    /// </remarks>
    private IReadOnlyDictionary<string, string> InService(string key)
    {
        if (_inService.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var flown = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var directory in Folders(key))
        {
            Read(directory);
        }

        _inService[key] = flown;
        return flown;

        void Read(string directory)
        {
            var files = MeshFiles(directory);

            foreach (var asset in _content.EnumerateFiles(directory, "*_entities.asset"))
            {
                var document = CwDocument.Parse(_content.Read(asset), CwParseOptions.Lenient);

                foreach (var entity in document.Nodes.Where(n => n.Key == "entity"))
                {
                    if (entity.Block?.GetString("name") is not { Length: > 0 } name)
                    {
                        continue;
                    }

                    // An entity naming a mesh from another set is how a set borrows a hull it never
                    // modelled, and is not one of this set's own. The game spells a path as it
                    // likes; the content index does not care, and neither should matching it.
                    var mesh = entity.Block.GetString("pdxmesh") is { Length: > 0 } declared &&
                        files.GetValueOrDefault(declared) is { Length: > 0 } file &&
                        Path.GetFileName(file).StartsWith(key, StringComparison.Ordinal) &&
                        _content.Contains(file)
                            ? file
                            : string.Empty;

                    // First wins, so a set's own folder is not overwritten by a titan's - though in
                    // practice the two never name the same entity.
                    flown.TryAdd(name, mesh);
                }
            }
        }
    }

    /// <summary>
    /// Every folder a set keeps models in, which is not always the one named after it.
    /// </summary>
    /// <remarks>
    /// Three classes are kept apart from the rest, a folder of sets each: <c>titans</c>,
    /// <c>colossus</c> and <c>juggernauts</c>. Looking only in <c>ships/&lt;set&gt;</c> found the
    /// fleet and none of the three, so a page of ships had no titan in it - and the game builds one
    /// for twenty of the sets.
    /// </remarks>
    /// <param name="key">The set.</param>
    /// <returns>The directories, nearest first.</returns>
    private IEnumerable<string> Folders(string key)
    {
        if (_content.ContainsDirectory($"{ModelRoot}/{key}"))
        {
            yield return $"{ModelRoot}/{key}";
        }

        foreach (var apart in ShipsKeptApart)
        {
            if (_content.ContainsDirectory($"{ModelRoot}/{apart}/{key}"))
            {
                yield return $"{ModelRoot}/{apart}/{key}";
            }
        }
    }

    /// <summary>The classes the game files away from the rest of a set's models, a folder each.</summary>
    private static readonly string[] ShipsKeptApart = ["titans", "colossus", "juggernauts"];

    /// <summary>
    /// Which file each of a set's declared meshes is, by the name entities refer to it by.
    /// </summary>
    /// <remarks>
    /// Every <c>.gfx</c> in the folder rather than the one named after the set and its ships. A
    /// titan's declarations are in <c>_humanoid_01_titan_meshes.gfx</c>, not
    /// <c>_humanoid_01_ships_meshes.gfx</c>, so looking for the one name found the entity, failed to
    /// resolve its mesh, and left twenty sets without the largest ship they build.
    /// </remarks>
    /// <param name="directory">The folder to read.</param>
    /// <returns>The mesh files, by declared name.</returns>
    private IReadOnlyDictionary<string, string> MeshFiles(string directory)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var settings in _content.EnumerateFiles(directory, "*.gfx"))
        {
            var document = CwDocument.Parse(_content.Read(settings), CwParseOptions.Lenient);

            foreach (var node in Declarations(document))
            {
                if (node.Block?.GetString("name") is { Length: > 0 } name &&
                    node.Block.GetString("file") is { Length: > 0 } file)
                {
                    files[name] = file;
                }
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
    /// <param name="meshPath">The mesh.</param>
    /// <returns>The picture, or null where nothing in it could be drawn.</returns>
    private byte[]? Draw(string meshPath) => Draw([meshPath], owner: null);

    /// <summary>
    /// Draws several meshes into one picture, which is how a sectioned ship is a ship.
    /// </summary>
    /// <remarks>
    /// Straight concatenation, because the game authors each section already in place: a humanoid
    /// battleship's bow runs from z -14.2 to 0.7 and its stern from -1.53 to 9.26, about the one
    /// origin. Nothing is moved, and the renderer frames the three together as it would frame one.
    /// </remarks>
    /// <param name="meshPaths">The meshes, which for a whole hull is one.</param>
    /// <param name="owner">The set that models them, whose folders the paint may be in.</param>
    /// <returns>The picture, or null where nothing in them could be drawn.</returns>
    private byte[]? Draw(IReadOnlyList<string> meshPaths, string? owner)
    {
        var parts = new List<MeshPart>();
        var textures = new Dictionary<string, DdsImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var meshPath in meshPaths)
        {
            var mesh = Dress(PortraitMesh.Load(_content.Read(meshPath)), meshPath, owner);

            parts.AddRange(mesh.Parts);

            foreach (var name in mesh.Parts
                         .Where(ModelRenderer.IsVisible)
                         .Select(p => p.Texture!)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!textures.ContainsKey(name) && Paint(name, meshPath, owner) is { } path)
                {
                    textures[name] = DdsReader.Read(_content.Read(path));
                }
            }
        }

        return _renderer.Render(new PortraitMesh(parts), textures) is { } image
            ? PngWriter.Encode(image)
            : null;
    }

    /// <summary>
    /// Where a part's texture actually is, since it is named by file alone.
    /// </summary>
    /// <remarks>
    /// Beside the mesh is where the game looks and where it nearly always is. The titans are the
    /// exception, being filed in a folder of their own: three sets keep no paint in there at all and
    /// their titan is painted from the set's ordinary folder, so looking only beside the mesh drew
    /// three blank titans.
    /// </remarks>
    /// <param name="texture">The file name the part asks for.</param>
    /// <param name="meshPath">The mesh it belongs to.</param>
    /// <param name="owner">The set that models it, where one is known.</param>
    /// <returns>The path, or null where the file is nowhere the set keeps art.</returns>
    private string? Paint(string texture, string meshPath, string? owner)
    {
        var beside = $"{meshPath[..meshPath.LastIndexOf('/')]}/{texture}";

        if (_content.Contains(beside))
        {
            return beside;
        }

        return owner is { Length: > 0 }
            ? Folders(owner).Select(f => $"{f}/{texture}").FirstOrDefault(_content.Contains)
            : null;
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
    private PortraitMesh Dress(PortraitMesh mesh, string meshPath, string? owner)
    {
        if (mesh.Parts.All(p => Painted(p.Texture, meshPath, owner)))
        {
            return mesh;
        }

        var declared = Declared(meshPath);

        return new PortraitMesh(
        [
            .. mesh.Parts.Select(part => Painted(part.Texture, meshPath, owner)
                ? part
                : part with
                {
                    // The settings name a shape and which of its meshes, since a shape painted with
                    // several materials is several meshes under one name. Older sets give no index
                    // and mean the shape entire.
                    Texture = declared.GetValueOrDefault((part.Name, part.Index))
                        ?? declared.GetValueOrDefault((part.Name, 0))
                        ?? part.Texture,
                })
        ])
        {
            Bones = mesh.Bones,
        };
    }

    /// <summary>
    /// Whether a part's own texture is a file that exists beside it.
    /// </summary>
    /// <remarks>
    /// Naming one is not the same as having one. Three ships name a texture the set does not carry -
    /// the necroid colony ship asks for <c>colony_ship_diffuse.dds</c> where the folder holds
    /// <c>necroid_01_colony_ship_diffuse.dds</c>, the set's own prefix missing from the mesh - and
    /// the game is unbothered because its <c>.gfx</c> declares the right file. Read as "the mesh
    /// knows its own texture", those three drew nothing at all.
    /// </remarks>
    /// <param name="texture">What the part names, which may be nothing.</param>
    /// <param name="meshPath">The mesh it belongs to.</param>
    /// <param name="owner">The set that models it, where one is known.</param>
    /// <returns>True where the file is there to be read.</returns>
    private bool Painted(string? texture, string meshPath, string? owner) =>
        texture is { Length: > 0 } name && Paint(name, meshPath, owner) is not null;

    /// <summary>What the set's own mesh settings say each part of a mesh is textured with.</summary>
    private IReadOnlyDictionary<(string Name, int Index), string> Declared(string meshPath)
    {
        var folder = meshPath[..meshPath.LastIndexOf('/')];
        var textures = new Dictionary<(string, int), string>();

        // Every .gfx in the folder, for the reason MeshFiles gives: a titan's declarations are in
        // _<set>_titan_meshes.gfx rather than _<set>_ships_meshes.gfx, and the psionic set is one of
        // the ones whose meshes name no texture of their own - so read by the one name, its titan
        // was three hulls with nothing to paint them.
        foreach (var node in _content
                     .EnumerateFiles(folder, "*.gfx")
                     .SelectMany(settings => Declarations(
                         CwDocument.Parse(_content.Read(settings), CwParseOptions.Lenient))))
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
                    var index = int.TryParse(settingsBlock.GetString("index"), System.Globalization.CultureInfo.InvariantCulture, out var declared)
                        ? declared
                        : 0;

                    textures[(name, index)] = diffuse;
                }
            }
        }

        return textures;
    }
}
