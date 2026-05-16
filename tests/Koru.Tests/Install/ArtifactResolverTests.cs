using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Install;
using Koru.Contracts;
using Koru.Cli.Core.Git;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Util;

namespace Koru.Tests.Install;

public class ArtifactResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GitOperations _gitOps = new();

    public ArtifactResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"koru-resolver-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        DeleteRecursive(_tempDir);
    }

    [Fact]
    public void Resolve_Bare_Name_Returns_Matches_Across_Registries()
    {
        // Arrange: two registries with real git repos
        var reg1 = CreateRegistry("work", ["core/skills/review.md", "core/skills/api.md"]);
        var reg2 = CreateRegistry("personal", ["core/skills/review.md", "chimera/modes/review.md"]);

        var configStore = new FakeConfigStore([reg1.Entry, reg2.Entry]);
        var plugins = new List<IPlugin>
        {
            new FakePlugin("core", ["core/**"]),
            new FakePlugin("chimera", ["chimera/**"])
        };
        var pluginHost = new FakePluginHost(plugins);

        var resolver = new ArtifactResolver(
            configStore,
            new GlobMatcher(),
            pluginHost,
            _gitOps,
            new PathExpander());

        // Act
        var matches = resolver.Resolve("review");

        // Assert: should find 3 matches (work/review.md, personal/review.md, personal/chimera/review.md)
        // Filtered to those with claiming plugins, so 3 matches
        Assert.Equal(3, matches.Count);

        // Unique registry+path pairs
        var keys = matches.Select(m => $"{m.RegistryName}/{m.SourcePath}").ToList();
        Assert.Contains("work/core/skills/review.md", keys);
        Assert.Contains("personal/core/skills/review.md", keys);
        Assert.Contains("personal/chimera/modes/review.md", keys);

        // Each should have at least one claiming plugin
        var workMatch = matches.First(m => m.RegistryName == "work" && m.SourcePath == "core/skills/review.md");
        Assert.Single(workMatch.ClaimingPlugins);
        Assert.Equal("core", workMatch.ClaimingPlugins[0].Name);
    }

    [Fact]
    public void Resolve_Full_Path_Matches_Exactly()
    {
        // Arrange
        var reg1 = CreateRegistry("work", ["core/skills/database-review.md"]);
        var configStore = new FakeConfigStore([reg1.Entry]);
        var plugins = new List<IPlugin> { new FakePlugin("core", ["core/**"]) };
        var resolver = new ArtifactResolver(
            configStore,
            new GlobMatcher(),
            new FakePluginHost(plugins),
            _gitOps,
            new PathExpander());

        // Act
        var matches = resolver.Resolve("core/skills/database-review.md");

        // Assert
        Assert.Single(matches);
        Assert.Equal("work", matches[0].RegistryName);
        Assert.Equal("core/skills/database-review.md", matches[0].SourcePath);
    }

    [Fact]
    public void Resolve_Path_Without_Extension_Falls_Through_To_Md()
    {
        // Arrange
        var reg1 = CreateRegistry("work", ["core/skills/database-review.md"]);
        var configStore = new FakeConfigStore([reg1.Entry]);
        var plugins = new List<IPlugin> { new FakePlugin("core", ["core/**"]) };
        var resolver = new ArtifactResolver(
            configStore,
            new GlobMatcher(),
            new FakePluginHost(plugins),
            _gitOps,
            new PathExpander());

        // Act: query without .md
        var matches = resolver.Resolve("core/skills/database-review");

        // Assert: should match the .md file
        Assert.Single(matches);
        Assert.Equal("work", matches[0].RegistryName);
        Assert.Equal("core/skills/database-review.md", matches[0].SourcePath);
    }

    [Fact]
    public void Resolve_No_Match_Returns_Empty()
    {
        // Arrange
        var reg1 = CreateRegistry("work", ["core/skills/test.md"]);
        var configStore = new FakeConfigStore([reg1.Entry]);
        var resolver = new ArtifactResolver(
            configStore,
            new GlobMatcher(),
            new FakePluginHost([new FakePlugin("core", ["core/**"])]),
            _gitOps,
            new PathExpander());

        // Act
        var matches = resolver.Resolve("nonexistent");

        // Assert
        Assert.Empty(matches);
    }

    private (string Path, RegistryEntry Entry) CreateRegistry(string name, string[] files)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        _gitOps.Init(path);

        foreach (var f in files)
        {
            var full = Path.Combine(path, f);
            var dir = Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(full, "# test");
        }

        _gitOps.CommitAll(path, "initial");

        var entry = new RegistryEntry
        {
            Name = name,
            Path = path,
            Remote = string.Empty
        };

        return (path, entry);
    }

    private static void DeleteRecursive(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var d in Directory.GetDirectories(path))
            DeleteRecursive(d);
        foreach (var f in Directory.GetFiles(path))
        {
            File.SetAttributes(f, FileAttributes.Normal);
            File.Delete(f);
        }
        Directory.Delete(path, recursive: true);
    }

    private class FakeConfigStore : IConfigStore
    {
        private readonly CliConfig _config;
        public FakeConfigStore(List<RegistryEntry> registries)
        {
            _config = new CliConfig { Registries = registries };
        }
        public string ConfigPath => string.Empty;
        public CliConfig Load() => _config;
        public void Save(CliConfig config) { }
    }

    private class FakePluginHost : IPluginHost
    {
        public IReadOnlyList<IPlugin> LoadedPlugins { get; }
        public FakePluginHost(List<IPlugin> plugins) => LoadedPlugins = plugins;
        public void LoadAll() { }
        public string Install(string nugetName, IEnumerable<string> feeds) => string.Empty;
        public void Remove(string name) { }
        public IReadOnlyList<string> InstalledPluginNames => LoadedPlugins.Select(p => p.Name).ToList();
    }

    private class FakePlugin : IPlugin
    {
        public string Name { get; }
        public IEnumerable<string> PathClaims { get; }
        public FakePlugin(string name, IEnumerable<string> claims)
        {
            Name = name;
            PathClaims = claims;
        }
        public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode) => null;
    }
}
