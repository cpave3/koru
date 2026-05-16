using System.Text.Json;
using System.Text.Json.Serialization;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.State;

public class StateStore : IStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _registriesRoot;

    public StateStore(IPathExpander pathExpander) => _registriesRoot = pathExpander.RegistriesRoot;
    public StateStore(string registriesRoot) => _registriesRoot = registriesRoot;

    public string StatePathFor(string registryName)
    {
        return Path.Combine(_registriesRoot, registryName, "state.json");
    }

    public IReadOnlyList<InstallRecord> Load(string registryName)
    {
        var path = StatePathFor(registryName);
        if (!File.Exists(path))
            return new List<InstallRecord>();

        var json = File.ReadAllText(path);
        var wrapper = JsonSerializer.Deserialize<StateWrapper>(json, Options);
        return wrapper?.Installs ?? new List<InstallRecord>();
    }

    public void Save(string registryName, IEnumerable<InstallRecord> records)
    {
        var path = StatePathFor(registryName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            Directory.CreateDirectory(parent);

        var wrapper = new StateWrapper { Installs = records.ToList() };
        var json = JsonSerializer.Serialize(wrapper, Options);
        File.WriteAllText(path, json);
    }

    private class StateWrapper
    {
        public List<InstallRecord> Installs { get; set; } = [];
    }
}
