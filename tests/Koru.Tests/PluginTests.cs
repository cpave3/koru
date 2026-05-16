using System.IO.Abstractions;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Plugins;
using Koru.Contracts;
using static Koru.Tests.TestHelpers;

namespace Koru.Tests;

public class CorePluginTests
{
    [Theory]
    [InlineData("core/skills/foo.md", Scope.Global)]
    [InlineData("core/skills/foo.md", Scope.ProjectLocal)]
    [InlineData("core/agents/bar.md", Scope.Global)]
    [InlineData("core/agents/bar.md", Scope.ProjectLocal)]
    public void GetInstallPlan_Returns_Destination_For_Known_Artifacts(string path, Scope scope)
    {
        var plugin = new CorePlugin();
        var artifact = new Artifact(path, string.Empty);

        var plan = plugin.GetInstallPlan(artifact, scope, InstallMode.Copy);

        Assert.NotNull(plan);
        Assert.EndsWith(Path.GetFileName(path), plan.DestinationPath);
    }

    [Fact]
    public void GetInstallPlan_Returns_Null_For_Unknown_Core_Artifact()
    {
        var plugin = new CorePlugin();
        var artifact = new Artifact("core/other.md", string.Empty);

        var plan = plugin.GetInstallPlan(artifact, Scope.Global, InstallMode.Copy);

        Assert.Null(plan);
    }

    [Fact]
    public void GetInstallPlan_Global_Skills_Go_To_UserProfile()
    {
        var plugin = new CorePlugin();
        var artifact = new Artifact("core/skills/database-review.md", string.Empty);

        var plan = plugin.GetInstallPlan(artifact, Scope.Global, InstallMode.Copy);

        Assert.NotNull(plan);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(Path.Combine(home, ".claude", "skills"), plan.DestinationPath);
    }

    [Fact]
    public void GetInstallPlan_Local_Skills_Go_To_DotClaude()
    {
        var plugin = new CorePlugin();
        var artifact = new Artifact("core/skills/database-review.md", string.Empty);

        var plan = plugin.GetInstallPlan(artifact, Scope.ProjectLocal, InstallMode.Copy);

        Assert.NotNull(plan);
        Assert.StartsWith(Path.Combine(".claude", "skills"), plan.DestinationPath);
    }
}

public class PluginLoaderTests
{
    [Fact]
    public void Load_Discovers_Valid_Plugin_From_Assembly()
    {
        var pluginsDir = GetTempDir();
        var samplePluginDir = Path.Combine(pluginsDir, "Koru.Tests.SamplePlugin");
        Directory.CreateDirectory(samplePluginDir);

        var sourceAssembly = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Koru.Tests.SamplePlugin",
                         "bin", "Debug", "net10.0", "Koru.Tests.SamplePlugin.dll"));
        File.Copy(sourceAssembly, Path.Combine(samplePluginDir, "Koru.Tests.SamplePlugin.dll"));

        var expander = new TestPathExpander(pluginsDir);
        var loader = new PluginLoader(expander);

        var plugins = loader.Load();

        var plugin = Assert.Single(plugins);
        Assert.Equal("sample", plugin.Name);
        Assert.Contains("sample/**", plugin.PathClaims);
    }

    [Fact]
    public void Load_Skips_Assembly_Without_IPlugin_Types()
    {
        var pluginsDir = GetTempDir();
        var otherDir = Path.Combine(pluginsDir, "OtherPkg");
        Directory.CreateDirectory(otherDir);
        File.WriteAllText(Path.Combine(otherDir, "NotAPlugin.txt"), "not a dll");

        var expander = new TestPathExpander(pluginsDir);
        var loader = new PluginLoader(expander);

        var plugins = loader.Load();
        Assert.Empty(plugins);
    }

    private sealed class TestPathExpander : IPathExpander
    {
        private readonly string _pluginsRoot;
        public TestPathExpander(string pluginsRoot) => _pluginsRoot = pluginsRoot;
        public string KoruRoot => throw new NotImplementedException();
        public string RegistriesRoot => throw new NotImplementedException();
        public string PluginsRoot => _pluginsRoot;
        public string Expand(string path) => path;
    }
}
