namespace Sem.Io;

/// <summary>
/// The repository's <c>sandbox/</c> tree: working copies of the player's real files that
/// development and tests are free to modify.
/// </summary>
/// <remarks>
/// The sandbox is populated one way only, real to sandbox, by the CLI's <c>devsync</c> command.
/// Nothing copies back. See <c>docs/file-safety.md</c>.
/// </remarks>
public sealed class SandboxLayout
{
    private const string SolutionName = "StellarisEmpireManager";

    private SandboxLayout(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;
        Root = Path.Combine(repositoryRoot, "sandbox");
        UserData = Path.Combine(Root, "userdata");
        GameFiles = Path.Combine(Root, "gamefiles");
        Output = Path.Combine(Root, "output");
    }

    /// <summary>Repository root, identified by the solution file.</summary>
    public string RepositoryRoot { get; }

    /// <summary>The sandbox root. Gitignored.</summary>
    public string Root { get; }

    /// <summary>Copies of the player's game data folder, including empire designs.</summary>
    public string UserData { get; }

    /// <summary>Copies of game installation files kept for study and fixture extraction.</summary>
    public string GameFiles { get; }

    /// <summary>Scratch space for exports and test artifacts.</summary>
    public string Output { get; }

    /// <summary>Builds the layout for a known repository root.</summary>
    public static SandboxLayout ForRepository(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return new SandboxLayout(PathNormalizer.Normalize(repositoryRoot));
    }

    /// <summary>
    /// Finds the repository by walking up from <paramref name="startingDirectory"/>
    /// (the running assembly's directory by default).
    /// </summary>
    public static SandboxLayout Discover(string? startingDirectory = null)
    {
        var root = FindRepositoryRoot(startingDirectory)
            ?? throw new DirectoryNotFoundException(
                $"Could not locate the repository root (no {SolutionName}.slnx or {SolutionName}.sln found in " +
                $"any parent directory of '{startingDirectory ?? AppContext.BaseDirectory}').");

        return ForRepository(root);
    }

    /// <summary>Walks up looking for the solution file. Returns null when there is none.</summary>
    public static string? FindRepositoryRoot(string? startingDirectory = null)
    {
        var directory = new DirectoryInfo(startingDirectory ?? AppContext.BaseDirectory);

        while (directory is not null)
        {
            // .slnx is the .NET 10 default; .sln is accepted so an older checkout still resolves.
            if (File.Exists(Path.Combine(directory.FullName, $"{SolutionName}.slnx")) ||
                File.Exists(Path.Combine(directory.FullName, $"{SolutionName}.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// The policy development and tests run under: writes are confined to the sandbox, temp and
    /// the local cache, and the real installation and game data folder are explicitly protected.
    /// </summary>
    public WritePolicy CreateDevelopmentPolicy()
    {
        var policy = WritePolicy.ForDevelopment(Root).Named("development (sandbox only)");

        var installRoot = StellarisLocator.FindInstallRoot();
        if (installRoot is not null)
        {
            policy = policy.Forbidding(installRoot);
        }

        var userDataRoot = StellarisLocator.FindUserDataRoot(installRoot);
        if (userDataRoot is not null)
        {
            policy = policy.Forbidding(userDataRoot);
        }

        return policy;
    }
}
