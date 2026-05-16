using Koru.Cli.Core.Models;
using Koru.Cli.Core.Sync;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests.Sync;

public class DirectoryArtifactDriftTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Checksum _checksum = new();

    public DirectoryArtifactDriftTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"koru-drift-dir-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Tree_Drift_Is_Detected_When_Any_File_In_Directory_Changes()
    {
        var src = Path.Combine(_tempDir, "registry", "core", "skills", "grill-me");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "SKILL.md"), "skill body");
        File.WriteAllText(Path.Combine(src, "AGENT-BRIEF.md"), "brief body");

        var dst = Path.Combine(_tempDir, "out", ".claude", "skills", "grill-me");
        AtomicFile.CopyDirectory(src, dst);

        var sourceHash = _checksum.ComputeSha256Tree(src);
        var installedHash = _checksum.ComputeSha256Tree(dst);
        Assert.Equal(sourceHash, installedHash);

        var record = new InstallRecord
        {
            SourcePath = "core/skills/grill-me",
            DestinationPath = dst,
            InstallMode = InstallMode.Copy,
            Plugin = "core",
            SourceChecksum = sourceHash,
            InstalledChecksum = installedHash,
            Registry = "work",
        };

        var detector = new DriftDetector(_checksum);
        Assert.Equal(DriftStatus.NoChange, detector.Check(record, Path.Combine(_tempDir, "registry")));

        File.WriteAllText(Path.Combine(dst, "AGENT-BRIEF.md"), "tampered");
        Assert.Equal(DriftStatus.Drifted, detector.Check(record, Path.Combine(_tempDir, "registry")));
    }

    [Fact]
    public void Source_Tree_Update_Triggers_SourceChanged()
    {
        var src = Path.Combine(_tempDir, "registry", "core", "skills", "grill-me");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "SKILL.md"), "v1");

        var dst = Path.Combine(_tempDir, "out", ".claude", "skills", "grill-me");
        AtomicFile.CopyDirectory(src, dst);

        var record = new InstallRecord
        {
            SourcePath = "core/skills/grill-me",
            DestinationPath = dst,
            InstallMode = InstallMode.Copy,
            Plugin = "core",
            SourceChecksum = _checksum.ComputeSha256Tree(src),
            InstalledChecksum = _checksum.ComputeSha256Tree(dst),
            Registry = "work",
        };

        File.WriteAllText(Path.Combine(src, "SKILL.md"), "v2");

        var detector = new DriftDetector(_checksum);
        Assert.Equal(DriftStatus.SourceChanged, detector.Check(record, Path.Combine(_tempDir, "registry")));
    }
}
