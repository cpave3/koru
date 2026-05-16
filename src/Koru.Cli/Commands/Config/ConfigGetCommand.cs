using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Config;

public class ConfigGetCommand : Command<ConfigGetCommand.ConfigGetSettings>
{
    private readonly IConfigStore _configStore;

    public ConfigGetCommand(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public class ConfigGetSettings : ConfigSettings
    {
        [CommandArgument(0, "<key>")]
        public string Key { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, ConfigGetSettings settings)
    {
        var config = _configStore.Load();

        if (settings.Key.Equals("defaultRegistry", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.WriteLine(config.DefaultRegistry ?? string.Empty);
            return 0;
        }

        if (settings.Key.Equals("registries", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in config.Registries)
            {
                AnsiConsole.WriteLine($"{entry.Name}\t{entry.Remote}\t{entry.Path}");
            }
            return 0;
        }

        if (settings.Key.Equals("projects", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var project in config.Projects)
            {
                AnsiConsole.WriteLine(project);
            }
            return 0;
        }

        if (settings.Key.Equals("nugetFeeds", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var feed in config.NugetFeeds)
            {
                AnsiConsole.WriteLine(feed);
            }
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
                    AnsiConsole.MarkupLine($"[red]Error: Registry '{parts[1]}' not found.[/]");
                    return 1;
                }
                AnsiConsole.WriteLine(entry.Remote);
                return 0;
            }
        }

        AnsiConsole.MarkupLine($"[red]Error: Unknown key '{settings.Key}'.[/]");
        return 1;
    }
}
