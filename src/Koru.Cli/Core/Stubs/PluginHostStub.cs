using Koru.Contracts;
using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class PluginHostStub : IPluginHost
{
    public IReadOnlyList<IPlugin> LoadedPlugins => throw new NotImplementedException("stub");
    public IReadOnlyList<string> InstalledPluginNames => throw new NotImplementedException("stub");

    public string Install(string nugetName, IEnumerable<string> feeds) => throw new NotImplementedException("stub");
    public void LoadAll() => throw new NotImplementedException("stub");
    public void Remove(string name) => throw new NotImplementedException("stub");
}
