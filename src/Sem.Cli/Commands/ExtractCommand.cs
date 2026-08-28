using System.CommandLine;
using System.Text.Json;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;

namespace Sem.Cli.Commands;

/// <summary>
/// Reads a Stellaris installation and writes the game database the designer runs on.
/// </summary>
public static class ExtractCommand
{
    /// <summary>
    /// Written indented so a patch's effect on the extracted data shows up as a readable diff
    /// rather than one enormous line.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static Command Create()
    {
        var installOption = new Option<DirectoryInfo?>("--install")
        {
            Description = "Stellaris installation directory. Auto-detected when omitted.",
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Where to write gamedb.json. Defaults to the sandbox output directory.",
        };

        var webOption = new Option<bool>("--web")
        {
            Description = "Write into the web app's wwwroot so a local site build has data to serve.",
        };

        var command = new Command("extract", "Read a Stellaris installation into a game database.")
        {
            installOption,
            outputOption,
            webOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(installOption)?.FullName,
            parseResult.GetValue(outputOption)?.FullName,
            parseResult.GetValue(webOption)));

        return command;
    }

    private static int Run(string? installOverride, string? outputOverride, bool forWeb)
    {
        var sandbox = SandboxLayout.Discover(Environment.CurrentDirectory);
        var file = new SafeFile(sandbox.CreateDevelopmentPolicy());

        var installRoot = installOverride ?? StellarisLocator.FindInstallRoot();
        if (installRoot is null)
        {
            Console.Error.WriteLine("Could not find a Stellaris installation. Pass --install explicitly.");
            return 1;
        }

        var defaultDirectory = forWeb ? sandbox.WebGameData : Path.Combine(sandbox.Output, "gamedata");
        var output = outputOverride ?? Path.Combine(defaultDirectory, "gamedb.json");

        Console.WriteLine($"Install : {installRoot}");
        Console.WriteLine($"Output  : {output}");
        Console.WriteLine();

        var extractor = new GameDataExtractor(LayeredContent.ForInstall(installRoot));
        var database = extractor.Extract(new Progress<string>(message => Console.WriteLine($"  {message}")));

        var json = JsonSerializer.SerializeToUtf8Bytes(database, SerializerOptions);
        file.WriteAllBytes(output, json);

        Console.WriteLine("  Reading localisation");
        var localisation = extractor.ExtractLocalisation(reachableFrom: database);
        var outputDirectory = Path.GetDirectoryName(output)!;
        var localisationJson = JsonSerializer.SerializeToUtf8Bytes(localisation, SerializerOptions);
        file.WriteAllBytes(Path.Combine(outputDirectory, "loc", "en.json"), localisationJson);

        var bake = new AssetBaker(LayeredContent.ForInstall(installRoot), file).Bake(
            extractor.Assets,
            Path.Combine(outputDirectory, "assets"),
            new Progress<string>(message => Console.WriteLine($"  {message}")));

        Console.WriteLine();
        WriteSummary(database, json.Length, localisation.Count, localisationJson.Length);

        Console.WriteLine();
        Console.WriteLine($"Images: {bake.Written:N0} written, {bake.Bytes / 1024.0 / 1024.0:F1} MB");

        foreach (var folder in bake.ByFolder)
        {
            Console.WriteLine($"  {folder.Files,6}  {folder.Bytes / 1024.0 / 1024.0,6:F1} MB  {folder.Folder}");
        }

        if (bake.Failures.Count > 0)
        {
            Console.WriteLine($"{bake.Failures.Count} image(s) could not be converted:");
            foreach (var failure in bake.Failures.Take(10))
            {
                Console.WriteLine($"  {failure}");
            }
        }

        if (extractor.Assets.Missing.Count > 0)
        {
            // Not every entity has artwork; the game leaves some without and shows nothing.
            Console.WriteLine($"{extractor.Assets.Missing.Count} referenced image(s) are not in the installation:");

            foreach (var group in extractor.Assets.Missing
                         .GroupBy(m => Path.GetDirectoryName(m)?.Replace('\\', '/') ?? "")
                         .OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Count(),4}  {group.Key}");
            }
        }

        return 0;
    }

    private static void WriteSummary(
        GameDatabase database,
        int byteCount,
        int localisationEntries,
        int localisationBytes)
    {
        Console.WriteLine($"Game version : {database.GameVersion}");
        Console.WriteLine($"Written      : {byteCount / 1024.0:F0} KB database, " +
                          $"{localisationBytes / 1024.0:F0} KB localisation ({localisationEntries:N0} entries)");
        Console.WriteLine();

        (string Label, int Count)[] counts =
        [
            ("content packs", database.Dlc.Count),
            ("archetypes", database.Archetypes.Count),
            ("species classes", database.SpeciesClasses.Count),
            ("species traits", database.Traits.Count(t => t.Kind == TraitKind.Species)),
            ("ruler traits", database.Traits.Count(t => t.Kind == TraitKind.StartingRuler)),
            ("ethics", database.Ethics.Count),
            ("authorities", database.Authorities.Count),
            ("civics", database.Civics.Count(c => !c.IsOrigin)),
            ("origins", database.Civics.Count(c => c.IsOrigin)),
            ("government types", database.GovernmentTypes.Count),
            ("planet classes", database.PlanetClasses.Count),
            ("starting worlds", database.PlanetClasses.Count(p => p.IsStartingWorld)),
            ("portrait categories", database.PortraitCategories.Count),
            ("portrait sets", database.PortraitSets.Count),
            ("portraits", database.Portraits.Count),
            ("name lists", database.NameLists.Count),
            ("starting systems", database.Initializers.Count),
            ("advisor voices", database.AdvisorVoices.Count),
            ("rooms", database.Rooms.Count),
            ("appearance sets", database.GraphicalCultures.Count),
            ("flag categories", database.FlagCategories.Count),
            ("flag colours", database.FlagColors.Count),
            ("built-in empires", database.PrescriptedEmpires.Count),
        ];

        foreach (var (label, count) in counts)
        {
            Console.WriteLine($"  {count,6}  {label}");
        }

        if (database.UnrecognisedTriggers.Count == 0)
        {
            return;
        }

        // Not an error: unknown conditions default to permitting an option. It is the signal that
        // a game patch has introduced script the extractor does not understand yet.
        var total = database.UnrecognisedTriggers.Values.Sum();
        Console.WriteLine();
        Console.WriteLine(
            $"{database.UnrecognisedTriggers.Count} unrecognised condition(s), {total} occurrence(s). Most common:");

        foreach (var (name, count) in database.UnrecognisedTriggers.Take(15))
        {
            Console.WriteLine($"  {count,6}  {name}");
        }
    }
}
