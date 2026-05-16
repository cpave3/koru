using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Config;

public class ConfigSetCommand : Command<ConfigSetCommand.ConfigSetSettings>
{
    private readonly IConfigStore _configStore;

    public ConfigSetCommand(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public class ConfigSetSettings : ConfigSettings
    {
        [CommandArgument(0, "<key>")]
        public string Key { get; set; } = string.Empty;

        [CommandArgument(1, "<value>")]
        public string Value { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, ConfigSetSettings settings)
    {
        var config = _configStore.Load();

        if (settings.Key.Equals("defaultRegistry", StringComparison.OrdinalIgnoreCase))
        {
            config.DefaultRegistry = settings.Value;
            _configStore.Save(config);
            AnsiConsole.MarkupLine($"[green]defaultRegistry set to '{settings.Value}'[/]");
            return 0;
        }

        if (settings.Key.Equals("projects", StringComparison.OrdinalIgnoreCase))
        {
            config.Projects.Add(settings.Value);
            _configStore.Save(config);
            AnsiConsole.MarkupLine($"[green]Added project '{settings.Value}'[/]");
            return 0;
        }

        if (settings.Key.Equals("nugetFeeds", StringComparison.OrdinalIgnoreCase))
        {
            config.NugetFeeds.Add(settings.Value);
            _configStore.Save(config);
            AnsiConsole.MarkupLine($"[green]Added NuGet feed '{settings.Value}'[/]");
            return 0;
        }

        // registries.<name>.remote
        if (settings.Key.StartsWith("registries.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = settings.Key.Split('.');
            if (parts.Length == 3 && parts[2].Equals("remote", StringComparison.OrdinalIgnoreCase))
            {
                var entry = config.Registries.FirstOrDefault(r =>
                    r.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    entry = new RegistryEntry { Name = parts[1] };
                    config.Registries.Add(entry);
                }
                entry.Remote = settings.Value;
                _configStore.Save(config);
                AnsiConsole.MarkupLine($"[green]Set remote for registry '{parts[1]}' to '{settings.Value}'[/]");
                return 0;
            }
        }

        AnsiConsole.MarkupLine($"[red]Error: Unknown key '{settings.Key}'.[/]");
        return 1;
    }
}
