using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Install;

public class RemoveCommand : Command<RemoveCommand.RemoveSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IStateStore _stateStore;

    public RemoveCommand(IConfigStore configStore, IStateStore stateStore)
    {
        _configStore = configStore;
        _stateStore = stateStore;
    }

    public class RemoveSettings : CommandSettings
    {
        [CommandArgument(0, "<artifact>")]
        public string Artifact { get; set; } = string.Empty;

        [CommandOption("--plugin <name>")]
        public string? Plugin { get; set; }
    }

    public override int Execute(CommandContext context, RemoveSettings settings)
    {
        var config = _configStore.Load();
        var cwd = Directory.GetCurrentDirectory();
        var normalizedQuery = settings.Artifact.Replace('\\', '/');

        var matches = new List<(string RegistryName, InstallRecord Record)>();

        foreach (var registry in config.Registries)
        {
            var records = _stateStore.Load(registry.Name);
            foreach (var record in records)
            {
                if (!IsSourcePathMatch(normalizedQuery, record.SourcePath))
                    continue;

                var destNormalized = record.DestinationPath.Replace('\\', '/');
                var cwdNormalized = cwd.Replace('\\', '/');
                if (!destNormalized.StartsWith(cwdNormalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                matches.Add((registry.Name, record));
            }
        }

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No matching installed artifact found in current project context.[/]");
            return 1;
        }

        var pluginGroups = matches.GroupBy(m => m.Record.Plugin).ToList();
        string selectedPlugin;

        if (pluginGroups.Count == 1)
        {
            selectedPlugin = pluginGroups[0].Key;
        }
        else if (!string.IsNullOrEmpty(settings.Plugin))
        {
            selectedPlugin = settings.Plugin;
            if (!pluginGroups.Any(g => g.Key.Equals(selectedPlugin, StringComparison.OrdinalIgnoreCase)))
            {
                AnsiConsole.MarkupLine($"[red]Plugin '{selectedPlugin}' not found among installed records.[/]");
                return 1;
            }
        }
        else
        {
            var prompt = new SelectionPrompt<string>()
                .Title("Multiple plugins installed this artifact. Select a plugin:")
                .AddChoices(pluginGroups.Select(g => g.Key));

            var result = AnsiConsole.Prompt(prompt);
            if (string.IsNullOrEmpty(result))
                return 1;
            selectedPlugin = result;
        }

        var target = matches.FirstOrDefault(m => m.Record.Plugin.Equals(selectedPlugin, StringComparison.OrdinalIgnoreCase));
        if (target == default)
        {
            AnsiConsole.MarkupLine("[red]No record found for the selected plugin.[/]");
            return 1;
        }

        // Delete destination (warn if missing, don't fail)
        var destPath = target.Record.DestinationPath;
        bool deleted = false;
        if (File.Exists(destPath))
        {
            File.Delete(destPath);
            deleted = true;
        }
        else
        {
            try
            {
                var attr = File.GetAttributes(destPath);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    File.Delete(destPath);
                    deleted = true;
                }
            }
            catch { }
        }

        if (!deleted)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: destination file not found: {destPath}[/]");
        }

        // Remove record from state
        var allRecords = _stateStore.Load(target.RegistryName).ToList();
        allRecords.RemoveAll(r =>
            r.SourcePath.Equals(target.Record.SourcePath, StringComparison.OrdinalIgnoreCase) &&
            r.Plugin.Equals(selectedPlugin, StringComparison.OrdinalIgnoreCase));

        _stateStore.Save(target.RegistryName, allRecords);

        AnsiConsole.MarkupLine($"[green]Removed '{target.Record.SourcePath}' (plugin: {selectedPlugin})[/]");
        return 0;
    }

    private static bool IsSourcePathMatch(string query, string sourcePath)
    {
        var normalizedSource = sourcePath.Replace('\\', '/');

        if (query.Contains('/'))
        {
            if (normalizedSource.Equals(query, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!query.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var withMd = query + ".md";
                if (normalizedSource.Equals(withMd, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(normalizedSource);
        return fileName.Equals(query, StringComparison.OrdinalIgnoreCase);
    }
}
