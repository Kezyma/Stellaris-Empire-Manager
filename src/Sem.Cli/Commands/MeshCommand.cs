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
