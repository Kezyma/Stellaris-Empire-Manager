using Sem.Designs;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;
using Sem.Rules;

namespace Sem.Extraction.Tests;

/// <summary>
/// Naming an empire the way the game names one.
/// </summary>
/// <remarks>
/// These suggestions used to be ours: a hand-written table of five or six words per authority, which
/// meant a reptilian imperium was offered the Rethellian Empire, Imperium, Hegemony, Autocracy or
/// Dominion and nothing else. The game will happily call the same empire "Empire of Pakshalika" —
/// and did, in the corpus below — so reopening such an empire showed a list its own name was not in.
/// That is the case these tests are about.
/// </remarks>
public sealed class EmpireNameGeneratorTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static readonly Lazy<GameDatabase> Database =
        new(() => GameDataExtractor.ExtractFrom(InstallRoot!));

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheGeneratorIsReadWhole()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");
        var database = Database.Value;

        // The file holds 271 shapes and 193 word lists. Asserted as lower bounds, since a content
        // pack may add more, and as bounds at all because reading none of it would otherwise look
        // exactly like an empire with an unusual government.
        Assert.True(database.EmpireNameFormats.Count >= 271, $"Only {database.EmpireNameFormats.Count} shapes.");
        Assert.True(database.EmpireNameParts.Count >= 193, $"Only {database.EmpireNameParts.Count} word lists.");

        // Every list a shape refers to has to exist, or the shape silently produces nothing.
        var known = database.EmpireNameParts.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

        var referenced = database.EmpireNameFormats
            .SelectMany(f => new[] { f.Format, f.PrefixFormat, f.Noun, f.Adjective })
            .OfType<string>()
            .SelectMany(t => System.Text.RegularExpressions.Regex.Matches(t, @"<(\w+)>")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(referenced.Except(known));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnEmpireTheGameNamedIsOfferedItsOwnName()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var designs = DesignFiles();
        Skip.If(designs.Count == 0, "Sandbox copies are missing. Run: dotnet run --project src/Sem.Cli -- devsync");

        var database = Database.Value;
        var rules = new EmpireRules(database);
        var generator = new NameGenerator(database);

        // Every empire in the corpus whose name the game generated: it is stored as a format and its
        // pieces rather than as words, which is exactly what marks it as the generator's work.
        var checked_ = 0;

        foreach (var path in designs)
        {
            foreach (var design in EmpireDesignsFile.Load(File.ReadAllBytes(path)).Designs)
            {
                // Only the names this generator produces. A name may also be stored as %ADJ% or
                // %ADJECTIVE%, which is the game's separate machinery for building a name out of a
                // species name and is nothing to do with the shapes read here.
                if (design.Name.IsLiteral ||
                    design.Name.Variables.Count == 0 ||
                    design.Name.Key.StartsWith('%'))
                {
                    continue;
                }

                var context = rules.CreateContext(design, AllPacks(database));

                var offered = generator.EmpireNames(context, Sources(design));

                Assert.True(
                    offered.Any(s => string.Equals(s.FormatKey, design.Name.Key, StringComparison.Ordinal)),
                    $"{design.Key} is named with the format '{design.Name.Key}', which the generator " +
                    $"did not offer for it. It offered {offered.Count} names " +
                    $"({string.Join(", ", offered.Take(4).Select(s => s.Text))}).");

                checked_++;
            }
        }

        Assert.True(checked_ > 0, "No empire in the corpus carries a generated name, so nothing was proved.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnImperiumAndADemocracyAreOfferedDifferentNames()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = Database.Value;
        var rules = new EmpireRules(database);
        var generator = new NameGenerator(database);

        var file = EmpireDesignsFile.CreateEmpty();

        var imperium = file.Add("A");
        imperium.Authority = "auth_imperial";
        imperium.SetEthics(["ethic_fanatic_militarist", "ethic_authoritarian"]);

        var democracy = file.Add("B");
        democracy.Authority = "auth_democratic";
        democracy.SetEthics(["ethic_fanatic_egalitarian", "ethic_pacifist"]);

        var sources = new EmpireNameSources
        {
            SpeciesAdjective = "Rethellian",
            PlanetName = "Tendrakkia",
            SystemName = "Rethel",
        };

        var one = generator.EmpireNames(rules.CreateContext(imperium, AllPacks(database)), sources);
        var two = generator.EmpireNames(rules.CreateContext(democracy, AllPacks(database)), sources);

        // The whole point of reading the conditions: a democracy should not be offered the names of
        // an imperium. Both should get something, and the two sets should differ.
        Assert.NotEmpty(one);
        Assert.NotEmpty(two);

        var shared = one.Select(s => s.Text).Intersect(two.Select(s => s.Text), StringComparer.Ordinal).Count();

        Assert.True(
            shared < Math.Min(one.Count, two.Count),
            "An imperium and a democracy were offered the same names, so the conditions are not being read.");

        // And the shapes the old hand-written table could not reach are among them.
        Assert.Contains(one, s => s.FormatKey is { Length: > 0 });
    }

    private static IReadOnlySet<string> AllPacks(GameDatabase database) =>
        database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>The three things the game's templates ask a design for.</summary>
    private static EmpireNameSources Sources(EmpireDesign design) => new()
    {
        SpeciesAdjective = Text(design.Species.Adjective) ?? Text(design.Species.Name),
        PlanetName = Text(design.PlanetName),
        SystemName = Text(design.SystemName),
    };

    /// <summary>
    /// A stored name as the generator would see it.
    /// </summary>
    /// <remarks>
    /// No localiser here, and none needed: the templates only ever ask whether the empire has such a
    /// name, and the corpus stores each of these either as a key or as a nested value. What matters
    /// for these tests is that something is there.
    /// </remarks>
    private static string? Text(LocRef reference) =>
        reference.IsEmpty
            ? null
            : reference.Variables.Count > 0
                ? reference.Variables[0].Value?.Key ?? reference.Key
                : reference.Key;

    /// <summary>The player's own designs, as copied into the sandbox. Never the originals.</summary>
    private static IReadOnlyList<string> DesignFiles() =>
        Repository() is { } root && Directory.Exists(Path.Combine(root, "sandbox", "userdata"))
            ? [.. Directory.EnumerateFiles(
                Path.Combine(root, "sandbox", "userdata"),
                "user_empire_designs_v3.4*.txt")]
            : [];

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
