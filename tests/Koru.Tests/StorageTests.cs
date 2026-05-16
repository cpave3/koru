using System.Security.Cryptography;
using System.Text;
using Koru.Cli.Core.Config;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests;

public class ConfigStoreTests
{
    private static string MkDir() => TestHelpers.GetTempDir();
    private static void RmDir(string d) => TestHelpers.CleanupDir(d);

    [Fact]
    public void Empty_Config_RoundTrip()
    {
        var tempDir = MkDir();
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            var store = new ConfigStore(configPath);
            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Null(loaded.DefaultRegistry);
            Assert.Empty(loaded.Registries);
            Assert.Empty(loaded.Projects);
            Assert.Empty(loaded.NugetFeeds);

            store.Save(loaded);
            Assert.True(File.Exists(configPath));
        }
        finally
        {
            RmDir(tempDir);
        }
    }

    [Fact]
    public void Populated_Config_RoundTrip()
    {
        var tempDir = MkDir();
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            var store = new ConfigStore(configPath);
            var original = new CliConfig
            {
                DefaultRegistry = "acme-team",
                Registries =
                [
                    new RegistryEntry
                    {
                        Name = "acme-team",
                        Remote = "https://github.com/acme/agent-registry",
                        Path = "~/.koru/registries/acme-team"
                    }
                ],
                Projects = ["/home/dev/Projects/acme-webapp"],
                NugetFeeds = ["https://api.nuget.org/v3/index.json"]
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal("acme-team", loaded.DefaultRegistry);
            Assert.Single(loaded.Registries);
            Assert.Equal("acme-team", loaded.Registries[0].Name);
            Assert.Equal("https://github.com/acme/agent-registry", loaded.Registries[0].Remote);
            Assert.Equal("~/.koru/registries/acme-team", loaded.Registries[0].Path);
            Assert.Single(loaded.Projects);
            Assert.Equal("/home/dev/Projects/acme-webapp", loaded.Projects[0]);
            Assert.Single(loaded.NugetFeeds);
            Assert.Equal("https://api.nuget.org/v3/index.json", loaded.NugetFeeds[0]);
        }
        finally
        {
            RmDir(tempDir);
        }
    }

    [Fact]
    public void Returns_Empty_When_File_Missing()
    {
        var tempDir = MkDir();
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            var store = new ConfigStore(configPath);
            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Null(loaded.DefaultRegistry);
        }
        finally
        {
            RmDir(tempDir);
        }
    }
}

public class StateStoreTests
{
    private static string MkDir() => TestHelpers.GetTempDir();
    private static void RmDir(string d) => TestHelpers.CleanupDir(d);

    [Fact]
    public void Empty_State_Roundtrip()
    {
        var tempDir = MkDir();
        try
        {
            var store = new StateStore(tempDir);
            var loaded = store.Load("acme-team");
            Assert.NotNull(loaded);
            Assert.Empty(loaded);
        }
        finally
        {
            RmDir(tempDir);
        }
    }

    [Fact]
    public void State_Roundtrip_Serializes_Enums_As_CamelCase()
    {
        var tempDir = MkDir();
        try
        {
            var store = new StateStore(tempDir);
            var records = new List<InstallRecord>
            {
                new InstallRecord
                {
                    SourcePath = "chimera/modes/review.md",
                    DestinationPath = "/home/dev/Projects/webapp/.chimera/modes/review.md",
                    InstallMode = InstallMode.Link,
                    Plugin = "chimera",
                    SourceChecksum = "sha256:abc...",
                    InstalledChecksum = null,
                    Registry = "acme-team"
                },
                new InstallRecord
                {
                    SourcePath = "core/skills/database-review.md",
                    DestinationPath = "/home/dev/Projects/webapp/.claude/skills/database-review.md",
                    InstallMode = InstallMode.Copy,
                    Plugin = "core",
                    SourceChecksum = "sha256:def...",
                    InstalledChecksum = "sha256:def...",
                    Registry = "acme-team"
                }
            };

            store.Save("acme-team", records);
            var json = File.ReadAllText(Path.Combine(tempDir, "acme-team", "state.json"));
            Assert.Contains("\"installMode\": \"link\"", json);
            Assert.Contains("\"installMode\": \"copy\"", json);

            var loaded = store.Load("acme-team");
            Assert.Equal(2, loaded.Count);
            Assert.Equal(InstallMode.Link, loaded[0].InstallMode);
            Assert.Null(loaded[0].InstalledChecksum);
            Assert.Equal(InstallMode.Copy, loaded[1].InstallMode);
            Assert.Equal("sha256:def...", loaded[1].InstalledChecksum);
        }
        finally
        {
            RmDir(tempDir);
        }
    }

