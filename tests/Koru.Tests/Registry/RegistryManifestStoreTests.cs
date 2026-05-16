using System.Text.RegularExpressions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Registry;
using Xunit;

namespace Koru.Tests.Registry;

public class RegistryManifestStoreTests
{
    [Fact]
    public void RoundTrip_Yaml_Preserves_Properties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-reg-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var manifest = new RegistryManifest
            {
                Name = "acme-team",
                Plugins =
                [
                    new PluginRef { Name = "chimera", Nuget = "Acme.Cli.Plugin.Chimera" },
                    new PluginRef { Name = "claude", Nuget = "Acme.Cli.Plugin.Claude" }
                ]
            };

            var store = new RegistryManifestStore();
            store.Save(tempDir, manifest);
            var loaded = store.Load(tempDir);

            Assert.Equal("acme-team", loaded.Name);
            Assert.Equal(2, loaded.Plugins.Count);
            Assert.Contains(loaded.Plugins, p => p.Name == "chimera" && p.Nuget == "Acme.Cli.Plugin.Chimera");
            Assert.Contains(loaded.Plugins, p => p.Name == "claude" && p.Nuget == "Acme.Cli.Plugin.Claude");
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    [Fact]
    public void Load_Throws_If_Manifest_Missing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-reg-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var store = new RegistryManifestStore();
            var ex = Assert.Throws<FileNotFoundException>(() => store.Load(tempDir));
            Assert.Contains("Registry manifest not found", ex.Message);
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    [Fact]
    public void EnsureDefaultLayout_Creates_Expected_Structure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-reg-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            RegistryLayout.EnsureDefaultLayout(tempDir);

            Assert.True(Directory.Exists(Path.Combine(tempDir, "core", "skills")));
            Assert.True(Directory.Exists(Path.Combine(tempDir, "core", "agents")));
            Assert.True(File.Exists(Path.Combine(tempDir, "core", "skills", ".gitkeep")));
            Assert.True(File.Exists(Path.Combine(tempDir, "core", "agents", ".gitkeep")));
            Assert.True(File.Exists(Path.Combine(tempDir, "registry.yaml")));
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    private static void DeleteRecursive(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var dir in Directory.GetDirectories(path))
        {
            DeleteRecursive(dir);
        }
        foreach (var file in Directory.GetFiles(path))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(path, recursive: true);
    }
}
