using YamlDotNet.Serialization;

namespace Koru.Cli.Core.Models;

public class RegistryManifest
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "plugins")]
    public List<PluginRef> Plugins { get; set; } = [];
}
