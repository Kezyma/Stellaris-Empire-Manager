using Sem.Assets;
using Sem.GameData;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction.Tests;

/// <summary>
/// Drawing the game's ship models.
/// </summary>
/// <remarks>
/// A ship is not a portrait, and these are the differences worth holding onto: it is sculpted in
/// the round rather than assembled from flat cards, so it needs a depth buffer; it carries a
/// collision hull the game never draws; and it comes out filling its frame rather than at a fixed
/// scale. Whether a hull looks right is a question for an eye, and that was answered by looking.
/// </remarks>
public sealed class ShipRenderingTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private const string Corvette = "mammalian_01/mammalian_01_corvette_S3.mesh";

    private static string ModelPath(string relative) =>
        Path.Combine(InstallRoot!, "gfx", "models", "ships", relative.Replace('/', Path.DirectorySeparatorChar));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AShipIsSculptedInTheRoundRatherThanBuiltFromFlatLayers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The opposite of the portraits, which are flat cards at one depth each. This is the whole
        // reason there is a second renderer, so it is worth knowing if it ever stops being true.
        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(ModelPath(Corvette)));
        var hull = mesh.Parts.Single(ModelRenderer.IsVisible);

        Assert.True(hull.Positions.Length > 1000, $"Expected a sculpted hull, found {hull.Positions.Length} vertices.");
        Assert.True(hull.Positions.Select(p => p.Z).Distinct().Count() > 100, "The hull sits at one depth.");
        Assert.Equal(hull.Positions.Length, hull.Normals.Length);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheCollisionHullIsNotDrawn()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(ModelPath(Corvette)));
        var hidden = mesh.Parts.Where(p => !ModelRenderer.IsVisible(p)).ToList();

        // A ship mesh carries a bare box for the game's collisions. Drawing it would wrap the ship
        // in an untextured crate; having no texture is what tells it apart, since some sets give
        // their collision hulls coordinates and geometry like any other part.
        Assert.NotEmpty(hidden);
        Assert.All(hidden, p => Assert.True(string.IsNullOrEmpty(p.Texture), $"{p.Name} names a texture."));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void DrawsAShipThatFillsItsFrameWithoutOverflowingIt()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var settings = new ModelSettings();
        var image = Draw(Corvette, settings);

        Assert.Equal((settings.Width, settings.Height), (image.Width, image.Height));

        var alphas = image.Pixels.Where((_, i) => i % 4 == 3).ToList();
        var covered = alphas.Count(a => a > 32) / (double)alphas.Count;

        // A ship seen three-quarters on takes up a fair share of the picture and leaves the corners
        // empty. All or nothing means the fit went wrong.
        Assert.InRange(covered, 0.15, 0.85);

        // The fit is measured from what the camera sees, so the model comes right up to the margin
        // on whichever axis is tighter rather than sitting small in the middle.
        var (across, down) = Spread(image);

        Assert.True(Math.Max(across, down) > 0.9,
            $"The model fills only {across:P0} across and {down:P0} down the frame it was fitted to.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheNearSideOfTheHullIsDrawnOverTheFarSide()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // Without a depth buffer, whichever triangle happened to come last would win — and for a
        // closed hull that is as often the far side as the near one, which reads as holes. Lit from
        // one direction, a hull that is right has a clear bright side and a clear dark side; one
        // painted in submission order comes out mottled.
        var image = Draw(Corvette, new ModelSettings());

        var (top, bottom) = Brightness(image);

        Assert.True(top > bottom * 1.15,
            $"The lit surfaces are not clearly brighter than the shaded ones ({top:F1} against {bottom:F1}).");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EverySetAPlayerCanChooseGetsAShipOfItsOwn()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The baker itself rather than a simplified copy of it, because the parts worth testing are
        // the ones a copy would leave out: following a set's fallback when it models no ships, and
        // reading the textures out of the set's mesh settings when the mesh names none itself.
        var content = LayeredContent.ForInstall(InstallRoot!);
        var sets = new GameDataExtractor(content).Extract().GraphicalCultures;

        Assert.NotEmpty(sets);

        var output = Directory.CreateTempSubdirectory("sem-ships-");

        try
        {
            var (drawn, report) = new ShipBaker(content, new SafeFile(WritePolicy.ForApplication()))
                .Bake(sets, output.FullName);

            // The Unbidden's whole fleet, which the game ships no paint for. Their hulls are drawn by
            // PdxMeshExtraDimensionalShip and their mesh settings declare that shader and nothing
            // else - there is no diffuse texture for any of them anywhere in the installation, only
            // for their anchor station and their portal. A renderer that paints triangles cannot draw
            // one.
            //
            // They are still resolved rather than skipped, because resolving them is what stops the
            // set borrowing a mammalian corvette it has never built. An empty gallery is true; that
            // one was not.
            //
            // Named rather than tolerated by count, so the next one that will not draw is noticed.
            string[] unpaintable =
            [
                "extra_dimensional_01 small_ship_ed",
                "extra_dimensional_01 medium_ship_ed",
                "extra_dimensional_01 large_ship_ed",
                "extra_dimensional_01 construction_ship_ed",
            ];

            var unexpected = report.Failures
                .Where(f => !unpaintable.Any(known => f.StartsWith(known, StringComparison.Ordinal)))
                .ToList();

            Assert.True(
                unexpected.Count == 0,
                string.Join(
                    Environment.NewLine + "  ",
                    unexpected.Prepend("Ships that would not draw:")));

            // Every set a player is offered flies something, whether its own or its fallback's.
            var offered = drawn.Where(s => s.Selectable is not AlwaysRequirement { Value: false }).ToList();

            Assert.All(offered, set =>
                Assert.False(string.IsNullOrEmpty(set.ShipPreview), $"{set.Key} has no ship."));

            // And every picture drawn is a picture of something. Slots were relaxed so that a
            // starbase's module hardpoints could be left out without losing the starbase, which means
            // a ship missing its stern would now draw rather than being refused - so the whole fleet
            // is measured, not the preview alone.
            Assert.All(report.Fleet.DistinctBy(f => f.Image), ship =>
            {
                var file = Path.Combine(output.FullName, ship.Image.Replace('/', Path.DirectorySeparatorChar));

                Assert.True(
                    new FileInfo(file).Length > 2_000,
                    $"{ship.Set} {ship.ShipClass} drew only {new FileInfo(file).Length} bytes.");
            });

            Assert.All(
                offered,
                set => Assert.True(
                    File.Exists(Path.Combine(output.FullName, set.ShipPreview!.Replace('/', Path.DirectorySeparatorChar))),
                    $"{set.Key} claims a picture that was not written."));

            Assert.True(offered.Count >= 20, $"Only {offered.Count} sets are offered.");

            var fleets = report.Fleet
                .GroupBy(f => f.Set, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            // The four classes the game files away from the rest of a set's models: three in a folder
            // of their own and the starbases in one flat folder every set shares. All four were
            // missing, from an asset glob that wanted *_entities.asset and a folder list that did not
            // know about ships/starbases.
            Assert.All(
                new[] { "colossus", "juggernaut", "starbase_citadel", "habitat_central_complex" },
                wanted => Assert.Contains(
                    fleets["humanoid_01"],
                    ship => string.Equals(ship.ShipClass, wanted, StringComparison.Ordinal)));

            // And the canonical name wins where two classes are one ship. A bio titan is drawn by the
            // titan's own entity, and read in file order every set called its titan a bio titan.
            Assert.Contains(fleets["humanoid_01"], s => s.ShipClass == "titan");
            Assert.DoesNotContain(fleets["humanoid_01"], s => s.ShipClass == "bio_titan");

            // A set with a fleet of its own flies it, and does not borrow. The Contingency models a
            // Warform and a Seeker; it has never built a corvette, and its meshes carry no prefix so
            // a name test threw every one of them away.
            Assert.All(
                fleets["ai_01"],
                ship => Assert.Equal("ai_01", ship.Image.Split('/')[1]));

            Assert.Contains(fleets["ai_01"], s => s.ShipClass == "large_ship_ai");

            // While a set that models nothing at all still shows what it flies in game.
            Assert.All(
                fleets["ai_02"],
                ship => Assert.Equal("mammalian_01", ship.Image.Split('/')[1]));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OneShapePaintedWithSeveralMaterialsReadsAsSeveralParts()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The plantoid corvette is a single shape holding three meshes — a strip of panels, a
        // translucent piece, and the hull itself, in that order. Reading one mesh per shape took the
        // panels and left the ship out, and since the panels are a small dark tiling sheet the
        // preview came out as a smudge. The hull is not first, which is what made this invisible
        // everywhere else: six other corvettes are built this way and happen to declare theirs
        // first, and lost only an overlay.
        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(ModelPath("plantoid_01/plantoid_01_corvette_S3.mesh")));
        var shape = mesh.Parts.Where(p => p.Name == "polySurface84Shape").ToList();

        Assert.Equal(3, shape.Count);
        Assert.Equal([0, 1, 2], shape.Select(p => p.Index));

        var hull = shape.MaxBy(p => p.Positions.Length)!;

        Assert.Equal("plantoid_01_corvette_diffuse.dds", hull.Texture);
        Assert.True(hull.Positions.Length > 1000, $"The hull has only {hull.Positions.Length} vertices.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheHullDrawnForASetIsAWholeShipRatherThanAnOffcut()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // A ship folder holds more meshes than the game flies, and the leftovers are not small
        // versions of the hull — they are pieces, a few hundred vertices wearing a tiling panel
        // texture, which drew as a dark smudge where the plantoid corvette should have been. The
        // guard is the same shape as the sculpted-hull assertion above, applied to whatever the
        // baker actually picked rather than to one mesh named here.
        var content = LayeredContent.ForInstall(InstallRoot!);
        var sets = new GameDataExtractor(content).Extract().GraphicalCultures;
        var byKey = sets.ToDictionary(s => s.Key, StringComparer.Ordinal);

        var baker = new ShipBaker(content, new SafeFile(WritePolicy.ForApplication()));

        var offered = sets
            .Where(s => s.Selectable is not AlwaysRequirement { Value: false })
            .Select(set => (set.Key, Meshes: baker.PreviewMeshes(set, byKey)))
            .Where(pair => pair.Meshes.Count > 0)
            .ToList();

        Assert.NotEmpty(offered);

        Assert.All(offered, pair =>
        {
            // Counted over the parts that carry texture coordinates rather than the ones already
            // naming a texture, since the psionic and mindwarden hulls are given theirs later, out
            // of the set's mesh settings, and would otherwise measure as empty.
            //
            // And across every mesh the preview is drawn from, since a sectioned ship is several -
            // a bow on its own would pass this and be half a ship.
            var vertices = pair.Meshes
                .Select(path => PortraitMesh.Load(content.Read(path)))
                .SelectMany(mesh => mesh.Parts.Where(p => p.TexCoords.Length > 0))
                .Sum(p => p.Positions.Length);

            var drawn = string.Join(", ", pair.Meshes.Select(Path.GetFileName));

            Assert.True(
                vertices > 1000,
                $"{pair.Key} is drawn by {drawn}, which has only {vertices} vertices.");
        });
    }

    private static DdsImage Draw(string relative, ModelSettings settings)
    {
        var path = ModelPath(relative);
        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(path));
        var directory = Path.GetDirectoryName(path)!;

        var textures = mesh.Parts
            .Where(ModelRenderer.IsVisible)
            .Select(p => p.Texture!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => File.Exists(Path.Combine(directory, name)))
            .ToDictionary(
                name => name,
                name => DdsReader.Read(SafeFile.ReadAllBytes(Path.Combine(directory, name))),
                StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(textures);

        return new ModelRenderer(settings).Render(mesh, textures)
            ?? throw new InvalidDataException($"Nothing in {relative} could be drawn.");
    }

    /// <summary>How much of each axis what was drawn spans, as a fraction of the picture.</summary>
    private static (double Across, double Down) Spread(DdsImage image)
    {
        int left = image.Width, right = -1, top = image.Height, bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A <= 32)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0
            ? (0, 0)
            : ((right - left + 1) / (double)image.Width, (bottom - top + 1) / (double)image.Height);
    }

    /// <summary>Average brightness of the lit half and the shaded half of what was drawn.</summary>
    private static (double Lit, double Shaded) Brightness(DdsImage image)
    {
        var values = new List<double>();

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var (b, g, r, a) = image[x, y];

                if (a > 128)
                {
                    values.Add((r + g + b) / 3.0);
                }
            }
        }

        values.Sort();

        var half = values.Count / 2;
        return (values.Skip(half).Average(), values.Take(half).Average());
    }
}
