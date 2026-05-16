using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.List;

public class ListArtifactsCommand : Command<ListArtifactsCommand.ListArtifactsSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;
    private readonly IGlobMatcher _globMatcher;
    private readonly IPluginHost _pluginHost;
    private readonly IPathExpander _pathExpander;

    public ListArtifactsCommand(
        IConfigStore configStore,
        IGitOps gitOps,
        IGlobMatcher globMatcher,
        IPluginHost pluginHost,
        IPathExpander pathExpander)
    {
        _configStore = configStore;
        _gitOps = gitOps;
        _globMatcher = globMatcher;
        _pluginHost = pluginHost;
        _pathExpander = pathExpander;
    }

    public class ListArtifactsSettings : ListSettings
    {
    }

    public override int Execute(CommandContext context, ListArtifactsSettings settings)
    {
        var config = _configStore.Load();
        var plugins = _pluginHost.LoadedPlugins;
        var table = new Table();
        table.AddColumn("Registry");
        table.AddColumn("Path");
        table.AddColumn("Claiming Plugins");

        int rowCount = 0;

        foreach (var registry in config.Registries)
        {
            var registryPath = registry.Path;
            if (registryPath.Contains('~'))
                registryPath = _pathExpander.Expand(registryPath);

            var trackedFiles = _gitOps.ListTrackedFiles(registryPath);

            foreach (var trackedPath in trackedFiles)
            {
                var normalized = trackedPath.Replace('\\', '/');
                var claiming = new List<string>();

                foreach (var plugin in plugins)
                {
                    foreach (var claim in plugin.PathClaims)
                    {
                        if (_globMatcher.Matches(claim, normalized))
                        {
                            claiming.Add(plugin.Name);
                            break;
                        }
                    }
                }

                if (claiming.Count > 0)
                {
                    table.AddRow(
                        registry.Name,
                        normalized,
                        string.Join(", ", claiming));
                    rowCount++;
                }
            }
        }

        if (rowCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No artifacts found.[/]");
            return 0;
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
