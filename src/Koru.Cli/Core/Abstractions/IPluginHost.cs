using Koru.Contracts;

namespace Koru.Cli.Core.Abstractions;

public interface IPluginHost
{
    IReadOnlyList<IPlugin> LoadedPlugins { get; }
    void LoadAll();
    string Install(string nugetName, IEnumerable<string> feeds);
    void Remove(string name);
    IReadOnlyList<string> InstalledPluginNames { get; }
}
