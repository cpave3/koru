using System.IO.Abstractions;
using Koru.Cli.Core.Git;
using Xunit;

namespace Koru.Tests.Git;

public class GitOperationsTests
{
    private readonly GitOperations _gitOps = new();

    [Fact]
    public void Init_And_Status_IsClean_After_Commit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-git-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            _gitOps.Init(tempDir);

            // Fresh repo with no commits: IsClean should be true
            Assert.True(_gitOps.IsClean(tempDir));

            // Add a file and commit
            File.WriteAllText(Path.Combine(tempDir, "hello.txt"), "world");
            _gitOps.CommitAll(tempDir, "initial commit");

            // After commit, should be clean
            Assert.True(_gitOps.IsClean(tempDir));
            var tracked = _gitOps.ListTrackedFiles(tempDir);
            Assert.Contains("hello.txt", tracked);
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    [Fact]
    public void ListTrackedFiles_Returns_Forward_Slash_Paths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-git-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            _gitOps.Init(tempDir);

            Directory.CreateDirectory(Path.Combine(tempDir, "core", "skills"));
            File.WriteAllText(Path.Combine(tempDir, "core", "skills", "test.md"), "# Test");
            _gitOps.CommitAll(tempDir, "add nested file");

            var tracked = _gitOps.ListTrackedFiles(tempDir);
            Assert.Single(tracked);
            Assert.Equal("core/skills/test.md", tracked[0]);
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    [Fact]
    public void Empty_Repo_Has_Empty_TrackedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"koru-git-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            _gitOps.Init(tempDir);
            var tracked = _gitOps.ListTrackedFiles(tempDir);
            Assert.Empty(tracked);
        }
        finally
        {
            DeleteRecursive(tempDir);
        }
    }

    private static void DeleteRecursive(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var dir in Directory.GetDirectories(path))
        {
            DeleteRecursive(dir);
        }
        foreach (var file in Directory.GetFiles(path))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(path, recursive: true);
    }
}
