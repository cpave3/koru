using Koru.Cli.Core.Util;

namespace Koru.Tests.Util;

public class ArtifactDiscoveryTests
{
    [Fact]
    public void Single_Md_Files_Become_File_Artifacts()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/review.md",
            "core/skills/style.md",
        });

        Assert.Equal(2, artifacts.Count);
        Assert.All(artifacts, a => Assert.False(a.IsDirectory));
        Assert.Contains(artifacts, a => a.Path == "core/skills/review.md");
        Assert.Contains(artifacts, a => a.Path == "core/skills/style.md");
    }

    [Fact]
    public void SKILL_Md_Claims_Containing_Directory()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/grill-me/SKILL.md",
            "core/skills/grill-me/AGENT-BRIEF.md",
            "core/skills/grill-me/scripts/check.sh",
        });

        Assert.Single(artifacts);
        var artifact = artifacts[0];
        Assert.True(artifact.IsDirectory);
        Assert.Equal("core/skills/grill-me", artifact.Path);
        Assert.Equal(3, artifact.Files.Count);
    }

    [Fact]
    public void Mixed_File_And_Directory_Artifacts_Are_Separated()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/review.md",
            "core/skills/grill-me/SKILL.md",
            "core/skills/grill-me/notes.md",
        });

        Assert.Equal(2, artifacts.Count);
        Assert.Contains(artifacts, a => !a.IsDirectory && a.Path == "core/skills/review.md");
        Assert.Contains(artifacts, a => a.IsDirectory && a.Path == "core/skills/grill-me");
    }

    [Fact]
    public void Nested_SKILL_Md_Takes_Precedence_For_Its_Subtree()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/outer/SKILL.md",
            "core/skills/outer/aside.md",
            "core/skills/outer/inner/SKILL.md",
            "core/skills/outer/inner/notes.md",
        });

        Assert.Equal(2, artifacts.Count);

        var inner = artifacts.Single(a => a.Path == "core/skills/outer/inner");
        Assert.True(inner.IsDirectory);
        Assert.Equal(2, inner.Files.Count);
        Assert.Contains("core/skills/outer/inner/SKILL.md", inner.Files);
        Assert.Contains("core/skills/outer/inner/notes.md", inner.Files);

        var outer = artifacts.Single(a => a.Path == "core/skills/outer");
        Assert.True(outer.IsDirectory);
        Assert.Equal(2, outer.Files.Count);
        Assert.Contains("core/skills/outer/SKILL.md", outer.Files);
        Assert.Contains("core/skills/outer/aside.md", outer.Files);
        Assert.DoesNotContain("core/skills/outer/inner/SKILL.md", outer.Files);
    }

    [Fact]
    public void Non_Md_Files_Outside_Skill_Dirs_Are_Ignored()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/.gitkeep",
            "core/skills/review.md",
            "registry.yaml",
            ".git/config",
            "state.json",
        });

        Assert.Single(artifacts);
        Assert.Equal("core/skills/review.md", artifacts[0].Path);
    }

    [Fact]
    public void Non_Md_Files_Inside_Skill_Dirs_Are_Kept()
    {
        var artifacts = ArtifactDiscovery.Discover(new[]
        {
            "core/skills/build/SKILL.md",
            "core/skills/build/scripts/run.sh",
            "core/skills/build/data.json",
        });

        Assert.Single(artifacts);
        Assert.Equal(3, artifacts[0].Files.Count);
    }
}
