using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Registry;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Registry;

public class InitCommand : Command<InitCommand.InitSettings>
{
    private readonly IPathExpander _pathExpander;
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;

    public InitCommand(IPathExpander pathExpander, IConfigStore configStore, IGitOps gitOps)
    {
        _pathExpander = pathExpander;
        _configStore = configStore;
        _gitOps = gitOps;
    }

    public class InitSettings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, InitSettings settings)
    {
        var targetDir = Path.Combine(_pathExpander.RegistriesRoot, settings.Name);

        if (Directory.Exists(targetDir))
        {
            AnsiConsole.MarkupLine($"[red]Error: Registry directory '{targetDir}' already exists.[/]");
            return 1;
        }

        Directory.CreateDirectory(targetDir);
        RegistryLayout.EnsureDefaultLayout(targetDir);

        _gitOps.Init(targetDir);
        _gitOps.CommitAll(targetDir, "Initial registry layout");

        var config = _configStore.Load();
        var fullPath = Path.GetFullPath(targetDir);

        if (config.Registries.Any(r => r.Name.Equals(settings.Name, StringComparison.OrdinalIgnoreCase) || r.Path == fullPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Registry '{settings.Name}' is already registered.[/]");
            return 1;
        }

        config.Registries.Add(new RegistryEntry
        {
            Name = settings.Name,
            Path = fullPath,
            Remote = string.Empty
        });

        if (string.IsNullOrEmpty(config.DefaultRegistry))
            config.DefaultRegistry = settings.Name;

        _configStore.Save(config);

        AnsiConsole.MarkupLine($"[green]Registry '{settings.Name}' initialized at {fullPath}[/]");
        return 0;
    }
}
