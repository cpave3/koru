using Koru.Cli.Core.Abstractions;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Plugin;

public class PluginAddCommand : Command<PluginAddCommand.PluginAddSettings>
{
    private readonly IPluginHost _pluginHost;

    public PluginAddCommand(IPluginHost pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public class PluginAddSettings : PluginSettings
    {
        [CommandArgument(0, "<nuget-name>")]
        public string NugetName { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, PluginAddSettings settings)
    {
        var feeds = GetConfiguredFeeds();
        var version = _pluginHost.Install(settings.NugetName, feeds);
        Console.WriteLine($"Installed plugin: {settings.NugetName} v{version}");
        return 0;
    }

    private static IEnumerable<string> GetConfiguredFeeds()
    {
        // Return default nuget feed; config worker will enrich this later.
        return new[] { "https://api.nuget.org/v3/index.json" };
    }
}
