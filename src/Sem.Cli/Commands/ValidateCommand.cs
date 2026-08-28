using System.CommandLine;
using Sem.Designs;
using Sem.Extraction;
using Sem.Io;
using Sem.Rules;

namespace Sem.Cli.Commands;

/// <summary>
/// Runs the rules engine over the game's own built-in empires.
/// </summary>
/// <remarks>
/// Every one of these is an empire the game itself ships and accepts, so any of them the engine
/// rejects is a rule this project has got wrong. It is the closest thing to a free correctness
/// suite the game provides.
/// </remarks>
public static class ValidateCommand
{
    public static Command Create()
    {
        var installOption = new Option<DirectoryInfo?>("--install")
        {
            Description = "Stellaris installation directory. Auto-detected when omitted.",
        };

        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "List every empire, not only the ones that fail.",
        };

        var command = new Command("validate", "Check the game's built-in empires against the rules engine.")
        {
            installOption,
            verboseOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(installOption)?.FullName,
            parseResult.GetValue(verboseOption)));

        return command;
    }

    private static int Run(string? installOverride, bool verbose)
    {
        var installRoot = installOverride ?? StellarisLocator.FindInstallRoot();
        if (installRoot is null)
        {
            Console.Error.WriteLine("Could not find a Stellaris installation. Pass --install explicitly.");
            return 1;
        }

        Console.WriteLine($"Install : {installRoot}");
        Console.WriteLine("Extracting game data...");

        var content = LayeredContent.ForInstall(installRoot);
        var database = new GameDataExtractor(content).Extract();
        var rules = new EmpireRules(database);

        // Judged with every pack available, since these empires are the game's own and several
        // need content this machine may or may not have installed.
        var allDlc = database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        var compiler = new RequirementCompiler();
        compiler.LoadScriptedTriggers(new ScriptLoader(content));
        var evaluator = new RequirementEvaluator();

        var checkedEmpires = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var path in content.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            var file = PrescriptedCountriesFile.Load(content.Read(path));

            foreach (var empire in file.Empires)
            {
                if (empire.IsDefaultTemplate)
                {
                    continue;
                }

                var designs = EmpireDesignsFile.CreateEmpty();
                var design = designs.AddFromPrescripted(empire, empire.Key);
                var context = rules.CreateContext(design, allDlc);

                // Some built-in empires exist only for players who lack a pack: the Iferyx use a
                // civic that the Megacorp expansion replaces. Judging those against a full set of
                // packs would test a combination the game never offers.
                if (!evaluator.IsSatisfied(compiler.CompileTriggerByName(empire.Playable), context))
                {
                    skipped++;
                    if (verbose)
                    {
                        Console.WriteLine($"  skip  {empire.Key}  (not offered: {empire.Playable})");
                    }

                    continue;
                }

                var report = rules.Validate(context, design);

                checkedEmpires++;

                if (report.IsValid)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  ok    {empire.Key}");
                    }

                    continue;
                }

                failed++;
                Console.WriteLine($"  FAIL  {empire.Key}  ({Path.GetFileName(path)})");

                foreach (var problem in report.Problems)
                {
                    Console.WriteLine($"          {problem}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{checkedEmpires - failed} of {checkedEmpires} built-in empires validate cleanly" +
            (skipped > 0 ? $" ({skipped} not offered with every pack owned)." : "."));

        return failed == 0 ? 0 : 1;
    }
}
