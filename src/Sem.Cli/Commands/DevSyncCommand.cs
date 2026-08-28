using System.CommandLine;
using Sem.Io;

namespace Sem.Cli.Commands;

/// <summary>
/// Copies the player's real Stellaris files into the repository sandbox so development and tests
/// have something realistic to work against.
/// </summary>
/// <remarks>
/// The copy is one way and always has been by design: real to sandbox, never back. The sandbox is
/// the only place development is allowed to write, and this command is the only thing that fills it.
/// </remarks>
public static class DevSyncCommand
{
    /// <summary>Files copied from the player's game data folder.</summary>
    private static readonly string[] UserDataPatterns =
    [
        "user_empire_designs_v3.4*.txt",
        "dlc_load.json",
        "game_data.json",
        "settings.txt",
    ];

    /// <summary>
    /// Small text files copied from the installation. Bulk game data is read in place instead,
    /// because reading never risks the install and mirroring gigabytes would.
    /// </summary>
    private static readonly string[] InstallFiles =
    [
        "launcher-settings.json",
    ];

    public static Command Create()
    {
        var installOption = new Option<DirectoryInfo?>("--install")
        {
            Description = "Stellaris installation directory. Auto-detected when omitted.",
        };

        var userDataOption = new Option<DirectoryInfo?>("--user-data")
        {
            Description = "Stellaris game data directory (saves and empire designs). Auto-detected when omitted.",
        };

        var command = new Command("devsync", "Copy the real Stellaris files into the repository sandbox (one way).")
        {
            installOption,
            userDataOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(installOption)?.FullName,
            parseResult.GetValue(userDataOption)?.FullName));

        return command;
    }

    private static int Run(string? installOverride, string? userDataOverride)
    {
        var sandbox = SandboxLayout.Discover(Environment.CurrentDirectory);
        var policy = sandbox.CreateDevelopmentPolicy();
        var file = new SafeFile(policy);

        Console.WriteLine($"Repository : {sandbox.RepositoryRoot}");
        Console.WriteLine($"Sandbox    : {sandbox.Root}");
        Console.WriteLine();

        var installRoot = installOverride ?? StellarisLocator.FindInstallRoot();
        var userDataRoot = userDataOverride ?? StellarisLocator.FindUserDataRoot(installRoot);

        if (installRoot is null && userDataRoot is null)
        {
            Console.Error.WriteLine(
                "Could not find a Stellaris installation or game data folder. " +
                "Pass --install and --user-data explicitly.");
            return 1;
        }

        var copied = 0;

        if (userDataRoot is not null)
        {
            Console.WriteLine($"Game data  : {userDataRoot}");
            GuardDirection(userDataRoot, sandbox);
            copied += CopyPatterns(file, userDataRoot, sandbox.UserData, UserDataPatterns);
        }
        else
        {
            Console.WriteLine("Game data  : not found (skipped)");
        }

        if (installRoot is not null)
        {
            Console.WriteLine($"Install    : {installRoot}");
            GuardDirection(installRoot, sandbox);

            foreach (var name in InstallFiles)
            {
                copied += CopyPatterns(file, installRoot, sandbox.GameFiles, [name]);
            }

            // The 53 built-in empires are the validation corpus for the rules engine.
            var prescripted = Path.Combine(installRoot, "prescripted_countries");
            if (Directory.Exists(prescripted))
            {
                copied += CopyPatterns(
                    file,
                    prescripted,
                    Path.Combine(sandbox.GameFiles, "prescripted_countries"),
                    ["*.txt"]);
            }

            // One tiny descriptor per DLC; the `name` field is the exact host_has_dlc string.
            copied += CopyDlcDescriptors(file, installRoot, sandbox.GameFiles);
        }
        else
        {
            Console.WriteLine("Install    : not found (skipped)");
        }

        Console.WriteLine();
        Console.WriteLine($"Copied {copied} file(s) into the sandbox. Originals were not modified.");
        return copied > 0 ? 0 : 1;
    }

    /// <summary>
    /// Refuses to treat the sandbox as a source. The copy only ever runs real to sandbox, and a
    /// reversed invocation would defeat the entire point of having one.
    /// </summary>
    private static void GuardDirection(string source, SandboxLayout sandbox)
    {
        var normalizedSource = PathNormalizer.Normalize(source);
        var normalizedSandbox = PathNormalizer.Normalize(sandbox.Root);

        if (PathNormalizer.IsWithin(normalizedSandbox, normalizedSource))
        {
            throw new InvalidOperationException(
                $"'{source}' is inside the sandbox. devsync only copies real files into the sandbox, " +
                "never the other way around.");
        }
    }

    private static int CopyPatterns(SafeFile file, string sourceDirectory, string destinationDirectory, string[] patterns)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        var copied = 0;

        foreach (var pattern in patterns)
        {
            foreach (var source in Directory.EnumerateFiles(sourceDirectory, pattern, SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
                file.Copy(source, destination, overwrite: true);
                Console.WriteLine($"  + {Path.GetFileName(source)}");
                copied++;
            }
        }

        return copied;
    }

    private static int CopyDlcDescriptors(SafeFile file, string installRoot, string destinationRoot)
    {
        var dlcRoot = Path.Combine(installRoot, "dlc");
        if (!Directory.Exists(dlcRoot))
        {
            return 0;
        }

        var copied = 0;

        foreach (var dlcDirectory in Directory.EnumerateDirectories(dlcRoot))
        {
            var name = Path.GetFileName(dlcDirectory);
            foreach (var descriptor in Directory.EnumerateFiles(dlcDirectory, "*.dlc", SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(destinationRoot, "dlc", name, Path.GetFileName(descriptor));
                file.Copy(descriptor, destination, overwrite: true);
                copied++;
            }
        }

        if (copied > 0)
        {
            Console.WriteLine($"  + {copied} DLC descriptor(s)");
        }

        return copied;
    }
}
