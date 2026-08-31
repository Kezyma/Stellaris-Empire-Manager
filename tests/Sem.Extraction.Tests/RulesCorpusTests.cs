using Sem.Designs;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;
using Sem.Rules;

namespace Sem.Extraction.Tests;

/// <summary>
/// Runs the rules engine against the game's own empires.
/// </summary>
/// <remarks>
/// Every built-in empire is one the game itself ships and accepts, so any the engine rejects is a
/// rule this project has got wrong. It is the closest thing to a free correctness suite the game
/// provides, and it exercises combinations no hand-written test would think to try.
/// </remarks>
public sealed class RulesCorpusTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static readonly Lazy<LayeredContent> Content =
        new(() => LayeredContent.ForInstall(InstallRoot!));

    private static readonly Lazy<GameDatabase> Database =
        new(() => new GameDataExtractor(Content.Value).Extract());

    private static readonly Lazy<EmpireRules> Rules = new(() => new EmpireRules(Database.Value));

    /// <summary>
    /// Every habitability preference the game defines has to be recognised as one, or the trait
    /// picker offers it as a choice the game does not have. Two name prefixes recognised 36 of the
    /// 48 and missed the whole machine family; asking the trait what it does instead catches all of
    /// them, and keeps catching them when the game adds another.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryHabitabilityPreferenceIsRecognisedAsOne()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var preferences = Database.Value.Traits
            .Where(t => t.Key.EndsWith("_preference", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(48, preferences.Count);

        var unrecognised = preferences
            .Where(t => !Rules.Value.IsHabitabilityPreference(t.Key))
            .Select(t => t.Key)
            .ToList();

        Assert.True(
            unrecognised.Count == 0,
            "Habitability preferences the picker would offer as choices:\r\n" + string.Join("\r\n", unrecognised));
    }

    /// <summary>
    /// A species is only ever suited to its world by a trait its archetype can actually hold. The
    /// world alone decided this, so a machine empire on a desert world was told it had
    /// <c>trait_pc_desert_preference</c>, whose game definition allows
    /// <c>{ BIOLOGICAL PRESAPIENT LITHOID }</c> and nothing else.
    /// </summary>
    [SkippableTheory]
    [Trait("Category", "RealData")]
    [InlineData("pc_desert")]
    [InlineData("pc_arctic")]
    [InlineData("pc_ocean")]
    [InlineData("pc_continental")]
    public void AHabitabilityPreferenceIsOneTheSpeciesArchetypeMayHold(string planetClass)
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var byKey = Database.Value.Traits.ToDictionary(t => t.Key, StringComparer.Ordinal);

        foreach (var archetype in new[] { "BIOLOGICAL", "LITHOID", "MACHINE", "ROBOT" })
        {
            var key = Rules.Value.HabitabilityTraitFor(planetClass, archetype);
            Assert.NotNull(key);

            var trait = byKey[key!];

            Assert.True(
                trait.AllowedArchetypes.Count == 0 || trait.AllowedArchetypes.Contains(archetype),
                $"{planetClass} gave a {archetype} species {key}, which allows " +
                $"[{string.Join(' ', trait.AllowedArchetypes)}].");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryBuiltInEmpireTheGameOffersValidates()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var allPacks = Database.Value.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var (validated, failures) = ValidateCorpus(allPacks);

        Assert.True(validated >= 40, $"Expected the built-in empires, validated {validated}.");
        Assert.True(failures.Count == 0, "Built-in empires the engine rejects:\r\n" + string.Join("\r\n", failures));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryBuiltInEmpireValidatesForAPlayerWhoOwnsNothing()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The other direction: without any packs, a different set of empires is offered, including
        // the ones that exist precisely because a pack is missing.
        var (validated, failures) = ValidateCorpus(new HashSet<string>());

        Assert.True(validated > 0, "No built-in empires were offered without any content packs.");
        Assert.True(failures.Count == 0, "Built-in empires the engine rejects:\r\n" + string.Join("\r\n", failures));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ContentPackGatingChangesWhichEmpiresAreOffered()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var allPacks = Database.Value.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        var withEverything = ValidateCorpus(allPacks).Validated;
        var withNothing = ValidateCorpus(new HashSet<string>()).Validated;

        Assert.True(
            withEverything > withNothing,
            $"Owning every pack should offer more empires, but offered {withEverything} against {withNothing}.");
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void BreakingARealEmpireInOneWayIsDetected()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var rules = Rules.Value;
        var allPacks = Database.Value.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        // The United Nations of Earth: democratic, egalitarian and xenophile, on a continental world.
        var original = LoadPrescripted("humans3") ?? LoadAnyValidEmpire(allPacks);
        Assert.NotNull(original);
        Assert.True(rules.Validate(original!, allPacks).IsValid, "The starting empire should be valid.");

        AssertBreaks("too many ethics", ValidationArea.Ethics, d => d.SetEthics(
            ["ethic_fanatic_militarist", "ethic_fanatic_xenophile"]));

        AssertBreaks("two ethics from one group", ValidationArea.Ethics, d => d.SetEthics(
            ["ethic_militarist", "ethic_pacifist"]));

        AssertBreaks("gestalt alongside another ethic", ValidationArea.Ethics, d => d.SetEthics(
            ["ethic_gestalt_consciousness", "ethic_militarist"]));

        AssertBreaks("no ethics at all", ValidationArea.Ethics, d => d.SetEthics([]));

        AssertBreaks("one civic too few", ValidationArea.Civics, d => d.SetCivics([]));

        AssertBreaks("an ethic that does not exist", ValidationArea.Ethics, d => d.SetEthics(
            ["ethic_not_a_real_ethic"]));

        AssertBreaks("a civic that does not exist", ValidationArea.Civics, d => d.SetCivics(
            ["civic_not_a_real_civic", "civic_beacon_of_liberty"]));

        AssertBreaks("an origin that does not exist", ValidationArea.Origin, d => d.Origin = "origin_nonsense");

        AssertBreaks("no authority", ValidationArea.Authority, d => d.Authority = null);

        AssertBreaks("no origin", ValidationArea.Origin, d => d.Origin = null);

        AssertBreaks("a species class that does not exist", ValidationArea.Species, d => d.Species.Class = "NOPE");

        AssertBreaks("a hive mind authority without gestalt consciousness", ValidationArea.Authority, d =>
            d.Authority = "auth_hive_mind");

        AssertBreaks("a machine species under a hive mind", ValidationArea.Species, d =>
        {
            // The Machine Age lets machines be individuals, so an ordinary machine empire is fine.
            // A hive mind is the combination the species class actually forbids.
            d.Species.Class = "MACHINE";
            d.Species.SetTraits(["trait_machine_unit"]);
            d.SetEthics(["ethic_gestalt_consciousness"]);
            d.Authority = "auth_hive_mind";
        });

        AssertBreaks("far too many traits", ValidationArea.Traits, d => d.Species.SetTraits([
            "trait_organic",
            "trait_intelligent",
            "trait_rapid_breeders",
            "trait_thrifty",
            "trait_natural_engineers",
            "trait_natural_physicists",
            "trait_natural_sociologists",
        ]));

        AssertBreaks("an origin needing a second species that is missing", ValidationArea.SecondarySpecies, d =>
        {
            d.Origin = "origin_syncretic_evolution";
            d.RemoveSecondarySpecies();
        });

        void AssertBreaks(string what, ValidationArea area, Action<EmpireDesign> mutate)
        {
            // Each mutation starts from a fresh copy, so one break cannot mask another.
            var file = EmpireDesignsFile.CreateEmpty();
            var design = file.AddCopy(original!, "Mutated");
            mutate(design);

            var report = rules.Validate(design, allPacks);

            Assert.True(
                report.Problems.Any(p => p.Area == area),
                $"Expected a {area} problem after applying '{what}', but got: {report}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryDesignThePlayerHasSavedValidates()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var sandbox = SandboxLayout.FindRepositoryRoot(AppContext.BaseDirectory);
        Skip.If(sandbox is null, "Not running inside the repository.");

        var designsPath = Path.Combine(sandbox!, "sandbox", "userdata", EmpireDesignsFile.FileName);
        Skip.If(!File.Exists(designsPath), "No copy of the player's designs. Run the devsync command.");

        var installed = Database.Value.Dlc.Where(d => d.Installed).Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var file = EmpireDesignsFile.Load(SafeFile.ReadAllBytes(designsPath));

        var failures = file.Designs
            .Select(d => (Design: d, Report: Rules.Value.Validate(d, installed)))
            .Where(x => !x.Report.IsValid)
            .Select(x => $"  {x.Design.Key}: {x.Report}")
            .ToList();

        Assert.True(
            failures.Count == 0,
            "The engine rejects designs the game accepted:\r\n" + string.Join("\r\n", failures));
    }

    /// <summary>
    /// Validates every built-in empire the game would offer to a player owning the given packs.
    /// Empires the game itself would not offer are skipped, since judging those tests a
    /// combination that never reaches a player.
    /// </summary>
    private static (int Validated, List<string> Failures) ValidateCorpus(IReadOnlySet<string> ownedPacks)
    {
        var rules = Rules.Value;
        var compiler = new RequirementCompiler();
        compiler.LoadScriptedTriggers(new ScriptLoader(Content.Value));
        var evaluator = new RequirementEvaluator();

        var validated = 0;
        var failures = new List<string>();

        foreach (var path in Content.Value.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            foreach (var empire in PrescriptedCountriesFile.Load(Content.Value.Read(path)).Empires)
            {
                if (empire.IsDefaultTemplate)
                {
                    continue;
                }

                var file = EmpireDesignsFile.CreateEmpty();
                var design = file.AddFromPrescripted(empire, empire.Key);
                var context = rules.CreateContext(design, ownedPacks);

                if (!evaluator.IsSatisfied(compiler.CompileTriggerByName(empire.Playable), context))
                {
                    continue;
                }

                validated++;
                var report = rules.Validate(context, design);

                if (!report.IsValid)
                {
                    failures.Add($"  {empire.Key} ({Path.GetFileName(path)}): {report}");
                }
            }
        }

        return (validated, failures);
    }

    private static EmpireDesign? LoadPrescripted(string key)
    {
        foreach (var path in Content.Value.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            foreach (var empire in PrescriptedCountriesFile.Load(Content.Value.Read(path)).Empires)
            {
                if (string.Equals(empire.Key, key, StringComparison.Ordinal))
                {
                    return EmpireDesignsFile.CreateEmpty().AddFromPrescripted(empire, key);
                }
            }
        }

        return null;
    }

    /// <summary>A fallback so the mutation suite still runs if a named empire is renamed by a patch.</summary>
    private static EmpireDesign? LoadAnyValidEmpire(IReadOnlySet<string> ownedPacks)
    {
        foreach (var path in Content.Value.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            foreach (var empire in PrescriptedCountriesFile.Load(Content.Value.Read(path)).Empires)
            {
                if (empire.IsDefaultTemplate)
                {
                    continue;
                }

                var design = EmpireDesignsFile.CreateEmpty().AddFromPrescripted(empire, empire.Key);
                if (Rules.Value.Validate(design, ownedPacks).IsValid)
                {
                    return design;
                }
            }
        }

        return null;
    }
}
