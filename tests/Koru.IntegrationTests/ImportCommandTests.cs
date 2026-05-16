using System.Diagnostics;

namespace Koru.IntegrationTests;

public class ImportCommandTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IDisposable _workingDir;
    private readonly string _sourceRepoDir;

    public ImportCommandTests()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        _fixture = new IntegrationTestFixture();
        _workingDir = _fixture.UseProjectDir();
        _sourceRepoDir = Path.Combine(Path.GetTempPath(), $"koru-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceRepoDir);
    }

    public void Dispose()
    {
        _workingDir.Dispose();
        _fixture.Dispose();
        try { Directory.Delete(_sourceRepoDir, recursive: true); } catch { }
    }

    [Fact]
    public void Import_Of_Single_Md_File_Copies_With_Frontmatter_And_Commits()
    {
        var registry = $"reg-{Guid.NewGuid():N}"[..8];
        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", registry).ExitCode);

        var sourceUrl = MakeFakeSourceRepoWithFile("notes/idea.md", "# Big Idea\n\nbody");

        var result = CommandRunner.Run(
            _fixture.Services,
            "import", sourceUrl, "notes/idea.md", "--registry", registry, "--yes");
        Assert.Equal(0, result.ExitCode);

        var imported = Path.Combine(_fixture.KoruHome, "registries", registry, "core", "skills", "idea.md");
        Assert.True(File.Exists(imported), $"expected {imported}; output: {result.Output}");

        var content = File.ReadAllText(imported);
        Assert.StartsWith("---", content);
        Assert.Contains("source:", content);
        Assert.Contains($"repo: {sourceUrl}", content);
        Assert.Contains("path: notes/idea.md", content);
        Assert.Contains("# Big Idea", content);

        var commits = RunGit(Path.Combine(_fixture.KoruHome, "registries", registry), "log", "--oneline");
        Assert.Contains("import: idea from", commits);
    }

    [Fact]
    public void Import_Of_SKILL_Md_Directory_Copies_Tree_And_Stamps_SKILL_Md()
    {
        var registry = $"reg-{Guid.NewGuid():N}"[..8];
        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", registry).ExitCode);

        var sourceUrl = MakeFakeSourceRepoWithFiles(new()
        {
            ["skills/productivity/grill-me/SKILL.md"] = "# Grill Me\n\nbody",
            ["skills/productivity/grill-me/AGENT-BRIEF.md"] = "brief content",
            ["skills/productivity/grill-me/scripts/run.sh"] = "#!/bin/sh",
        });

        var result = CommandRunner.Run(
            _fixture.Services,
            "import", sourceUrl, "skills/productivity/grill-me", "--registry", registry, "--yes");
        Assert.Equal(0, result.ExitCode);

        var importedDir = Path.Combine(_fixture.KoruHome, "registries", registry, "core", "skills", "grill-me");
        Assert.True(Directory.Exists(importedDir), $"output: {result.Output}");

        var skillMd = File.ReadAllText(Path.Combine(importedDir, "SKILL.md"));
        Assert.StartsWith("---", skillMd);
        Assert.Contains("path: skills/productivity/grill-me", skillMd);
        Assert.Contains("# Grill Me", skillMd);

        Assert.Equal("brief content", File.ReadAllText(Path.Combine(importedDir, "AGENT-BRIEF.md")));
        Assert.Equal("#!/bin/sh", File.ReadAllText(Path.Combine(importedDir, "scripts", "run.sh")));
    }

    [Fact]
    public void Import_Refuses_To_Overwrite_Without_Force()
    {
        var registry = $"reg-{Guid.NewGuid():N}"[..8];
        Assert.Equal(0, CommandRunner.Run(_fixture.Services, "init", registry).ExitCode);

        var sourceUrl = MakeFakeSourceRepoWithFile("a/foo.md", "v1");
        Assert.Equal(0, CommandRunner.Run(
            _fixture.Services, "import", sourceUrl, "a/foo.md", "--registry", registry, "--yes").ExitCode);

        var sourceUrl2 = MakeFakeSourceRepoWithFile("b/foo.md", "v2");
        var second = CommandRunner.Run(
            _fixture.Services, "import", sourceUrl2, "b/foo.md", "--registry", registry, "--yes");

        Assert.Contains("already exists", second.Output);
    }

    private string MakeFakeSourceRepoWithFile(string relativePath, string content)
        => MakeFakeSourceRepoWithFiles(new() { [relativePath] = content });

    private string MakeFakeSourceRepoWithFiles(Dictionary<string, string> files)
    {
        var repoDir = Path.Combine(_sourceRepoDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoDir);
        RunGit(repoDir, "init", "-b", "main", "-q");
        RunGit(repoDir, "config", "user.email", "test@koru.local");
        RunGit(repoDir, "config", "user.name", "Koru Test");

        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(repoDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        RunGit(repoDir, "add", ".");
        RunGit(repoDir, "commit", "-q", "-m", "initial");

        return $"file://{repoDir}";
    }

    private static string RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git failed ({string.Join(' ', args)}): {stderr}");
        return stdout;
    }
}
