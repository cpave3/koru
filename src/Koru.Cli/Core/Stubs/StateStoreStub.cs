using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Stubs;

public class StateStoreStub : IStateStore
{
    public IReadOnlyList<InstallRecord> Load(string registryName) => throw new NotImplementedException("stub");

    public void Save(string registryName, IEnumerable<InstallRecord> records) => throw new NotImplementedException("stub");

    public string StatePathFor(string registryName) => throw new NotImplementedException("stub");
}
