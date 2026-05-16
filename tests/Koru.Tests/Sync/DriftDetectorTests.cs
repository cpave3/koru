using Koru.Cli.Core.Models;
using Koru.Cli.Core.Sync;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests.Sync;

public class DriftDetectorTests
{
    private readonly Checksum _checksum = new();
    private readonly DriftDetector _detector;

    public DriftDetectorTests()
    {
        _detector = new DriftDetector(_checksum);
    }

    [Fact]
    public void NoChange_When_Both_Checksums_Match()
    {
        var tempDir = TestHelpers.GetTempDir();
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.md");
            var destPath = Path.Combine(tempDir, "dest.md");
            File.WriteAllText(sourcePath, "# Hello");
            File.WriteAllText(destPath, "# Hello");

            var record = new InstallRecord
            {
                SourcePath = "source.md",
                DestinationPath = destPath,
                InstallMode = InstallMode.Copy,
                SourceChecksum = _checksum.ComputeSha256(sourcePath),
                InstalledChecksum = _checksum.ComputeSha256(destPath),
                Registry = "test"
            };

            var result = _detector.Check(record, tempDir);
            Assert.Equal(DriftStatus.NoChange, result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Drifted_When_Installed_File_Changed()
    {
        var tempDir = TestHelpers.GetTempDir();
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.md");
            var destPath = Path.Combine(tempDir, "dest.md");
            File.WriteAllText(sourcePath, "# Hello");
            File.WriteAllText(destPath, "# Hello");
            var originalInstalledChecksum = _checksum.ComputeSha256(destPath);

            // Modify the installed file
            File.WriteAllText(destPath, "# Hello Modified");

            var record = new InstallRecord
            {
                SourcePath = "source.md",
                DestinationPath = destPath,
                InstallMode = InstallMode.Copy,
                SourceChecksum = _checksum.ComputeSha256(sourcePath),
                InstalledChecksum = originalInstalledChecksum,
                Registry = "test"
            };

            var result = _detector.Check(record, tempDir);
            Assert.Equal(DriftStatus.Drifted, result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void SourceChanged_When_Source_File_Changed()
    {
        var tempDir = TestHelpers.GetTempDir();
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.md");
            var destPath = Path.Combine(tempDir, "dest.md");
            File.WriteAllText(sourcePath, "# Hello");
            File.WriteAllText(destPath, "# Hello");
            var originalSourceChecksum = _checksum.ComputeSha256(sourcePath);

            // Modify the source file
            File.WriteAllText(sourcePath, "# Hello Modified");

            var record = new InstallRecord
            {
                SourcePath = "source.md",
                DestinationPath = destPath,
                InstallMode = InstallMode.Copy,
                SourceChecksum = originalSourceChecksum,
                InstalledChecksum = _checksum.ComputeSha256(destPath),
                Registry = "test"
            };

            var result = _detector.Check(record, tempDir);
            Assert.Equal(DriftStatus.SourceChanged, result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Link_Mode_Returns_NoChange()
    {
        var tempDir = TestHelpers.GetTempDir();
        try
        {
            var record = new InstallRecord
            {
                SourcePath = "x.md",
                DestinationPath = "/tmp/x.md",
                InstallMode = InstallMode.Link,
                Registry = "test"
            };

            var result = _detector.Check(record, tempDir);
            Assert.Equal(DriftStatus.NoChange, result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }

    [Fact]
    public void Missing_Destination_Returns_SourceChanged()
    {
        var tempDir = TestHelpers.GetTempDir();
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.md");
            File.WriteAllText(sourcePath, "# Hello");

            var record = new InstallRecord
            {
                SourcePath = "source.md",
                DestinationPath = Path.Combine(tempDir, "nonexistent.md"),
                InstallMode = InstallMode.Copy,
                SourceChecksum = _checksum.ComputeSha256(sourcePath),
                InstalledChecksum = "sha256:abc",
                Registry = "test"
            };

            var result = _detector.Check(record, tempDir);
            Assert.Equal(DriftStatus.SourceChanged, result);
        }
        finally
        {
            TestHelpers.CleanupDir(tempDir);
        }
    }
}
