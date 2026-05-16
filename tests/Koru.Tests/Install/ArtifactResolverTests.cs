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
    public void Resolve_Directory_Prefix_Returns_All_Files_Beneath()
    {
        var reg = CreateRegistry("work", [
            "core/skills/a.md",
            "core/skills/b.md",
            "core/agents/c.md",
        ]);
        var resolver = NewResolver(reg.Entry);

        var matches = resolver.Resolve("core/skills");

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.StartsWith("core/skills/", m.SourcePath));
    }

    [Fact]
    public void Resolve_Top_Level_Prefix_Returns_Everything_Under_Namespace()
    {
        var reg = CreateRegistry("work", [
            "core/skills/a.md",
            "core/agents/c.md",
            "chimera/modes/d.md",
        ]);
        var resolver = NewResolver(reg.Entry, [
            new FakePlugin("core", ["core/**"]),
            new FakePlugin("chimera", ["chimera/**"]),
        ]);

        var matches = resolver.Resolve("core");

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.StartsWith("core/", m.SourcePath));
    }

    [Fact]
    public void Resolve_Glob_Matches_With_Star()
    {
        var reg = CreateRegistry("work", [
            "core/agents/review-correctness.md",
            "core/agents/review-style.md",
            "core/agents/format.md",
        ]);
        var resolver = NewResolver(reg.Entry);

        var matches = resolver.Resolve("core/agents/review-*");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.SourcePath == "core/agents/review-correctness.md");
        Assert.Contains(matches, m => m.SourcePath == "core/agents/review-style.md");
    }

    [Fact]
    public void ResolveAll_Returns_Every_Claimed_Artifact_Across_Registries()
    {
        var reg1 = CreateRegistry("work", ["core/skills/a.md", "core/skills/b.md"]);
        var reg2 = CreateRegistry("personal", ["core/skills/c.md"]);
        var resolver = NewResolverMulti([reg1.Entry, reg2.Entry]);

        var matches = resolver.ResolveAll();

        Assert.Equal(3, matches.Count);
        Assert.Contains(matches, m => m.RegistryName == "work" && m.SourcePath == "core/skills/a.md");
        Assert.Contains(matches, m => m.RegistryName == "personal" && m.SourcePath == "core/skills/c.md");
    }

    [Fact]
    public void Resolve_Finds_Directory_Artifact_By_Name()
    {
        var reg = CreateRegistry("work", [
            "core/skills/grill-me/SKILL.md",
            "core/skills/grill-me/AGENT-BRIEF.md",
        ]);
        var resolver = NewResolver(reg.Entry);

        var matches = resolver.Resolve("grill-me");

        Assert.Single(matches);
        Assert.Equal("core/skills/grill-me", matches[0].SourcePath);
    }

    [Fact]
    public void Resolve_Finds_Both_File_And_Directory_With_Same_Basename()
    {
        var reg = CreateRegistry("work", [
            "core/skills/review.md",
            "core/skills/review/SKILL.md",
        ]);
        var resolver = NewResolver(reg.Entry);

        var matches = resolver.Resolve("review");

        Assert.Equal(2, matches.Count);
        var paths = matches.Select(m => m.SourcePath).ToHashSet();
        Assert.Contains("core/skills/review.md", paths);
        Assert.Contains("core/skills/review", paths);
    }

    [Fact]
    public void ResolveAll_Treats_SKILL_Md_Dir_As_Single_Artifact()
    {
        var reg = CreateRegistry("work", [
            "core/skills/a.md",
            "core/skills/grill-me/SKILL.md",
            "core/skills/grill-me/notes.md",
            "core/skills/grill-me/scripts/run.sh",
        ]);
        var resolver = NewResolver(reg.Entry);

        var all = resolver.ResolveAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, a => a.SourcePath == "core/skills/a.md");
        Assert.Contains(all, a => a.SourcePath == "core/skills/grill-me");
    }

    [Fact]
    public void Resolve_Skips_Non_Markdown_Files()
    {
        var reg = CreateRegistry("work", [
            "core/skills/a.md",
            "core/skills/.gitkeep",
            "core/agents/.gitkeep",
            "core/agents/b.md",
        ]);
        var resolver = NewResolver(reg.Entry);

        var resolved = resolver.ResolveAll();
        var paths = resolved.Select(r => r.SourcePath).ToHashSet();

        Assert.Equal(2, resolved.Count);
        Assert.Contains("core/skills/a.md", paths);
        Assert.Contains("core/agents/b.md", paths);
        Assert.DoesNotContain("core/skills/.gitkeep", paths);
        Assert.DoesNotContain("core/agents/.gitkeep", paths);
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

    private ArtifactResolver NewResolver(RegistryEntry entry, List<IPlugin>? plugins = null)
    {
        plugins ??= [new FakePlugin("core", ["core/**"])];
        return new ArtifactResolver(
            new FakeConfigStore([entry]),
            new GlobMatcher(),
            new FakePluginHost(plugins),
            _gitOps,
            new PathExpander());
    }

    private ArtifactResolver NewResolverMulti(List<RegistryEntry> entries, List<IPlugin>? plugins = null)
    {
        plugins ??= [new FakePlugin("core", ["core/**"])];
        return new ArtifactResolver(
            new FakeConfigStore(entries),
            new GlobMatcher(),
            new FakePluginHost(plugins),
            _gitOps,
            new PathExpander());
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
