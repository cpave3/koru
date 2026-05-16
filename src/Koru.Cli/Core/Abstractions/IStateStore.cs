using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Abstractions;

public interface IStateStore
{
    IReadOnlyList<InstallRecord> Load(string registryName);
    void Save(string registryName, IEnumerable<InstallRecord> records);
    string StatePathFor(string registryName);
}
