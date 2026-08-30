using System.Text.Json;
using Sem.Designs;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Reads every ruler in the player's own files and checks the name a screen would show.
/// </summary>
/// <remarks>
/// The hand-written cases below this file's own fixtures prove the rules; this proves there is no
/// fourth rule nobody noticed. It is the check that was missing: the design corpus tests already
/// walked every ruler name in these same files, but only to see that the fields round-trip, never to
/// see what the name came out as — so three separate faults sat in files the tests were already
/// opening.
/// </remarks>
public sealed class RulerNameCorpusTests
{
    /// <summary>Anything a name should never contain: a hole, a second form, or a template.</summary>
    private static readonly char[] Machinery = ['$', '|', '%'];

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryRulerInTheCorpusReadsAsAName()
    {
        var localizer = Localisation();
        Skip.If(localizer is null, "Extracted text is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        var files = DesignFiles();
        Skip.If(files.Count == 0, "Sandbox copies are missing. Run: dotnet run --project src/Sem.Cli -- devsync");

        var checked_ = 0;

        foreach (var path in files)
        {
            foreach (var design in EmpireDesignsFile.Load(File.ReadAllBytes(path)).Designs)
            {
                var name = localizer!.RulerName(design.Ruler);
                checked_++;

                Assert.False(
                    string.IsNullOrWhiteSpace(name),
                    $"{Path.GetFileName(path)}: {design.Key} has a ruler with no readable name.");

                Assert.True(
                    name.IndexOfAny(Machinery) < 0,
                    $"{Path.GetFileName(path)}: {design.Key}'s ruler reads \"{name}\".");
            }
        }

        Assert.True(checked_ > 50, $"Only {checked_} rulers were read; the corpus should be far larger.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ARulerWhoseNameIsBuiltFromPartsKeepsBothOfThem()
    {
        var localizer = Localisation();
        Skip.If(localizer is null, "Extracted text is missing.");

        var files = DesignFiles();
        Skip.If(files.Count == 0, "Sandbox copies are missing.");

        // Every %LEADER_1% and %LEADER_2% in the corpus carries a given name and a family name. A
        // name that comes back as one word means one of them was dropped, which is what happened to
        // twelve of these.
        var built = 0;

        foreach (var path in files)
        {
            foreach (var design in EmpireDesignsFile.Load(File.ReadAllBytes(path)).Designs)
            {
                if (design.Ruler.Name.FullNames is not { } full ||
                    !full.Key.StartsWith("%LEADER", StringComparison.Ordinal) ||
                    full.Variables.Count < 2)
                {
                    continue;
                }

                built++;
                var name = localizer!.RulerName(design.Ruler);

                Assert.True(
                    name.Contains(' ', StringComparison.Ordinal),
                    $"{design.Key}'s ruler is built from two parts but reads \"{name}\".");
            }
        }

        Assert.True(built >= 30, $"Only {built} rulers are built from parts; expected far more.");
    }

    /// <summary>The player's own designs, as copied into the sandbox. Never the originals.</summary>
    private static IReadOnlyList<string> DesignFiles() =>
        Repository() is { } root && Directory.Exists(Path.Combine(root, "sandbox", "userdata"))
            ? [.. Directory.EnumerateFiles(
                Path.Combine(root, "sandbox", "userdata"),
                "user_empire_designs_v3.4*.txt")]
            : [];

    /// <summary>The extracted text, which is what a screen would actually read from.</summary>
    private static Localizer? Localisation()
    {
        if (Repository() is not { } root)
        {
            return null;
        }

        var path = Path.Combine(root, "src", "Sem.Web", "wwwroot", "gamedata", "loc", "en.json");

        return File.Exists(path)
            && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(path)) is { } entries
                ? new Localizer(entries)
                : null;
    }

    private static string? Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "sandbox")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
