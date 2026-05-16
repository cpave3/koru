using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Registry;

public class LinkCommand : Command<LinkCommand.LinkSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;

    public LinkCommand(IConfigStore configStore, IGitOps gitOps)
    {
        _configStore = configStore;
        _gitOps = gitOps;
    }

    public class LinkSettings : CommandSettings
    {
        [CommandArgument(0, "<remote-url>")]
        public string RemoteUrl { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, LinkSettings settings)
    {
        var cwd = Directory.GetCurrentDirectory();
        var config = _configStore.Load();

        // Resolve registry: CWD must equal a registered path OR contain registry.yaml
        var entry = config.Registries.FirstOrDefault(r =>
            string.Equals(Path.GetFullPath(r.Path), Path.GetFullPath(cwd), StringComparison.OrdinalIgnoreCase));

        if (entry is null && File.Exists(Path.Combine(cwd, "registry.yaml")))
        {
            // Registry exists locally but isn't in config; still allow remote setup
            entry = new RegistryEntry { Name = Path.GetFileName(cwd), Path = cwd, Remote = string.Empty };
        }

        if (entry is null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Current directory is not a registered registry. Run this from inside a registry directory.[/]");
            return 1;
        }

        var registryPath = Path.GetFullPath(entry.Path);
        _gitOps.SetRemote(registryPath, settings.RemoteUrl);
        _gitOps.Push(registryPath);

        // Only update config if the registry is already known
        var configEntry = config.Registries.FirstOrDefault(r =>
            string.Equals(r.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
        if (configEntry is not null)
        {
            configEntry.Remote = settings.RemoteUrl;
            _configStore.Save(config);
        }

        AnsiConsole.MarkupLine($"[green]Linked remote '{settings.RemoteUrl}' to registry '{entry.Name}' and pushed.[/]");
        return 0;
    }
}
