using Sem.Assets;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Extraction.Tests;

/// <summary>
/// Reading and drawing the game's portrait models.
/// </summary>
/// <remarks>
/// These check the model files are understood and that something recognisable comes out. Whether a
/// likeness actually looks like its species is a question only an eye can answer, and that was
/// done by looking at the results rather than by asserting on pixels.
/// </remarks>
public sealed class PortraitRenderingTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static string ModelPath(string relative) =>
        Path.Combine(InstallRoot!, "gfx", "models", "portraits", relative.Replace('/', Path.DirectorySeparatorChar));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsAPortraitModelIntoItsParts()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(ModelPath("mammalian/mammalian_01_portrait.mesh")));

        // A portrait is assembled from separately named pieces rather than one lump.
        Assert.True(mesh.Parts.Count >= 4, $"Expected several parts, found {mesh.Parts.Count}.");
        Assert.Contains(mesh.Parts, p => p.Name.Contains("body", StringComparison.OrdinalIgnoreCase));

        foreach (var part in mesh.Parts)
        {
            Assert.NotEmpty(part.Positions);
            Assert.NotEmpty(part.Triangles);
            Assert.Equal(part.Positions.Length, part.TexCoords.Length);
            Assert.Equal(0, part.Triangles.Length % 3);
            Assert.All(part.Triangles, i => Assert.InRange(i, 0, part.Positions.Length - 1));
            Assert.False(string.IsNullOrEmpty(part.Texture), $"{part.Name} names no texture.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PortraitPartsAreFlatLayersRatherThanSculptedInTheRound()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // This is why the renderer paints layers in order instead of using a depth buffer, and it
        // is worth catching if a future model breaks the assumption.
        var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(ModelPath("mammalian/mammalian_01_portrait.mesh")));

        foreach (var part in mesh.Parts)
        {
            var depths = part.Positions.Select(p => p.Z).Distinct().ToList();
            Assert.True(depths.Count == 1, $"{part.Name} spans {depths.Count} depths.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void DrawsAPortraitThatIsNeitherBlankNorSolid()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var path = ModelPath("mammalian/mammalian_01_portrait.mesh");

        // Posed as the game poses it. A model's vertices are stored in the space it was drawn in —
        // this one sits thirty units above its own origin — and the skeleton's rest pose is what
        // carries it to where the game shows it. Drawn unposed it lands outside the frame.
        var pose = PortraitPose.Read(
            SafeFile.ReadAllBytes(ModelPath("mammalian/mammalian_01_portrait_happy.anim")));

        var mesh = pose.ApplyTo(PortraitMesh.Load(SafeFile.ReadAllBytes(path)));
        var directory = Path.GetDirectoryName(path)!;

        var textures = mesh.Parts
            .Select(p => p.Texture)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => File.Exists(Path.Combine(directory, name)))
            .ToDictionary(
                name => name,
                name => DdsReader.Read(SafeFile.ReadAllBytes(Path.Combine(directory, name))),
                StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(textures);

        var settings = new RenderSettings();
        var image = new PortraitRenderer(settings).Render(mesh, textures);

        Assert.Equal((settings.Width, settings.Height), (image.Width, image.Height));

        // The game's own proportions, so a portrait is the shape it composites into a room.
        Assert.Equal(575.0 / 380.0, (double)image.Width / image.Height, 2);

        var alphas = image.Pixels.Where((_, i) => i % 4 == 3).ToList();
        var covered = alphas.Count(a => a > 32) / (double)alphas.Count;

        // A figure fills a good part of the frame and leaves the rest empty. All or nothing means
        // the drawing went wrong.
        Assert.InRange(covered, 0.15, 0.95);

        // A likeness has many colours; one flat colour means the texture was not applied.
        var distinct = new HashSet<int>();
        for (var i = 0; i < image.Pixels.Length; i += 4)
        {
            if (image.Pixels[i + 3] > 128)
            {
                distinct.Add((image.Pixels[i] << 16) | (image.Pixels[i + 1] << 8) | image.Pixels[i + 2]);
            }
        }

        Assert.True(distinct.Count > 200, $"Only {distinct.Count} distinct colours; the texture may not be applied.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryPortraitModelTheGameShipsCanBeRead()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var root = Path.Combine(InstallRoot!, "gfx", "models", "portraits");
        Skip.If(!Directory.Exists(root), "No portrait models in this installation.");

        var read = 0;
        var failures = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*.mesh", SearchOption.AllDirectories))
        {
            try
            {
                var mesh = PortraitMesh.Load(SafeFile.ReadAllBytes(path));
                read++;

                Assert.All(mesh.Parts, p => Assert.All(p.Triangles, i => Assert.InRange(i, 0, p.Positions.Length - 1)));
            }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        Assert.True(read > 300, $"Expected the full set of models, read {read}.");
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {read} models could not be read:\r\n" + string.Join("\r\n", failures.Take(10)));
    }
}
