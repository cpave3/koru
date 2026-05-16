using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.List;

public class ListRegistriesCommand : Command<ListRegistriesCommand.ListRegistriesSettings>
{
    private readonly IConfigStore _configStore;

    public ListRegistriesCommand(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public class ListRegistriesSettings : ListSettings
    {
    }

    public override int Execute(CommandContext context, ListRegistriesSettings settings)
    {
        var config = _configStore.Load();
        if (config.Registries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No registries -- try koru init or koru use[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Remote");
        table.AddColumn("Path");
        table.AddColumn("Default");

        foreach (var entry in config.Registries)
        {
            var isDefault = entry.Name.Equals(config.DefaultRegistry, StringComparison.OrdinalIgnoreCase);
            table.AddRow(
                entry.Name,
                string.IsNullOrEmpty(entry.Remote) ? "(none)" : entry.Remote,
                entry.Path,
                isDefault ? "✓" : string.Empty);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
