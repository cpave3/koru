using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Registry;

public interface IRegistryManifestStore
{
    RegistryManifest Load(string registryPath);
    void Save(string registryPath, RegistryManifest manifest);
    bool Exists(string registryPath);
}
