using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Stubs;

public class ConfigStoreStub : IConfigStore
{
    public string ConfigPath => throw new NotImplementedException("stub");

    public CliConfig Load() => throw new NotImplementedException("stub");

    public void Save(CliConfig config) => throw new NotImplementedException("stub");
}
