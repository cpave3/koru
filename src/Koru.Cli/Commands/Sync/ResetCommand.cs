using System.IO.Abstractions;
using Koru.Cli.Core;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Sync;
using Koru.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Sync;

public class ResetCommand : Command<ResetCommand.ResetSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IStateStore _stateStore;
    private readonly IGitOps _gitOps;
    private readonly IArtifactResolver _artifactResolver;
    private readonly IPathExpander _pathExpander;
    private readonly IChecksum _checksum;
    private readonly IFileSystem _fileSystem;

    public ResetCommand(
        IConfigStore configStore,
        IStateStore stateStore,
        IGitOps gitOps,
        IArtifactResolver artifactResolver,
        IPathExpander pathExpander,
        IChecksum checksum,
        IFileSystem fileSystem)
    {
        _configStore = configStore;
        _stateStore = stateStore;
        _gitOps = gitOps;
        _artifactResolver = artifactResolver;
        _pathExpander = pathExpander;
        _checksum = checksum;
        _fileSystem = fileSystem;
    }

    public class ResetSettings : CommandSettings
    {
        [CommandArgument(0, "<artifact>")]
        public string Artifact { get; set; } = string.Empty;

        [CommandOption("--plugin <name>")]
        public string? Plugin { get; set; }
    }

    public override int Execute(CommandContext context, ResetSettings settings)
    {
        var resolved = _artifactResolver.Resolve(settings.Artifact);
        if (resolved.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Artifact '{settings.Artifact}' not found in any registry.[/]");
            return 1;
        }

        // If resolved in multiple registries, present a list
        ResolvedArtifact chosen;
        if (resolved.Count > 1)
        {
            var names = resolved.Select(r => $"{r.RegistryName}: {r.SourcePath}").ToList();
            var chosenName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple matches found. Choose one:")
                    .AddChoices(names));
            chosen = resolved[names.IndexOf(chosenName)];
        }
        else
        {
            chosen = resolved[0];
        }

        // Find registry path
        var config = _configStore.Load();
        var registryEntry = config.Registries.FirstOrDefault(r => r.Name == chosen.RegistryName);
        if (registryEntry is null)
        {
            AnsiConsole.MarkupLine($"[red]Registry '{chosen.RegistryName}' not found in config.[/]");
            return 1;
        }

        var registryPath = _pathExpander.Expand(registryEntry.Path);

        // Require clean registry working tree
        if (!_gitOps.IsClean(registryPath))
        {
            AnsiConsole.MarkupLine($"[red]Reset requires a clean registry working tree. Please commit or revert changes and try again.[/]");
            return 1;
        }

        // Load existing records
        var records = _stateStore.Load(chosen.RegistryName).ToList();
        var candidates = records
            .Where(r => r.SourcePath.Equals(chosen.SourcePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Artifact '{settings.Artifact}' is not installed.[/]");
            return 1;
        }

        // Disambiguate by plugin
        InstallRecord record;
        if (candidates.Count > 1 && string.IsNullOrEmpty(settings.Plugin))
        {
            var names = candidates.Select(c => c.Plugin).ToList();
            var chosenPlugin = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple plugins installed this artifact. Choose one:")
                    .AddChoices(names));
            record = candidates.First(c => c.Plugin == chosenPlugin);
        }
        else if (!string.IsNullOrEmpty(settings.Plugin))
        {
            record = candidates.FirstOrDefault(c => c.Plugin.Equals(settings.Plugin, StringComparison.OrdinalIgnoreCase))
                ?? candidates.First();
        }
        else
        {
            record = candidates.First();
        }

        // Verify copy mode
        if (record.InstallMode == InstallMode.Link)
        {
            AnsiConsole.MarkupLine($"[yellow]Artifact '{settings.Artifact}' was installed with link mode; nothing to reset.[/]");
            return 0;
        }

        // Re-copy
        var sourcePath = Path.Combine(registryPath, record.SourcePath);
        if (!_fileSystem.File.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source file not found: {sourcePath}[/]");
            return 1;
        }

        _fileSystem.File.Copy(sourcePath, record.DestinationPath, overwrite: true);
        var sourceChecksum = _checksum.ComputeSha256(sourcePath);
        var installedChecksum = _checksum.ComputeSha256(record.DestinationPath);

        record.SourceChecksum = sourceChecksum;
        record.InstalledChecksum = installedChecksum;

        _stateStore.Save(chosen.RegistryName, records);

        AnsiConsole.MarkupLine($"[green]Reset {record.DestinationPath} to registry version.[/]");
        return 0;
    }
}
