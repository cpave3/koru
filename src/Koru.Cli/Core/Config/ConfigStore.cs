using System.Text.Json;
using System.Text.Json.Serialization;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Config;

public class ConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _configPath;

    public ConfigStore() : this(
        ResolveConfigPath())
    { }

    private static string ResolveConfigPath()
    {
        var koruHome = Environment.GetEnvironmentVariable("KORU_HOME");
        if (!string.IsNullOrEmpty(koruHome))
            return Path.Combine(koruHome, "config.json");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "koru",
            "config.json");
    }

    public ConfigStore(string configPath)
    {
        _configPath = configPath;
    }

    public string ConfigPath => _configPath;

    public CliConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new CliConfig();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<CliConfig>(json, Options) ?? new CliConfig();
    }

    public void Save(CliConfig config)
    {
        var parent = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(ConfigPath, json);
    }
}
