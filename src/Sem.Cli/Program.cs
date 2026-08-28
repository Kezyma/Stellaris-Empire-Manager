using System.CommandLine;
using Sem.Cli.Commands;

var root = new RootCommand("Stellaris Empire Manager development tools.")
{
    DevSyncCommand.Create(),
    ExtractCommand.Create(),
};

return root.Parse(args).Invoke();
