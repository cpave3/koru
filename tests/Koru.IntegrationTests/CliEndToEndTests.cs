using System.Text.Json;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Install;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Plugins;
using Koru.Contracts;

namespace Koru.IntegrationTests;

public class CliEndToEndTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IDisposable _workingDir;

    public CliEndToEndTests()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        _fixture = new IntegrationTestFixture();
        _workingDir = _fixture.UseProjectDir();
    }

    public void Dispose()
    {
        _workingDir.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void Init_Creates_Registry_And_List_Shows_It()
    {
        var name = $"init-{Guid.NewGuid():N}"[..8];

        var init = CommandRunner.Run(_fixture.Services, "init", name);
        Assert.Equal(0, init.ExitCode);
        Assert.Contains($"Registry '{name}' initialized", init.Output);

        var list = CommandRunner.Run(_fixture.Services, "list", "registries");
        Assert.Equal(0, list.ExitCode);
        Assert.Contains(name, list.Output);
    }

    [Fact]
    public void Install_Copy_Then_Sync_DryRun_Is_Clean()
    {
        var name = $"copy-{Guid.NewGuid():N}"[..8];

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", name).ExitCode);

        var registryPath = Path.Combine(_fixture.KoruHome, "registries", name);
        var skillDir = Path.Combine(registryPath, "core", "skills");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "foo.md"), "# Foo Skill\n");
        _fixture.GitCommit(name, "Add foo skill");

        var install = CommandRunner.Run(_fixture.Services, "install", "core/skills/foo.md", "--yes");
        Assert.Equal(0, install.ExitCode);

        var destPath = Path.Combine(_fixture.ProjectDir, ".claude", "skills", "foo.md");
        Assert.True(File.Exists(destPath), $"Expected file at {destPath}");

        var statePath = _fixture.StatePath(name);
        var stateJson = File.ReadAllText(statePath);
        var state = JsonSerializer.Deserialize<StateJson>(stateJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(state);
        Assert.Single(state.Installs);
        Assert.Equal("core/skills/foo.md", state.Installs[0].SourcePath);
        Assert.Equal("copy", state.Installs[0].InstallMode);

        var sync = CommandRunner.Run(_fixture.Services, "sync", "--dry-run", "--yes");
        Assert.Equal(0, sync.ExitCode);
    }

    [Fact]
    public void Drift_Detected_Then_Reset_Restores_Then_Sync_Clean()
    {
        var name = $"drift-{Guid.NewGuid():N}"[..8];

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", name).ExitCode);
        var registryPath = Path.Combine(_fixture.KoruHome, "registries", name);
        var skillDir = Path.Combine(registryPath, "core", "skills");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "foo.md"), "# Foo Skill\n");
        _fixture.GitCommit(name, "Add foo skill");

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "install", "core/skills/foo.md", "--yes").ExitCode);
        var destPath = Path.Combine(_fixture.ProjectDir, ".claude", "skills", "foo.md");
        Assert.True(File.Exists(destPath));

        File.WriteAllText(destPath, "# Drifted Content\n");

        var sync = CommandRunner.Run(_fixture.Services, "sync", "--yes");
        Assert.NotEqual(0, sync.ExitCode);
        Assert.Contains("drift", sync.Output, StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"STATE BEFORE RESET:\n{File.ReadAllText(_fixture.StatePath(name))}\n---END STATE---");

        var reset = CommandRunner.Run(_fixture.Services, new[] { "reset", "core/skills/foo.md" }, new[] { "core" });
        Assert.Equal(0, reset.ExitCode);
        Assert.Contains("Reset", reset.Output);
        var content = File.ReadAllText(destPath);
        Assert.Equal("# Foo Skill\n", content);

        var sync2 = CommandRunner.Run(_fixture.Services, "sync", "--yes");
        Assert.Equal(0, sync2.ExitCode);
    }

    [Fact]
    public void Link_Mode_Bypasses_Drift()
    {
        var name = $"link-{Guid.NewGuid():N}"[..8];

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", name).ExitCode);
        var registryPath = Path.Combine(_fixture.KoruHome, "registries", name);
        var skillDir = Path.Combine(registryPath, "core", "skills");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "bar.md"), "# Bar Skill\n");
        _fixture.GitCommit(name, "Add bar skill");

        // Install directly via service (avoids interactive link-mode prompt)
        var installer = _fixture.Resolve<ArtifactInstaller>();
        var resolver = _fixture.Resolve<IArtifactResolver>();
        var matches = resolver.Resolve("core/skills/bar.md");
        Assert.Single(matches);
        using var _ = _fixture.UseProjectDir();
        var results = installer.Install(matches[0], Scope.ProjectLocal, InstallMode.Link, _fixture.ProjectDir, registryPath);
        Assert.Single(results);
        Assert.Equal(InstallMode.Link, results[0].Mode);

        var destPath = Path.Combine(_fixture.ProjectDir, ".claude", "skills", "bar.md");
        Assert.True(File.Exists(destPath) || new FileInfo(destPath).LinkTarget != null);

        File.WriteAllText(destPath, "# Modified via link\n");

        var sync = CommandRunner.Run(_fixture.Services, "sync", "--yes");
        Assert.Equal(0, sync.ExitCode);
    }

    [Fact]
    public void Registry_Update_Propagates_To_Copy_Artifact()
    {
        var name = $"update-{Guid.NewGuid():N}"[..8];

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", name).ExitCode);
        var registryPath = Path.Combine(_fixture.KoruHome, "registries", name);
        var skillDir = Path.Combine(registryPath, "core", "skills");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "update.md"), "# V1\n");
        _fixture.GitCommit(name, "Add update skill");

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "install", "core/skills/update.md", "--yes").ExitCode);
        var destPath = Path.Combine(_fixture.ProjectDir, ".claude", "skills", "update.md");
        Assert.Equal("# V1\n", File.ReadAllText(destPath));

        var statePath = _fixture.StatePath(name);
        var state1 = JsonSerializer.Deserialize<StateJson>(File.ReadAllText(statePath), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        var oldChecksum = state1.Installs[0].SourceChecksum;

        File.WriteAllText(Path.Combine(skillDir, "update.md"), "# V2\n");
        _fixture.GitCommit(name, "Update skill to v2");

        var sync = CommandRunner.Run(_fixture.Services, "sync", "--yes");
        Assert.Equal(0, sync.ExitCode);
        Assert.Equal("# V2\n", File.ReadAllText(destPath));

        var state2 = JsonSerializer.Deserialize<StateJson>(File.ReadAllText(statePath), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        Assert.NotEqual(oldChecksum, state2.Installs[0].SourceChecksum);
    }

    [Fact]
    public void Artifact_Removed_From_Registry_Sync_Removes_Copy()
    {
        var name = $"remove-{Guid.NewGuid():N}"[..8];

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", name).ExitCode);
        var registryPath = Path.Combine(_fixture.KoruHome, "registries", name);
        var skillDir = Path.Combine(registryPath, "core", "skills");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "remove.md"), "# Remove Me\n");
        _fixture.GitCommit(name, "Add remove skill");

        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "install", "core/skills/remove.md", "--yes").ExitCode);
        var destPath = Path.Combine(_fixture.ProjectDir, ".claude", "skills", "remove.md");
        Assert.True(File.Exists(destPath));

        File.Delete(Path.Combine(skillDir, "remove.md"));
        _fixture.GitCommit(name, "Remove remove skill");

        var sync = CommandRunner.Run(_fixture.Services, "sync", "--yes");
        Assert.Equal(0, sync.ExitCode);
        Assert.False(File.Exists(destPath));

        var state = JsonSerializer.Deserialize<StateJson>(File.ReadAllText(_fixture.StatePath(name)), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        Assert.DoesNotContain(state.Installs, s => s.SourcePath == "core/skills/remove.md");
    }

    [Fact]
    public void PluginLoader_Loads_SamplePlugin()
    {
        var pluginsDir = Path.Combine(_fixture.KoruHome, "plugins");
        var sampleDir = Path.Combine(pluginsDir, "Koru.Tests.SamplePlugin");
        Directory.CreateDirectory(sampleDir);

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Koru.Tests.SamplePlugin.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "Koru.Tests.SamplePlugin", "bin", "Debug", "net10.0", "Koru.Tests.SamplePlugin.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Koru.Tests.SamplePlugin", "bin", "Debug", "net10.0", "Koru.Tests.SamplePlugin.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Koru.Tests.SamplePlugin", "bin", "Debug", "net10.0", "Koru.Tests.SamplePlugin.dll"),
        };
        var sourceAssembly = candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        Assert.NotNull(sourceAssembly);
        File.Copy(sourceAssembly, Path.Combine(sampleDir, "Koru.Tests.SamplePlugin.dll"), overwrite: true);

        var host = new PluginHost(
            new TestPathExpander(_fixture.KoruHome),
            new Koru.Cli.Core.Config.ConfigStore(_fixture.ConfigPath));

        var plugin = Assert.Single(host.LoadedPlugins, p => p.Name == "sample");
        Assert.Equal("sample", plugin.Name);
        Assert.Contains("sample/**", plugin.PathClaims);
    }
}

file class StateJson
{
    public List<InstallRecordJson> Installs { get; set; } = [];
}

file class InstallRecordJson
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string InstallMode { get; set; } = string.Empty;
    public string Plugin { get; set; } = string.Empty;
    public string SourceChecksum { get; set; } = string.Empty;
    public string? InstalledChecksum { get; set; }
    public string Registry { get; set; } = string.Empty;
}