    [Fact]
    public void State_Preserves_Null_InstalledChecksum_For_Link_Mode()
    {
        var tempDir = MkDir();
        try
        {
            var store = new StateStore(tempDir);
            var records = new List<InstallRecord>
            {
                new InstallRecord
                {
                    SourcePath = "chimera/modes/review.md",
                    DestinationPath = "/home/dev/Projects/webapp/.chimera/modes/review.md",
                    InstallMode = InstallMode.Link,
                    Plugin = "chimera",
                    SourceChecksum = "sha256:abc...",
                    InstalledChecksum = null,
                    Registry = "acme-team"
                }
            };

            store.Save("acme-team", records);
            var loaded = store.Load("acme-team");
            Assert.Single(loaded);
            Assert.Null(loaded[0].InstalledChecksum);
            Assert.Equal(InstallMode.Link, loaded[0].InstallMode);
        }
        finally
        {
            RmDir(tempDir);
        }
    }
}

public class ChecksumTests
{
    [Fact]
    public void Computes_Known_Sha256()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFile = Path.Combine(tempDir, "test.txt");
            File.WriteAllText(testFile, "koru");

            var checksum = new Checksum();
            var result = checksum.ComputeSha256(testFile);

            using var sha = SHA256.Create();
            var expectedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes("koru"));
            var expectedHex = Convert.ToHexStringLower(expectedBytes);
            Assert.Equal($"sha256:{expectedHex}", result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }
}

public class GlobMatcherTests
{
    private readonly GlobMatcher _matcher = new();

    [Fact]
    public void Chimera_DoubleStar_Matches_Descendants()
    {
        Assert.True(_matcher.Matches("chimera/**", "chimera/modes/review.md"));
        Assert.True(_matcher.Matches("chimera/**", "chimera/skills/test.md"));
        Assert.True(_matcher.Matches("chimera/**", "chimera/deep/nested/file.md"));
        Assert.False(_matcher.Matches("chimera/**", "chimera.md"));
        Assert.False(_matcher.Matches("chimera/**", "other/chimera/modes/a.md"));
    }

    [Fact]
    public void Star_Pattern_Matches_Single_Segment()
    {
        Assert.True(_matcher.Matches("core/skills/*", "core/skills/database-review.md"));
        Assert.True(_matcher.Matches("core/skills/*", "core/skills/api.md"));
        Assert.True(_matcher.Matches("core/skills/*", "core/skills/.hidden.md"));
        Assert.False(_matcher.Matches("core/skills/*", "core/skills/nested/deep.md"));
    }

    [Fact]
    public void DoubleStar_Glob_Matches_Any_Depth()
    {
        Assert.True(_matcher.Matches("**/*.md", "root.md"));
        Assert.True(_matcher.Matches("**/*.md", "core/skills/database-review.md"));
        Assert.True(_matcher.Matches("**/*.md", "chimera/modes/deep/nested/x.md"));
        Assert.False(_matcher.Matches("**/*.md", "chimera/modes/deep/nested/x.txt"));
        Assert.False(_matcher.Matches("**/*.md", "a/b/c.md.txt"));
    }

    [Fact]
    public void Exact_Segment_Negative_Cases()
    {
        Assert.False(_matcher.Matches("chimera/**", "chimera.md"));
        Assert.False(_matcher.Matches("chimera/**", "chimera"));
        Assert.False(_matcher.Matches("chimera/**", "other/chimera/modes/a.md"));
    }
}

internal static class TestHelpers
{
    public static string GetTempDir() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public static void CleanupDir(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}

public class PathExpanderTests
{
    [Fact]
    public void Expand_Replaces_Leading_Tilde_With_UserProfile()
    {
        var expander = new PathExpander();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, expander.Expand("~"));
        Assert.Equal(Path.Combine(home, ".koru"), expander.Expand("~/.koru"));
        Assert.Equal(home, expander.Expand(home));
    }

    [Fact]
    public void Roots_Are_Correct()
    {
        var expander = new PathExpander();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".koru"), expander.KoruRoot);
        Assert.Equal(Path.Combine(home, ".koru", "registries"), expander.RegistriesRoot);
        Assert.Equal(Path.Combine(home, ".koru", "plugins"), expander.PluginsRoot);
    }
}
