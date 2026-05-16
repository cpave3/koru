using Koru.Cli.Core.Install;
using Koru.Contracts;
using Koru.Cli.Core.Config;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Util;
using Koru.Cli.Core.Models;

namespace Koru.Tests.Install;

public class ArtifactInstallerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigStore _configStore;
    private readonly StateStore _stateStore;
    private readonly Checksum _checksum;
    private readonly PathExpander _pathExpander;
    private readonly ArtifactInstaller _installer;

    public ArtifactInstallerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"koru-install-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var configPath = Path.Combine(_tempDir, "config.json");
        _configStore = new ConfigStore(configPath);
        _stateStore = new StateStore(_tempDir);
        _checksum = new Checksum();
        _pathExpander = new PathExpander();
        _installer = new ArtifactInstaller(_configStore, _stateStore, _checksum, _pathExpander);
    }

    public void Dispose()
    {
        DeleteRecursive(_tempDir);
    }

    [Fact]
    public void Install_Copy_Creates_File_And_Records_Checksum()
    {
        // Arrange
        var registryDir = Path.Combine(_tempDir, "registry");
        Directory.CreateDirectory(registryDir);
        var sourcePath = Path.Combine(registryDir, "core/skills/test.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# Test Skill");

        var artifact = new Koru.Cli.Core.Abstractions.ResolvedArtifact(
            "test-reg",
            "core/skills/test.md",
            [new CorePlugin()]);

        var projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);

        // Act
        var results = _installer.Install(artifact, Scope.ProjectLocal, InstallMode.Copy, projectDir, registryDir);

        // Assert
        Assert.Single(results);
        var entry = results[0];
        Assert.Equal("core", entry.PluginName);
        Assert.Equal(InstallMode.Copy, entry.Mode);
        Assert.NotNull(entry.InstalledChecksum);
        Assert.NotEmpty(entry.SourceChecksum);
        Assert.NotEmpty(entry.InstalledChecksum);
        Assert.Equal(entry.SourceChecksum, entry.InstalledChecksum);

        // Destination should exist
        Assert.True(File.Exists(entry.DestinationPath));

        // State should be recorded
        var records = _stateStore.Load("test-reg");
        Assert.Single(records);
        Assert.Equal("core/skills/test.md", records[0].SourcePath);
        Assert.Equal(InstallMode.Copy, records[0].InstallMode);
        Assert.Equal("core", records[0].Plugin);
        Assert.NotNull(records[0].InstalledChecksum);

        // Project should be added to config
        var config = _configStore.Load();
        Assert.Contains(projectDir, config.Projects);
    }

    [Fact]
    public void Install_Link_Creates_Symlink_With_Null_Checksum()
    {
        // Arrange
        var registryDir = Path.Combine(_tempDir, "registry");
        Directory.CreateDirectory(registryDir);
        var sourcePath = Path.Combine(registryDir, "core/skills/test.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# Test Skill");

        var artifact = new Koru.Cli.Core.Abstractions.ResolvedArtifact(
            "test-reg",
            "core/skills/test.md",
            [new CorePlugin()]);

        var projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);

        // Act
        var results = _installer.Install(artifact, Scope.ProjectLocal, InstallMode.Link, projectDir, registryDir);

        // Assert
        Assert.Single(results);
        var entry = results[0];
        Assert.Equal("core", entry.PluginName);
        Assert.Equal(InstallMode.Link, entry.Mode);
        Assert.Null(entry.InstalledChecksum);
        Assert.NotEmpty(entry.SourceChecksum);

        // Destination should exist (as symlink)
        Assert.True(File.Exists(entry.DestinationPath) || IsSymlink(entry.DestinationPath));

        // State
        var records = _stateStore.Load("test-reg");
        Assert.Single(records);
        Assert.Equal(InstallMode.Link, records[0].InstallMode);
        Assert.Null(records[0].InstalledChecksum);
    }

    [Fact]
    public void Install_Global_Does_Not_Add_To_Projects()
    {
        // Arrange
        var registryDir = Path.Combine(_tempDir, "registry");
        Directory.CreateDirectory(registryDir);
        var sourcePath = Path.Combine(registryDir, "core/skills/test.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# Test");

        var artifact = new Koru.Cli.Core.Abstractions.ResolvedArtifact(
            "test-reg",
            "core/skills/test.md",
            [new CorePlugin()]);

        var projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedDest = Path.Combine(home, ".claude", "skills", "test.md");
        // Clean up before test
        try { File.Delete(expectedDest); } catch { }

        // Act
        var results = _installer.Install(artifact, Scope.Global, InstallMode.Copy, projectDir, registryDir);

        // Assert
        Assert.Single(results);
        Assert.True(File.Exists(results[0].DestinationPath));

        var config = _configStore.Load();
        Assert.DoesNotContain(projectDir, config.Projects);
    }

    [Fact]
    public void Install_Conflicting_Plugins_Throws()
    {
        // Arrange
        var registryDir = Path.Combine(_tempDir, "registry");
        Directory.CreateDirectory(registryDir);
        var sourcePath = Path.Combine(registryDir, "test.md");
        File.WriteAllText(sourcePath, "# Test");

        // Two plugins targeting same destination
        var conflictPlugin1 = new FakePlugin("pluginA", new[] { "**" }, (a, s, m) => new InstallPlan(Path.Combine(s == Scope.ProjectLocal ? "/tmp/proj" : "/tmp/home", "test.md")));
        var conflictPlugin2 = new FakePlugin("pluginB", new[] { "**" }, (a, s, m) => new InstallPlan(Path.Combine(s == Scope.ProjectLocal ? "/tmp/proj" : "/tmp/home", "test.md")));

        var artifact = new Koru.Cli.Core.Abstractions.ResolvedArtifact(
            "test-reg",
            "test.md",
            [conflictPlugin1, conflictPlugin2]);

        // Act + Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _installer.Install(artifact, Scope.ProjectLocal, InstallMode.Copy, _tempDir, registryDir));
        Assert.Contains("Conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pluginA", ex.Message);
        Assert.Contains("pluginB", ex.Message);
    }

    [Fact]
    public void Install_Reinstall_Overwrites_State()
    {
        // Arrange
        var registryDir = Path.Combine(_tempDir, "registry");
        Directory.CreateDirectory(registryDir);
        var sourcePath = Path.Combine(registryDir, "core/skills/test.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# Test");

        var artifact = new Koru.Cli.Core.Abstractions.ResolvedArtifact(
            "test-reg",
            "core/skills/test.md",
            [new CorePlugin()]);

        var projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);

        // First install
        _installer.Install(artifact, Scope.ProjectLocal, InstallMode.Copy, projectDir, registryDir);
        var records1 = _stateStore.Load("test-reg");
        Assert.Single(records1);

        // Modify source
        File.WriteAllText(sourcePath, "# Modified");
        // Reinstall
        _installer.Install(artifact, Scope.ProjectLocal, InstallMode.Copy, projectDir, registryDir);
        var records2 = _stateStore.Load("test-reg");
        Assert.Single(records2);

        // Check counts
        Assert.Equal("# Modified", File.ReadAllText(records2[0].DestinationPath));
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            return fi.Exists && (fi.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch { return false; }
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

    private class CorePlugin : Koru.Contracts.IPlugin
    {
        public string Name => "core";
        public IEnumerable<string> PathClaims => new[] { "core/**" };
        public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
        {
            var path = artifact.Path.Replace('\\', '/');
            if (path.StartsWith("core/skills/", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = path["core/skills/".Length..];
                var dest = scope == Scope.Global
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills", fileName)
                    : Path.Combine(".", ".claude", "skills", fileName);
                return new InstallPlan(dest);
            }
            return null;
        }
    }

    private class FakePlugin : Koru.Contracts.IPlugin
    {
        public string Name { get; }
        public IEnumerable<string> PathClaims { get; }
        private readonly Func<Artifact, Scope, InstallMode, InstallPlan?> _planFactory;

        public FakePlugin(string name, IEnumerable<string> claims, Func<Artifact, Scope, InstallMode, InstallPlan?> planFactory)
        {
            Name = name;
            PathClaims = claims;
            _planFactory = planFactory;
        }

        public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
            => _planFactory(artifact, scope, mode);
    }
}
