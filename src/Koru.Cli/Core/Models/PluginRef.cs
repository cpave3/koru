using YamlDotNet.Serialization;

namespace Koru.Cli.Core.Models;

public class PluginRef
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "nuget")]
    public string Nuget { get; set; } = string.Empty;
}
