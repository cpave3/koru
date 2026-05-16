using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class PathExpanderStub : IPathExpander
{
    public string KoruRoot => throw new NotImplementedException("stub");
    public string RegistriesRoot => throw new NotImplementedException("stub");
    public string PluginsRoot => throw new NotImplementedException("stub");

    public string Expand(string path) => throw new NotImplementedException("stub");
}
