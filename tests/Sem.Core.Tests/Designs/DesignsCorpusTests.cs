using Sem.Designs;
using Sem.Io;

namespace Sem.Core.Tests.Designs;

/// <summary>
/// Runs the model over the player's actual files and the game's built-in empires. Hand-written
/// samples cannot cover what a 3.x-era file omits or what fifty-odd shipped empires contain.
/// </summary>
public sealed class DesignsCorpusTests
{
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryPlayerDesignsFileRoundTripsThroughTheModel()
    {
        var files = TestPaths.SandboxDesignFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        foreach (var path in files)
        {
            var original = SafeFile.ReadAllBytes(path);
            var file = EmpireDesignsFile.Load(original);

            // Touching every property must not modify anything.
            foreach (var design in file.Designs)
            {
                ReadEverything(design);
            }

            Assert.True(
                original.AsSpan().SequenceEqual(file.Save()),
                $"{Path.GetFileName(path)} changed after being read through the model.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void PlayerDesignsHaveTheFieldsTheGameAlwaysWrites()
    {
        var files = TestPaths.SandboxDesignFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        var totalDesigns = 0;

        foreach (var path in files)
        {
            var file = EmpireDesignsFile.Load(SafeFile.ReadAllBytes(path));
            Assert.NotEmpty(file.Designs);

            foreach (var design in file.Designs)
            {
                totalDesigns++;
                var where = $"{Path.GetFileName(path)}:{design.Key}";

                Assert.False(string.IsNullOrEmpty(design.Key), $"{where} has no key.");
                Assert.False(string.IsNullOrEmpty(design.Species.Class), $"{where} has no species class.");
                Assert.False(string.IsNullOrEmpty(design.Species.Portrait), $"{where} has no portrait.");
                Assert.False(string.IsNullOrEmpty(design.Authority), $"{where} has no authority.");
                Assert.False(string.IsNullOrEmpty(design.Origin), $"{where} has no origin.");
                Assert.NotEmpty(design.Ethics);

                // A gestalt has exactly one ethic; everyone else has one or two entries.
                Assert.InRange(design.Ethics.Count, 1, 3);
            }
        }

        // Sized to the live designs file rather than to the backups it used to sweep in: the point
        // of the count is that the corpus was read at all, not that it is large.
        Assert.True(totalDesigns >= 15, $"Expected the whole designs file, found {totalDesigns} designs.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void OlderFormatFilesLoadDespiteOmittingNewerFields()
    {
        var legacy = TestPaths.SandboxDesignFiles
            .FirstOrDefault(p => Path.GetFileName(p).Contains("_250416", StringComparison.Ordinal));
        Skip.If(legacy is null, "The 3.x-era designs backup is not in the sandbox.");

        var file = EmpireDesignsFile.Load(SafeFile.ReadAllBytes(legacy!));

        // This file predates is_nomadic, so the property must read as absent rather than throw.
        Assert.All(file.Designs, d => Assert.Null(d.IsNomadic));
        Assert.Contains(file.Designs, d => d.SecondarySpecies is not null);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryBuiltInEmpireLoadsIntoTheModel()
    {
        var files = TestPaths.SandboxPrescriptedFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        var empires = new List<PrescriptedEmpire>();

        foreach (var path in files)
        {
            var file = PrescriptedCountriesFile.Load(SafeFile.ReadAllBytes(path));
            empires.AddRange(file.Empires);

            // Reading must not disturb the file either.
            Assert.True(
                SafeFile.ReadAllBytes(path).AsSpan().SequenceEqual(file.Document.ToBytes()),
                $"{Path.GetFileName(path)} changed after being read.");
        }

        Assert.True(empires.Count >= 50, $"Expected the built-in empires, found {empires.Count}.");
        Assert.Contains(empires, e => e.IsDefaultTemplate);

        foreach (var empire in empires.Where(e => !e.IsDefaultTemplate))
        {
            Assert.False(string.IsNullOrEmpty(empire.Key));
            Assert.NotNull(empire.Species);
            Assert.False(string.IsNullOrEmpty(empire.Species!.Class), $"{empire.Key} has no species class.");
            Assert.False(string.IsNullOrEmpty(empire.Authority), $"{empire.Key} has no authority.");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void BuiltInEmpiresConvertIntoEditablePlayerDesigns()
    {
        var files = TestPaths.SandboxPrescriptedFiles;
        Skip.If(files.Count == 0, TestPaths.SandboxMissingMessage);

        var target = EmpireDesignsFile.CreateEmpty();
        var converted = 0;

        foreach (var path in files)
        {
            foreach (var empire in PrescriptedCountriesFile.Load(SafeFile.ReadAllBytes(path)).Empires)
            {
                if (empire.IsDefaultTemplate)
                {
                    continue;
                }

                var design = target.AddFromPrescripted(empire, $"{empire.Key} copy");
                converted++;

                Assert.Equal(empire.Authority, design.Authority);
                Assert.Equal(empire.Origin, design.Origin);
                Assert.Equal(empire.Species!.Class, design.Species.Class);
                Assert.Equal(empire.Species.Traits, design.Species.Traits);
                Assert.Equal(empire.Ethics, design.Ethics);
                Assert.Equal(empire.Civics, design.Civics);

                // Names arrive as localisation keys and must stay non-literal so they still translate.
                Assert.False(design.Name.IsLiteral);
                Assert.Equal(empire.Name, design.Name.Key);

                // A converted preset must never quietly spawn as an AI empire.
                Assert.Equal("no", design.SpawnEnabled);
            }
        }

        Assert.True(converted >= 50, $"Expected to convert the built-in empires, converted {converted}.");

        // The whole set must survive a save and reload as a valid designs file.
        var reloaded = EmpireDesignsFile.Load(target.Save());
        Assert.Equal(converted, reloaded.Designs.Count);
    }

    /// <summary>Touches every modelled property, to prove reading never mutates the tree.</summary>
    private static void ReadEverything(EmpireDesign design)
    {
        _ = design.Key;
        _ = design.ShipPrefix.Key;
        _ = design.Name.IsLiteral;
        _ = design.Adjective.Key;
        _ = design.Authority;
        _ = design.PrescriptedFlag;
        _ = design.Government;
        _ = design.IsNomadic;
        _ = design.AdvisorVoiceType;
        _ = design.PlanetName.Key;
        _ = design.PlanetClass;
        _ = design.ShipSize;
        _ = design.SystemName.Key;
        _ = design.Initializer;
        _ = design.GraphicalCulture;
        _ = design.CityGraphicalCulture;
        _ = design.Room;
        _ = design.SpawnEnabled;
        _ = design.SpawnAsFallen;
        _ = design.IgnorePortraitDuplication;
        _ = design.Origin;
        _ = design.Ethics;
        _ = design.Civics;

        var species = design.Species;
        _ = species.Class;
        _ = species.Portrait;
        _ = species.Name.Key;
        _ = species.Plural.Key;
        _ = species.Adjective.Key;
        _ = species.Biography;
        _ = species.NameList;
        _ = species.Gender;
        _ = species.Traits;

        if (design.SecondarySpecies is { } secondary)
        {
            _ = secondary.Class;
            _ = secondary.Traits;
        }

        var ruler = design.Ruler;
        _ = ruler.Gender;
        _ = ruler.Portrait;
        _ = ruler.Texture;
        _ = ruler.EvolutionMask;
        _ = ruler.Attachment;
        _ = ruler.Clothes;
        _ = ruler.Traits;
        _ = ruler.LeaderClass;
        _ = ruler.Title?.Key;
        _ = ruler.TitleFemale?.Key;
        _ = ruler.Name.UseFullRegnalName;
        ReadNameRecursively(ruler.Name.FullNames);

        var flag = design.Flag;
        _ = flag.Icon.Category;
        _ = flag.Icon.File;
        _ = flag.Background.Category;
        _ = flag.Background.File;
        _ = flag.Colors;
    }

    private static void ReadNameRecursively(LocRef? name)
    {
        if (name is null)
        {
            return;
        }

        _ = name.Key;
        _ = name.IsLiteral;

        foreach (var variable in name.Variables)
        {
            _ = variable.Key;
            ReadNameRecursively(variable.Value);
        }
    }
}
