using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Registry;

public static class RegistryLayout
{
    public static string ManifestPath(string registryPath) => Path.Combine(registryPath, "registry.yaml");
    public static string CoreSkillsDir(string registryPath) => Path.Combine(registryPath, "core", "skills");
    public static string CoreAgentsDir(string registryPath) => Path.Combine(registryPath, "core", "agents");

    public static void EnsureDefaultLayout(string path)
    {
        Directory.CreateDirectory(CoreSkillsDir(path));
        Directory.CreateDirectory(CoreAgentsDir(path));

        var manifest = new RegistryManifest
        {
            Name = Path.GetFileName(path),
            Plugins = []
        };

        var store = new RegistryManifestStore();
        store.Save(path, manifest);

        File.WriteAllText(Path.Combine(path, ".gitignore"), "state.json\n");
        File.WriteAllText(Path.Combine(CoreSkillsDir(path), ".gitkeep"), string.Empty);
        File.WriteAllText(Path.Combine(CoreAgentsDir(path), ".gitkeep"), string.Empty);
    }
}
