using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Config;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Plugins;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Sync;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests.Sync;

public class InstallPlanBuilderTests
{
    private readonly GlobMatcher _globMatcher = new();
    private readonly PathExpander _pathExpander = new();
    private readonly Checksum _checksum = new();
    private readonly FakeGitOps _gitOps = new();
    private StateStore _stateStore = null!;

    public InstallPlanBuilderTests()
    {
    }

    [Fact]
    public void Build_GlobalScope_Emits_Core_Plugin_DesiredInstalls()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;

            // Set up tracked files
            _gitOps.Files[registryPath] = new[] { "core/skills/review.md", "core/agents/agent1.md" };

            var plugins = new List<IPlugin> { new CorePlugin() };
            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);

            var plan = builder.Build("test-registry", registryPath, plugins, ScopeFilter.GlobalOnly(), new List<string>());

            Assert.Equal(2, plan.Count);
            Assert.Contains(plan, d => d.SourcePath == "core/skills/review.md" && d.Scope == Scope.Global && d.Plugin == "core");
            Assert.Contains(plan, d => d.SourcePath == "core/agents/agent1.md" && d.Scope == Scope.Global && d.Plugin == "core");
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Build_Excludes_RegistryYaml_StateJson_And_GitPaths()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;
            _gitOps.Files[registryPath] = new[] { "registry.yaml", "state.json", ".git/config", "core/skills/x.md" };

            var plugins = new List<IPlugin> { new CorePlugin() };
            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);

            var plan = builder.Build("test", registryPath, plugins, ScopeFilter.GlobalOnly(), new List<string>());

            Assert.Single(plan);
            Assert.Equal("core/skills/x.md", plan[0].SourcePath);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Build_Uses_Existing_Mode_From_State()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;
            _gitOps.Files[registryPath] = new[] { "core/skills/review.md" };

            _stateStore.Save("test", new List<InstallRecord>
            {
                new() { SourcePath = "core/skills/review.md", Plugin = "core", DestinationPath = "/tmp/x", InstallMode = InstallMode.Link, Registry = "test" }
            });

            var plugins = new List<IPlugin> { new CorePlugin() };
            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);

            var plan = builder.Build("test", registryPath, plugins, ScopeFilter.GlobalOnly(), new List<string>());

            Assert.Single(plan);
            Assert.Equal(InstallMode.Link, plan[0].Mode);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Build_ProjectLocal_Emits_When_Existing_Record_Matches_Project()
    {
        var tempDir = TestHelpers.GetTempDir();
        var projectDir = Path.Combine(tempDir, "projects", "a");
        Directory.CreateDirectory(projectDir);
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;
            _gitOps.Files[registryPath] = new[] { "core/skills/review.md" };

            var destPath = Path.Combine(projectDir, ".claude", "skills", "review.md");
            _stateStore.Save("test", new List<InstallRecord>
            {
                new()
                {
                    SourcePath = "core/skills/review.md",
                    Plugin = "core",
                    DestinationPath = destPath,
                    InstallMode = InstallMode.Copy,
                    Registry = "test"
                }
            });

            var plugins = new List<IPlugin> { new CorePlugin() };
            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);
            var plan = builder.Build("test", registryPath, plugins, ScopeFilter.All(), new List<string> { projectDir });

            var local = Assert.Single(plan.Where(d => d.Scope == Scope.ProjectLocal));
            Assert.Equal(destPath, local.DestinationPath);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Build_ProjectLocal_Skips_New_Artifacts_Even_For_Tended_Projects()
    {
        var tempDir = TestHelpers.GetTempDir();
        var projectDir = Path.Combine(tempDir, "projects", "a");
        Directory.CreateDirectory(projectDir);
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;
            _gitOps.Files[registryPath] = new[] { "core/skills/review.md" };

            // No existing records for this artifact
            var plugins = new List<IPlugin> { new CorePlugin() };
            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);
            var plan = builder.Build("test", registryPath, plugins, ScopeFilter.All(), new List<string> { projectDir });

            // Should only have global scope (the artifact is new at project-local)
            // Wait, actually per spec: only global auto-applies for new artifacts.
            // Project-local only re-installs if already present in state.
            // So plan should have 1 global item.
            var global = Assert.Single(plan.Where(d => d.Scope == Scope.Global));
            Assert.Equal("core/skills/review.md", global.SourcePath);
            Assert.DoesNotContain(plan, d => d.Scope == Scope.ProjectLocal);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Build_Throws_SyncConflictException_When_Two_Plugins_Target_Same_Destination()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            _stateStore = new StateStore(tempDir);
            var registryPath = tempDir;
            _gitOps.Files[registryPath] = new[] { "core/skills/review.md" };

            var plugin1 = new TestPlugin("plugin1", new[] { "core/**" }, (a, s, m) => new InstallPlan("/tmp/same/path.md"));
            var plugin2 = new TestPlugin("plugin2", new[] { "core/**" }, (a, s, m) => new InstallPlan("/tmp/same/path.md"));

            var builder = new InstallPlanBuilder(_globMatcher, _pathExpander, _checksum, _stateStore, _gitOps);

            Assert.Throws<SyncConflictException>(() =>
                builder.Build("test", registryPath, new[] { plugin1, plugin2 }, ScopeFilter.GlobalOnly(), new List<string>()));
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    private sealed class FakeGitOps : IGitOps
    {
        public Dictionary<string, string[]> Files { get; } = new();

        public void Clone(string remote, string path) => throw new NotImplementedException();
        public void CommitAll(string path, string message) => throw new NotImplementedException();
        public void Init(string path) => throw new NotImplementedException();
        public bool IsClean(string path) => throw new NotImplementedException();
        public IReadOnlyList<string> ListTrackedFiles(string path) => Files.TryGetValue(path, out var f) ? f : new List<string>();
        public void Pull(string path) => throw new NotImplementedException();
        public void Push(string path) => throw new NotImplementedException();
        public void SetRemote(string path, string remote) => throw new NotImplementedException();
        public IReadOnlyList<string> Status(string path) => throw new NotImplementedException();
    }

    private sealed record TestPlugin(string Name, string[] PathClaims, Func<Artifact, Scope, InstallMode, InstallPlan?> PlanFactory) : IPlugin
    {
        string IPlugin.Name => Name;
        IEnumerable<string> IPlugin.PathClaims => PathClaims;
        public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode) => PlanFactory(artifact, scope, mode);
    }
}
