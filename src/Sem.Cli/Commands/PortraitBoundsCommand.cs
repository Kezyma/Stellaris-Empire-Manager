using System.CommandLine;
using Sem.Extraction;
using Sem.Io;

namespace Sem.Cli.Commands;

/// <summary>
/// Reports how much room the portraits actually need, which is what the renderer's frame is set from.
/// </summary>
/// <remarks>
/// <para>
/// The game's own portrait layout is a 380-pixel box at scale 24 with the figure standing 20 pixels
/// up it — but that box is a window the game crops rather than a fit around the figures, so copying
/// it cut the heads off the tall species and left the short ones floating above the bottom edge.
/// The frame has to come from the models instead, and this is how it is measured.
/// </para>
/// <para>
/// Only the portraits a player can actually choose count. A room background and the crisis entities
/// are drawn by the same code and are enormous; letting them set the scale would shrink every
/// species to fit something nobody sees.
/// </para>
/// </remarks>
public static class PortraitBoundsCommand
{
    public static Command Create()
    {
        var installOption = new Option<DirectoryInfo?>("--install", "-i")
        {
            Description = "The Stellaris installation to read. Found automatically when omitted.",
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Measure every portrait, not only the ones the designer offers.",
        };

        var command = new Command("portrait-bounds", "Measure how far portraits reach above and below their origin.")
        {
            installOption,
            allOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(installOption)?.FullName,
            parseResult.GetValue(allOption)));

        return command;
    }

    private static int Run(string? installOverride, bool all)
    {
        var installRoot = installOverride ?? StellarisLocator.FindInstallRoot();

        if (installRoot is null)
        {
            Console.Error.WriteLine("Could not find a Stellaris installation. Pass --install explicitly.");
            return 1;
        }

        var sandbox = SandboxLayout.Discover(Environment.CurrentDirectory);
        var content = LayeredContent.ForInstall(installRoot);
        var extractor = new GameDataExtractor(content);
        var database = extractor.Extract();

        var offered = database.PortraitSets
            .SelectMany(s => s.Portraits.Select(p => p.Key))
            .ToHashSet(StringComparer.Ordinal);

        var keys = database.Portraits
            .Where(p => !p.IsGroup)
            .Select(p => p.Key)
            .Where(k => all || offered.Contains(k))
            .ToList();

        Console.WriteLine($"Install : {installRoot}");
        Console.WriteLine($"Measured: {keys.Count} portrait(s){(all ? string.Empty : " the designer offers")}");
        Console.WriteLine();

        var extents = new PortraitBaker(content, new SafeFile(sandbox.CreateDevelopmentPolicy()))
            .Measure(keys, new Progress<string>(message => Console.WriteLine($"  {message}")))
            .OrderByDescending(e => e.Rise)
            .ToList();

        if (extents.Count == 0)
        {
            Console.Error.WriteLine("Nothing could be measured.");
            return 1;
        }

        var rise = extents.Max(e => e.Rise);

        if (extents.Any(e => e.Clipped))
        {
            Console.WriteLine("Larger than even the measuring frame - these report a floor, not a height:");
            foreach (var extent in extents.Where(e => e.Clipped))
            {
                Console.WriteLine($"  {extent.Key,-28} rise {extent.Rise,7:F2}  drop {extent.Drop,6:F2}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Tallest:");
        foreach (var extent in extents.Take(12))
        {
            Console.WriteLine($"  {extent.Key,-28} rise {extent.Rise,7:F2}  drop {extent.Drop,6:F2}");
        }

        Console.WriteLine();
        Console.WriteLine("Deepest:");
        foreach (var extent in extents.OrderByDescending(e => e.Drop).Take(8))
        {
            Console.WriteLine($"  {extent.Key,-28} rise {extent.Rise,7:F2}  drop {extent.Drop,6:F2}");
        }

        Console.WriteLine();
        Console.WriteLine("rise :" + Percentiles([.. extents.Select(e => e.Rise)]));
        Console.WriteLine("drop :" + Percentiles([.. extents.Select(e => e.Drop)]));
        // Nothing below the origin counts. The game's own designer tile shows the render from its top
        // down to three units above the ground line, so whatever a species keeps beneath its feet is
        // cropped away there too — and a figure standing off the bottom of its frame reads as a
        // figure standing, where one floating above it reads as a mistake.
        Console.WriteLine();
        Console.WriteLine("A frame standing every portrait on its bottom edge:");
        Console.WriteLine($"  VisibleHeight = {rise:F3}");
        Console.WriteLine("  BottomMargin  = 0");
        Console.WriteLine($"  against the game's own 15.833 and -0.05263 - figures draw " +
                          $"{15.833 / rise:P0} of their present size.");

        return 0;
    }

    /// <summary>
    /// The shape of a spread, so a long tail can be told from a wide one.
    /// </summary>
    private static string Percentiles(float[] values)
    {
        Array.Sort(values);

        return string.Concat(
            new[] { 50, 75, 90, 95, 99 }.Select(p =>
                $"  p{p} {values[Math.Min(values.Length - 1, values.Length * p / 100)],6:F2}")) +
            $"  max {values[^1],6:F2}";
    }
}
