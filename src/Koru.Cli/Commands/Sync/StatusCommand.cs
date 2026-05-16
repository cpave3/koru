using Koru.Cli.Core;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Sync;
using Koru.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Sync;

public class StatusCommand : Command<StatusCommand.StatusSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IStateStore _stateStore;
    private readonly IGitOps _gitOps;
    private readonly IPluginHost _pluginHost;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly IPathExpander _pathExpander;
    private readonly DriftDetector _driftDetector;

    public StatusCommand(
        IConfigStore configStore,
        IStateStore stateStore,
        IGitOps gitOps,
        IPluginHost pluginHost,
        InstallPlanBuilder planBuilder,
        IPathExpander pathExpander,
        DriftDetector driftDetector)
    {
        _configStore = configStore;
        _stateStore = stateStore;
        _gitOps = gitOps;
        _pluginHost = pluginHost;
        _planBuilder = planBuilder;
        _pathExpander = pathExpander;
        _driftDetector = driftDetector;
    }

    public class StatusSettings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, StatusSettings settings)
    {
        var config = _configStore.Load();

        foreach (var registry in config.Registries)
        {
            AnsiConsole.MarkupLine($"[blue]Registry: {registry.Name}[/]");
            var registryPath = _pathExpander.Expand(registry.Path);

            var plan = _planBuilder.Build(registry.Name, registryPath, _pluginHost.LoadedPlugins, ScopeFilter.All(), config.Projects);
            var existingRecords = _stateStore.Load(registry.Name);

            var wouldCreate = new List<DesiredInstall>();
            var wouldUpdate = new List<DesiredInstall>();
            var wouldRemove = new List<InstallRecord>();
            var drifted = new List<InstallRecord>();

            foreach (var record in existingRecords)
            {
                var match = plan.FirstOrDefault(d =>
                    d.SourcePath.Equals(record.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                    d.Plugin.Equals(record.Plugin, StringComparison.OrdinalIgnoreCase) &&
                    d.DestinationPath.Equals(record.DestinationPath, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    wouldRemove.Add(record);
                }
                else if (record.InstallMode == InstallMode.Copy)
                {
                    var status = _driftDetector.Check(record, registryPath);
                    if (status == DriftStatus.Drifted)
                        drifted.Add(record);
                    else if (status == DriftStatus.SourceChanged)
                        wouldUpdate.Add(match);
                }
            }

            foreach (var d in plan)
            {
                var existingRecord = existingRecords.FirstOrDefault(r =>
                    r.SourcePath.Equals(d.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                    r.Plugin.Equals(d.Plugin, StringComparison.OrdinalIgnoreCase) &&
                    r.DestinationPath.Equals(d.DestinationPath, StringComparison.OrdinalIgnoreCase));
                if (existingRecord is null)
                    wouldCreate.Add(d);
            }

            // Render
            if (wouldCreate.Count == 0 && wouldUpdate.Count == 0 && wouldRemove.Count == 0 && drifted.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]Up to date.[/]");
            }
            else
            {
                var table = new Table();
                table.AddColumn("Action");
                table.AddColumn("Artifact");
                table.AddColumn("Plugin");
                table.AddColumn("Destination");

                foreach (var d in wouldCreate)
                    table.AddRow("[green]create[/]", d.SourcePath, d.Plugin, d.DestinationPath);
                foreach (var d in wouldUpdate)
                    table.AddRow("[yellow]update[/]", d.SourcePath, d.Plugin, d.DestinationPath);
                foreach (var r in wouldRemove)
                    table.AddRow("[red]remove[/]", r.SourcePath, r.Plugin, r.DestinationPath);
                foreach (var r in drifted)
                    table.AddRow("[red]drifted[/]", r.SourcePath, r.Plugin, r.DestinationPath);

                AnsiConsole.Write(table);
            }

            AnsiConsole.WriteLine();
        }

        return 0;
    }
}
