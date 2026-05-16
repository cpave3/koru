using Koru.Cli.Core.Util;

namespace Koru.Tests.Util;

public class FrontmatterTests
{
    [Fact]
    public void Parse_File_With_No_Frontmatter_Returns_Empty_And_Whole_Body()
    {
        var (front, body) = Frontmatter.Parse("# Hello\n\nbody");
        Assert.Empty(front);
        Assert.Equal("# Hello\n\nbody", body);
    }

    [Fact]
    public void Parse_File_With_Frontmatter_Splits_Correctly()
    {
        var source = "---\ntitle: Foo\ncategory: dev\n---\n# Hello\n\nbody";
        var (front, body) = Frontmatter.Parse(source);
        Assert.Equal("Foo", front["title"]);
        Assert.Equal("dev", front["category"]);
        Assert.Equal("# Hello\n\nbody", body);
    }

    [Fact]
    public void SetSource_Adds_Source_Block_To_File_Without_Frontmatter()
    {
        var input = "# Skill\n\nbody";
        var block = new SourceBlock(
            Repo: "https://github.com/foo/bar",
            Path: "skills/x",
            Ref: "HEAD",
            Commit: "abcd1234",
            ImportedAt: new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero));

        var result = Frontmatter.SetSource(input, block);

        Assert.StartsWith("---\n", result);
        Assert.Contains("source:", result);
        Assert.Contains("repo: https://github.com/foo/bar", result);
        Assert.Contains("commit: abcd1234", result);
        Assert.Contains("# Skill", result);
    }

    [Fact]
    public void SetSource_Replaces_Existing_Source_And_Preserves_Other_Keys()
    {
        var input = "---\ntitle: keepme\nsource:\n  repo: old\n  commit: old\n---\nbody";
        var block = new SourceBlock("new-repo", "new/path", "main", "feed5", DateTimeOffset.UtcNow);

        var result = Frontmatter.SetSource(input, block);

        Assert.Contains("title: keepme", result);
        Assert.Contains("repo: new-repo", result);
        Assert.DoesNotContain("repo: old", result);
        Assert.Contains("body", result);
    }
}
