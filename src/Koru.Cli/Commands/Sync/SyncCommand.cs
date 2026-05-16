using Koru.Cli.Core;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Sync;
using Koru.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Sync;

public class SyncCommand : Command<SyncCommand.SyncSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;
    private readonly IPluginHost _pluginHost;
    private readonly InstallPlanBuilder _planBuilder;
    private readonly SyncReconciler _reconciler;
    private readonly IPathExpander _pathExpander;

    public SyncCommand(
        IConfigStore configStore,
        IGitOps gitOps,
        IPluginHost pluginHost,
        InstallPlanBuilder planBuilder,
        SyncReconciler reconciler,
        IPathExpander pathExpander)
    {
        _configStore = configStore;
        _gitOps = gitOps;
        _pluginHost = pluginHost;
        _planBuilder = planBuilder;
        _reconciler = reconciler;
        _pathExpander = pathExpander;
    }

    public class SyncSettings : CommandSettings
    {
        [CommandOption("--global-only")]
        public bool GlobalOnly { get; set; }

        [CommandOption("--project <path>")]
        public string? Project { get; set; }

        [CommandOption("--dry-run")]
        public bool DryRun { get; set; }

        [CommandOption("--yes")]
        public bool Yes { get; set; }
    }

    public override int Execute(CommandContext context, SyncSettings settings)
    {
        var config = _configStore.Load();
        var overallReports = new Dictionary<string, SyncReport>();

        foreach (var registry in config.Registries)
        {
            var registryPath = _pathExpander.Expand(registry.Path);
            AnsiConsole.MarkupLine($"[blue]Syncing registry: {registry.Name}[/]");

            // Git phase
            if (!_gitOps.IsClean(registryPath))
            {
                if (!settings.Yes)
                {
                    var choice = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"Registry '{registry.Name}' has local changes.")
                            .AddChoices("Abort", "Proceed with local changes"));

                    if (choice == "Abort")
                    {
                        AnsiConsole.MarkupLine("[red]Aborted.[/]");
                        return 1;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Proceeding with local changes in '{registry.Name}' (--yes)[/]");
                }
            }
            else
            {
                try
                {
                    _gitOps.Pull(registryPath);
                    AnsiConsole.MarkupLine($"[green]Pulled latest changes for '{registry.Name}'.[/]");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine($"[red]Merge conflict in '{registry.Name}': {ex.Message}[/]");
                    AnsiConsole.MarkupLine("[yellow]Please resolve manually and re-run sync.[/]");
                    return 1;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: could not pull '{registry.Name}': {ex.Message}[/]");
                }
            }

            // Build plan
            var scopeFilter = CreateScopeFilter(settings);
            var plugins = _pluginHost.LoadedPlugins;

            IReadOnlyList<DesiredInstall> plan;
            try
            {
                plan = _planBuilder.Build(registry.Name, registryPath, plugins, scopeFilter, config.Projects);
            }
            catch (SyncConflictException ex)
            {
                AnsiConsole.MarkupLine($"[red]Sync conflict: {ex.Message}[/]");
                return 1;
            }

            // Reconcile
            var report = _reconciler.Reconcile(registry.Name, registryPath, plan, settings.DryRun);
            overallReports[registry.Name] = report;
        }

        // Print summary
        PrintSummary(overallReports);

        var totalDrifted = overallReports.Values.Sum(r => r.Drifted);
        var totalConflict = overallReports.Values.Any(r => r.Conflict);
        return (totalDrifted > 0 || totalConflict) ? 1 : 0;
    }

    private static ScopeFilter CreateScopeFilter(SyncSettings settings)
    {
        if (settings.GlobalOnly)
            return ScopeFilter.GlobalOnly();
        if (!string.IsNullOrEmpty(settings.Project))
            return ScopeFilter.ProjectOnly(settings.Project);
        return ScopeFilter.All();
    }

    private static void PrintSummary(Dictionary<string, SyncReport> reports)
    {
        if (reports.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No registries configured.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Registry");
        table.AddColumn("Created");
        table.AddColumn("Updated");
        table.AddColumn("Removed");
        table.AddColumn("Drifted");

        int totalCreated = 0, totalUpdated = 0, totalRemoved = 0, totalDrifted = 0;

        foreach (var (name, report) in reports)
        {
            table.AddRow(name, report.Created.ToString(), report.Updated.ToString(), report.Removed.ToString(), report.Drifted.ToString());
            totalCreated += report.Created;
            totalUpdated += report.Updated;
            totalRemoved += report.Removed;
            totalDrifted += report.Drifted;
        }

        table.AddRow("---", "---", "---", "---", "---");
        table.AddRow("Total", totalCreated.ToString(), totalUpdated.ToString(), totalRemoved.ToString(), totalDrifted.ToString());

        AnsiConsole.Write(table);
    }
}
