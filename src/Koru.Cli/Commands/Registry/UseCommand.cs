using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Registry;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Registry;

public class UseCommand : Command<UseCommand.UseSettings>
{
    private readonly IPathExpander _pathExpander;
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;
    private readonly IPluginHost _pluginHost;

    public UseCommand(IPathExpander pathExpander, IConfigStore configStore, IGitOps gitOps, IPluginHost pluginHost)
    {
        _pathExpander = pathExpander;
        _configStore = configStore;
        _gitOps = gitOps;
        _pluginHost = pluginHost;
    }

    public class UseSettings : CommandSettings
    {
        [CommandArgument(0, "<remote-url>")]
        public string RemoteUrl { get; set; } = string.Empty;

        [CommandArgument(1, "[name]")]
        public string? Name { get; set; }
    }

    public override int Execute(CommandContext context, UseSettings settings)
    {
        var name = settings.Name ?? DeriveNameFromUrl(settings.RemoteUrl);
        if (string.IsNullOrEmpty(name))
        {
            AnsiConsole.MarkupLine("[red]Error: Could not derive registry name from URL. Provide a name.[/]");
            return 1;
        }

        var targetDir = Path.Combine(_pathExpander.RegistriesRoot, name);
        if (Directory.Exists(targetDir))
        {
            AnsiConsole.MarkupLine($"[red]Error: Registry '{name}' already exists at {targetDir}. Provide a different name if needed.[/]");
            return 1;
        }

        AnsiConsole.Status()
            .Start($"Cloning {settings.RemoteUrl}...", ctx =>
            {
                _gitOps.Clone(settings.RemoteUrl, targetDir);
            });

        var fullPath = Path.GetFullPath(targetDir);
        var manifestStore = new RegistryManifestStore();
        RegistryManifest manifest;
        try
        {
            manifest = manifestStore.Load(fullPath);
        }
        catch (FileNotFoundException)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: No registry.yaml found in cloned repository.[/]");
            manifest = new RegistryManifest { Name = name, Plugins = [] };
        }

        _pluginHost.LoadAll();
        var installed = _pluginHost.InstalledPluginNames;
        var missing = manifest.Plugins.Where(p => !installed.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        foreach (var plugin in missing)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Plugin '{plugin.Name}' is required but not installed.[/]");
            if (!string.IsNullOrEmpty(plugin.Nuget))
            {
                AnsiConsole.MarkupLine($"  [dim]Run: koru plugin add {plugin.Nuget}[/]");
            }
        }

        // Register in config
        var config = _configStore.Load();
        if (config.Registries.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || r.Path == fullPath))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Registry '{name}' is already registered.[/]");
        }
        else
        {
            config.Registries.Add(new RegistryEntry
            {
                Name = name,
                Path = fullPath,
                Remote = settings.RemoteUrl
            });
            _configStore.Save(config);
        }

        if (missing.Count > 0)
            AnsiConsole.MarkupLine("[yellow]Install missing plugins, then run:[/] [green]koru sync[/]");
        else
            AnsiConsole.MarkupLine("[green]Next step: koru sync[/]");

        return 0;
    }

    private static string DeriveNameFromUrl(string url)
    {
        // Extract last path segment and strip .git
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash < 0) lastSlash = url.LastIndexOf('\\');
        if (lastSlash < 0) return string.Empty;

        var segment = url[(lastSlash + 1)..];
        if (segment.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            segment = segment[..^4];
        return segment;
    }
}
