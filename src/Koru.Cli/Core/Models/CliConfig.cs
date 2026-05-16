namespace Koru.Cli.Core.Models;

public class CliConfig
{
    public string? DefaultRegistry { get; set; }
    public List<RegistryEntry> Registries { get; set; } = [];
    public List<string> Projects { get; set; } = [];
    public List<string> NugetFeeds { get; set; } = [];
}
