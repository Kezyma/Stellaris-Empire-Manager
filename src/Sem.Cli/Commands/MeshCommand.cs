using System.CommandLine;
using Sem.Assets;
using Sem.Io;
using Sem.MeshBake;

namespace Sem.Cli.Commands;

/// <summary>
/// Prints the structure of a Paradox model file, for working out how portraits are put together.
/// </summary>
public static class MeshCommand
{
    /// <summary>Builds the command line this verb is invoked by.</summary>
    /// <returns>The verb, its options and what to run. Bakes the game's portrait and ship meshes into flat images.</returns>
    public static Command Create()
    {
        var pathArgument = new Argument<FileInfo>("file")
        {
            Description = "The .mesh or .anim file to read.",
        };

        var depthOption = new Option<int>("--depth", "-d")
        {
            Description = "How far down the tree to print.",
            DefaultValueFactory = _ => 3,
        };

        var renderOption = new Option<FileInfo?>("--render", "-r")
        {
            Description = "Also draw the model to this PNG file.",
        };

        var command = new Command("mesh", "Print the structure of a Paradox model file.")
        {
            pathArgument,
            depthOption,
            renderOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(pathArgument)!.FullName,
            parseResult.GetValue(depthOption),
            parseResult.GetValue(renderOption)?.FullName));

        return command;
    }

    private static int Run(string path, int maxDepth, string? renderTo)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"'{path}' does not exist.");
            return 1;
        }

        var bytes = SafeFile.ReadAllBytes(path);
        var asset = PdxAssetReader.Read(bytes);

        if (renderTo is not null)
        {
            return Render(path, bytes, renderTo);
        }

        Console.WriteLine($"{Path.GetFileName(path)}");
        Console.WriteLine();
        Print(asset, 0, maxDepth);

        Console.WriteLine();
        Console.WriteLine($"Total nodes: {asset.Descendants().Count()}");
        Console.WriteLine($"Meshes     : {asset.Descendants().Count(n => n.Name == "mesh")}");

        // Where the model sits, which is the question when two of them are meant to be one ship. A
        // warship above a corvette is a bow, a middle and a stern in separate files, and whether
        // they are authored already in place or each about its own origin is the whole of how they
        // are put together - and nothing in the game's own files says which.
        var mesh = PortraitMesh.Load(bytes);

        if (mesh.Parts.Count > 0)
        {
            var (min, max) = mesh.Bounds;

            Console.WriteLine(
                $"Bounds     : x {min.X:0.##} to {max.X:0.##}, "
                    + $"y {min.Y:0.##} to {max.Y:0.##}, z {min.Z:0.##} to {max.Z:0.##}");
        }
        return 0;
    }

    /// <summary>
    /// Draws the model, taking each part's texture from beside the model file, which is where the
    /// game keeps them.
    /// </summary>
    private static int Render(string meshPath, byte[] bytes, string outputPath)
    {
        var mesh = PortraitMesh.Load(bytes);
        var directory = Path.GetDirectoryName(meshPath)!;
        var textures = new Dictionary<string, DdsImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in mesh.Parts.Select(p => p.Texture).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var texturePath = Path.Combine(directory, name);
            if (!File.Exists(texturePath))
            {
                Console.WriteLine($"  texture not found: {name}");
                continue;
            }

            try
            {
                textures[name] = DdsReader.Read(SafeFile.ReadAllBytes(texturePath));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                Console.WriteLine($"  texture unreadable: {name} ({ex.Message})");
            }
        }

        var image = new PortraitRenderer().Render(mesh, textures);
        var png = PngWriter.Encode(image);

        var sandbox = SandboxLayout.Discover(Environment.CurrentDirectory);
        new SafeFile(sandbox.CreateDevelopmentPolicy()).WriteAllBytes(outputPath, png);

        var (min, max) = mesh.Bounds;

        Console.WriteLine($"{Path.GetFileName(meshPath)}");
        Console.WriteLine($"  bounds   : x {min.X:F2}..{max.X:F2}  y {min.Y:F2}..{max.Y:F2}  z {min.Z:F2}..{max.Z:F2}");
        Console.WriteLine($"  triangles: {mesh.Parts.Sum(p => p.Triangles.Length / 3):N0}");
        Console.WriteLine($"  textures : {string.Join(", ", textures.Keys)}");
        Console.WriteLine();

        foreach (var part in mesh.Parts)
        {
            var px = part.Positions;
            Console.WriteLine(
                $"  {part.Name,-22} {px.Length,4} verts  " +
                $"y {px.Min(p => p.Y):F2}..{px.Max(p => p.Y):F2}  " +
                $"z {px.Min(p => p.Z):F2}..{px.Max(p => p.Z):F2}  " +
                $"uv {(part.TexCoords.Length > 0 ? $"{part.TexCoords.Min(t => t.Y):F2}..{part.TexCoords.Max(t => t.Y):F2}" : "none")}  " +
                $"{part.Texture}");
        }

        Console.WriteLine();
        Console.WriteLine($"  written  : {outputPath} ({image.Width}x{image.Height}, {png.Length / 1024.0:F0} KB)");
        return 0;
    }

    private static void Print(PdxNode node, int depth, int maxDepth)
    {
        var indent = new string(' ', depth * 2);

        if (depth > 0)
        {
            var properties = node.Properties.Count == 0
                ? string.Empty
                : "  " + string.Join(", ", node.Properties.Select(p => $"{p.Key}={p.Value}"));

            Console.WriteLine($"{indent}{node.Name}{properties}");
        }

        if (depth >= maxDepth)
        {
            if (node.Children.Count > 0)
            {
                Console.WriteLine($"{indent}  ... {node.Children.Count} more");
            }

            return;
        }

        foreach (var child in node.Children)
        {
            Print(child, depth + 1, maxDepth);
        }
    }
}
