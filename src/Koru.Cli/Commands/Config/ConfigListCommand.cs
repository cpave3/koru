using System.Text.Json;
using System.Text.Json.Serialization;
using Koru.Cli.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Config;

public class ConfigListCommand : Command<ConfigListCommand.ConfigListSettings>
{
    private readonly IConfigStore _configStore;

    public ConfigListCommand(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public class ConfigListSettings : ConfigSettings
    {
    }

    public override int Execute(CommandContext context, ConfigListSettings settings)
    {
        var config = _configStore.Load();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        AnsiConsole.WriteLine(JsonSerializer.Serialize(config, options));
        return 0;
    }
}
