using Koru.Cli.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Install;

public class UntendCommand : Command<UntendCommand.UntendSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IStateStore _stateStore;
    private readonly IPathExpander _pathExpander;

    public UntendCommand(IConfigStore configStore, IStateStore stateStore, IPathExpander pathExpander)
    {
        _configStore = configStore;
        _stateStore = stateStore;
        _pathExpander = pathExpander;
    }

    public class UntendSettings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        public string Path { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, UntendSettings settings)
    {
        var config = _configStore.Load();
        var targetPath = Path.GetFullPath(settings.Path);

        var removed = config.Projects.RemoveAll(p =>
            Path.GetFullPath(p).Equals(targetPath, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Project path '{targetPath}' was not in the tended projects list.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Removed '{targetPath}' from tended projects.[/]");
        }

        _configStore.Save(config);

        // Drop any project-local install records pointing inside that path
        var removedRecords = 0;
        var targetNormalized = targetPath.Replace('\\', '/');

        foreach (var registry in config.Registries)
        {
            var records = _stateStore.Load(registry.Name).ToList();
            var toRemove = records.Where(r =>
            {
                var destNormalized = r.DestinationPath.Replace('\\', '/');
                return destNormalized.StartsWith(targetNormalized + "/", StringComparison.OrdinalIgnoreCase)
                    || destNormalized.Equals(targetNormalized, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (toRemove.Count > 0)
            {
                foreach (var rec in toRemove)
                    records.Remove(rec);

                _stateStore.Save(registry.Name, records);
                removedRecords += toRemove.Count;
            }
        }

        if (removedRecords > 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Dropped {removedRecords} install record(s) from state.json. " +
                "Installed files were left in place and will be ignored on future syncs.[/]");
        }

        return 0;
    }
}
