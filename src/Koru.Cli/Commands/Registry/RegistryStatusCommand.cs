using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Registry;

public class RegistryStatusCommand : Command<RegistryStatusCommand.RegistryStatusSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;

    public RegistryStatusCommand(IConfigStore configStore, IGitOps gitOps)
    {
        _configStore = configStore;
        _gitOps = gitOps;
    }

    public class RegistryStatusSettings : RegistrySettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, RegistryStatusSettings settings)
    {
        var config = _configStore.Load();
        var entry = config.Registries.FirstOrDefault(r =>
            r.Name.Equals(settings.Name, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Registry '{settings.Name}' not found.[/]");
            return 1;
        }

        var changedFiles = _gitOps.Status(entry.Path);
        if (changedFiles.Count == 0)
        {
            AnsiConsole.WriteLine("Working tree is clean.");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Path");
        table.AddColumn("Status");

        foreach (var path in changedFiles)
        {
            table.AddRow(path, "Modified");
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
