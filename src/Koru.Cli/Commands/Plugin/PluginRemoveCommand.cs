using Koru.Cli.Core.Abstractions;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Plugin;

public class PluginRemoveCommand : Command<PluginRemoveCommand.PluginRemoveSettings>
{
    private readonly IPluginHost _pluginHost;

    public PluginRemoveCommand(IPluginHost pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public class PluginRemoveSettings : PluginSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, PluginRemoveSettings settings)
    {
        if (settings.Name == "core")
        {
            Console.WriteLine("Error: The 'core' plugin cannot be removed.");
            return 1;
        }

        _pluginHost.Remove(settings.Name);
        Console.WriteLine($"Removed plugin: {settings.Name}");
        return 0;
    }
}
