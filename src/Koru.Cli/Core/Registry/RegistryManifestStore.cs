using System.Globalization;
using Koru.Cli.Core.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Koru.Cli.Core.Registry;

public class RegistryManifestStore : IRegistryManifestStore
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public RegistryManifestStore()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public RegistryManifest Load(string registryPath)
    {
        var path = RegistryLayout.ManifestPath(registryPath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Registry manifest not found: {path}");

        var yaml = File.ReadAllText(path);
        return _deserializer.Deserialize<RegistryManifest>(yaml);
    }

    public void Save(string registryPath, RegistryManifest manifest)
    {
        var path = RegistryLayout.ManifestPath(registryPath);
        var yaml = _serializer.Serialize(manifest);
        File.WriteAllText(path, yaml);
    }

    public bool Exists(string registryPath)
    {
        return File.Exists(RegistryLayout.ManifestPath(registryPath));
    }
}
