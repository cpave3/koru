using Koru.Cli.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.List;

public class ListPluginsCommand : Command<ListPluginsCommand.ListPluginsSettings>
{
    private readonly IPluginHost _pluginHost;

    public ListPluginsCommand(IPluginHost pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public class ListPluginsSettings : ListSettings
    {
    }

    public override int Execute(CommandContext context, ListPluginsSettings settings)
    {
        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Path Claims");
        table.AddColumn("Source");

        foreach (var plugin in _pluginHost.LoadedPlugins)
        {
            var source = plugin.Name == "core" ? "built-in" : "nuget";
            var claims = string.Join(", ", plugin.PathClaims);
            table.AddRow(plugin.Name, claims, source);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
