using Koru.Cli.Core.Util;

namespace Koru.Tests.Util;

public class AtomicFileTests : IDisposable
{
    private readonly string _tempDir;

    public AtomicFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"koru-atomic-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Copy_To_Missing_Destination_Writes_File()
    {
        var src = Path.Combine(_tempDir, "src.md");
        var dst = Path.Combine(_tempDir, "out", "dst.md");
        File.WriteAllText(src, "hello");

        AtomicFile.Copy(src, dst);

        Assert.Equal("hello", File.ReadAllText(dst));
    }

    [Fact]
    public void Copy_Overwrites_Destination_Held_Open_By_Another_Reader()
    {
        var src = Path.Combine(_tempDir, "src.md");
        var dst = Path.Combine(_tempDir, "dst.md");
        File.WriteAllText(src, "new");
        File.WriteAllText(dst, "old");

        using var holder = new FileStream(dst, FileMode.Open, FileAccess.Read, FileShare.Read);

        AtomicFile.Copy(src, dst);

        Assert.Equal("new", File.ReadAllText(dst));
    }

    [Fact]
    public void Copy_Leaves_No_Temp_Files_On_Success()
    {
        var src = Path.Combine(_tempDir, "src.md");
        var dst = Path.Combine(_tempDir, "dst.md");
        File.WriteAllText(src, "x");

        AtomicFile.Copy(src, dst);

        var leftovers = Directory.GetFiles(_tempDir, "*.koru-*");
        Assert.Empty(leftovers);
    }

    [Fact]
    public void CopyDirectory_Recursively_Copies_Tree()
    {
        var src = Path.Combine(_tempDir, "skill");
        Directory.CreateDirectory(Path.Combine(src, "scripts"));
        File.WriteAllText(Path.Combine(src, "SKILL.md"), "skill content");
        File.WriteAllText(Path.Combine(src, "scripts", "run.sh"), "#!/bin/sh");

        var dst = Path.Combine(_tempDir, "out", "skill");

        AtomicFile.CopyDirectory(src, dst);

        Assert.Equal("skill content", File.ReadAllText(Path.Combine(dst, "SKILL.md")));
        Assert.Equal("#!/bin/sh", File.ReadAllText(Path.Combine(dst, "scripts", "run.sh")));
    }

    [Fact]
    public void CopyDirectory_Overwrites_Existing_Tree()
    {
        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "SKILL.md"), "new");

        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(dst, "SKILL.md"), "old");
        File.WriteAllText(Path.Combine(dst, "stale.md"), "stale");

        AtomicFile.CopyDirectory(src, dst);

        Assert.Equal("new", File.ReadAllText(Path.Combine(dst, "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(dst, "stale.md")));
    }
}
