using System.IO.Abstractions;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Sync;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests.Sync;

public class SyncReconcilerTests
{
    private string _tempDir = null!;
    private StateStore _stateStore = null!;
    private DriftDetector _driftDetector = null!;
    private LinkInstaller _linkInstaller = null!;
    private CopyInstaller _copyInstaller = null!;
    private Checksum _checksum = null!;

    public SyncReconcilerTests()
    {
    }

    private SyncReconciler CreateReconciler()
    {
        return new SyncReconciler(
            _stateStore,
            _driftDetector,
            _linkInstaller,
            _copyInstaller,
            new FileSystem(),
            _checksum);
    }

    private void CreateRegistryFile(string registryPath, string relativePath, string content)
    {
        var path = Path.Combine(registryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Reconcile_Creates_New_File_And_Records()
    {
        _tempDir = TestHelpers.GetTempDir();
        try
        {
            var registryPath = Path.Combine(_tempDir, "registry");
            Directory.CreateDirectory(registryPath);
            _stateStore = new StateStore(_tempDir);
            _checksum = new Checksum();
            _driftDetector = new DriftDetector(_checksum);
            var pathExpander = new PathExpander();
            _linkInstaller = new LinkInstaller(new FileSystem(), pathExpander);
            _copyInstaller = new CopyInstaller(new FileSystem(), _checksum);

            CreateRegistryFile(registryPath, "core/skills/new.md", "# New");

            var desired = new List<DesiredInstall>
            {
                new("test", "core/skills/new.md", Path.Combine(_tempDir, "dest", "new.md"), "core", InstallMode.Copy, Scope.Global, null)
            };

            var reconciler = CreateReconciler();
            var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

            Assert.Equal(1, report.Created);
            Assert.Equal(0, report.Updated);
            Assert.Equal(0, report.Removed);
            Assert.Equal(0, report.Drifted);

            Assert.True(File.Exists(Path.Combine(_tempDir, "dest", "new.md")));

            var state = _stateStore.Load("test");
            Assert.Single(state);
            Assert.Equal("core/skills/new.md", state[0].SourcePath);
            Assert.Equal(InstallMode.Copy, state[0].InstallMode);
        }
        finally
        {
            TestHelpers.CleanupDir(_tempDir);
        }
    }

    [Fact]
    public void Reconcile_Updates_When_Source_Changed()
    {
        _tempDir = TestHelpers.GetTempDir();
        try
        {
            var registryPath = Path.Combine(_tempDir, "registry");
            Directory.CreateDirectory(registryPath);
            _stateStore = new StateStore(_tempDir);
            _checksum = new Checksum();
            _driftDetector = new DriftDetector(_checksum);
            var pathExpander = new PathExpander();
            _linkInstaller = new LinkInstaller(new FileSystem(), pathExpander);
            _copyInstaller = new CopyInstaller(new FileSystem(), _checksum);

            CreateRegistryFile(registryPath, "core/skills/existing.md", "# Updated");

            var destPath = Path.Combine(_tempDir, "dest", "existing.md");
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllText(destPath, "# Old");

            // The OLD source checksum was based on "# Old" but the new source is "# Updated"
            var oldSourceChecksum = _checksum.ComputeSha256(destPath); // simulate old match
            var destChecksum = _checksum.ComputeSha256(destPath);

            _stateStore.Save("test", new List<InstallRecord>
            {
                new()
                {
                    SourcePath = "core/skills/existing.md",
                    DestinationPath = destPath,
                    InstallMode = InstallMode.Copy,
                    Plugin = "core",
                    SourceChecksum = oldSourceChecksum,
                    InstalledChecksum = destChecksum,
                    Registry = "test"
                }
            });

            // Now the source file has changed contents ("# Updated") but destination is still "# Old"
            // The installed checksum matches recorded, but source checksum for "# Updated" differs.

            var desired = new List<DesiredInstall>
            {
                new("test", "core/skills/existing.md", destPath, "core", InstallMode.Copy, Scope.Global, null)
            };

            // Wait, we need the source file to be different from what's recorded.
            // The source file was written as "# Updated" initially.
            // The recorded source checksum was based on "# Old" (we computed it from destPath before writing "# Updated" to source).
            // So source checksum should differ.

            var reconciler = CreateReconciler();
            var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

            // The old destination file had the same content as the old source (since we set them equal).
            // After creating the source registry file, we wrote "# Updated" to source
            // But wait - we wrote "# Updated" before computing oldSourceChecksum?
            // No, let me re-read... we wrote "# Updated" to source, then set up dest as "# Old",
            // then computed oldSourceChecksum from dest ("# Old"), and installedChecksum from dest ("# Old").
            // Then the source file still has "# Updated". So source checksum should differ from oldSourceChecksum.
            // Installed checksum (dest "# Old") matches recorded installedChecksum ("# Old").
            // So DriftDetector should return SourceChanged, and reconciler should copy and update.

            // Hmm, but `destChecksum = _checksum.ComputeSha256(destPath)` was computed before source was modified.
            // Actually the source path is `Path.Combine(registryPath, "core/skills/existing.md")` which was written with "# Updated".
            // Then we wrote dest with "# Old".
            // oldSourceChecksum was computed from dest (which is "# Old"), not from source.
            // So source="# Updated" differs from recorded source checksum ("# Old") → SourceChanged is expected.

            // But installed file also "# Old" which matches installedChecksum → no drift.

            // So update should happen.
            // Actually: the installed checksum matches, BUT the source changed. That's SourceChanged.
            // Reconciler should copy and update checksums.

            // Wait, there's a problem: we wrote dest as "# Old" and computed installedChecksum from it.
            // Then the installed file "# Old" has checksum = installedChecksum (matches).
            // Source file "# Updated" has checksum that does NOT match oldSourceChecksum (which was from "# Old").
            // So drift detector returns SourceChanged.

            // Actually wait - when we call Reconcile, the CopyInstaller will copy "# Updated" to dest.
            // So report should show 1 update.

            // Let me re-examine if this works by tracing manually...
            // Actually, the issue is the source and dest content may overlap. Let me minimize...

            // No, actually the source was "# Updated" and dest was "# Old". Both checksums differ.
            // oldSourceChecksum was computed from dest ("# Old") — so it matches dest but not the actual source.
            // destChecksum (also "# Old") matches dest.
            // When DriftDetector runs:
            //   currentInstalledChecksum = ComputeSha256(dest) = hash("# Old") → matches installedChecksum! (no drift)
            //   source file exists, currentSourceChecksum = hash("# Updated") → does NOT match oldSourceChecksum → SourceChanged
            // So it correctly returns SourceChanged.

            // Reconciler should copy source to dest and update state.
            // After reconcile, dest should contain "# Updated".
            Assert.Equal(0, report.Created);
            Assert.Equal(1, report.Updated);

            var content = File.ReadAllText(destPath);
            Assert.Equal("# Updated", content);
        }
        finally
        {
            TestHelpers.CleanupDir(_tempDir);
        }
    }

    [Fact]
    public void Reconcile_Removes_When_No_Longer_Desired()
    {
        _tempDir = TestHelpers.GetTempDir();
        try
        {
            var registryPath = Path.Combine(_tempDir, "registry");
            Directory.CreateDirectory(registryPath);
            _stateStore = new StateStore(_tempDir);
            _checksum = new Checksum();
            _driftDetector = new DriftDetector(_checksum);
            var pathExpander = new PathExpander();
            _linkInstaller = new LinkInstaller(new FileSystem(), pathExpander);
            _copyInstaller = new CopyInstaller(new FileSystem(), _checksum);

            var destPath = Path.Combine(_tempDir, "dest", "old.md");
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllText(destPath, "# Old");

            _stateStore.Save("test", new List<InstallRecord>
            {
                new()
                {
                    SourcePath = "core/skills/old.md",
                    DestinationPath = destPath,
                    InstallMode = InstallMode.Copy,
                    Plugin = "core",
                    SourceChecksum = "sha256:abc",
                    InstalledChecksum = "sha256:abc",
                    Registry = "test"
                }
            });

            var desired = new List<DesiredInstall>(); // Nothing desired

            var reconciler = CreateReconciler();
            var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

            Assert.Equal(0, report.Created);
            Assert.Equal(0, report.Updated);
            Assert.Equal(1, report.Removed);
            Assert.False(File.Exists(destPath));

            var state = _stateStore.Load("test");
            Assert.Empty(state);
        }
        finally
        {
            TestHelpers.CleanupDir(_tempDir);
        }
    }

    [Fact]
    public void Reconcile_DryRun_Does_Not_Write_State_Or_Files()
    {
        _tempDir = TestHelpers.GetTempDir();
        try
        {
            var registryPath = Path.Combine(_tempDir, "registry");
            Directory.CreateDirectory(registryPath);
            _stateStore = new StateStore(_tempDir);
            _checksum = new Checksum();
            _driftDetector = new DriftDetector(_checksum);
            var pathExpander = new PathExpander();
            _linkInstaller = new LinkInstaller(new FileSystem(), pathExpander);
            _copyInstaller = new CopyInstaller(new FileSystem(), _checksum);

            CreateRegistryFile(registryPath, "core/skills/new.md", "# New");

            var desired = new List<DesiredInstall>
            {
                new("test", "core/skills/new.md", Path.Combine(_tempDir, "dest", "new.md"), "core", InstallMode.Copy, Scope.Global, null)
            };

            var reconciler = CreateReconciler();
            var report = reconciler.Reconcile("test", registryPath, desired, dryRun: true);

            Assert.Equal(1, report.Created);
            Assert.False(File.Exists(Path.Combine(_tempDir, "dest", "new.md")));

            // State file should not exist
            var statePath = _stateStore.StatePathFor("test");
            Assert.False(File.Exists(statePath));
        }
        finally
        {
            TestHelpers.CleanupDir(_tempDir);
        }
    }

    [Fact]
    public void Reconcile_Detects_Drift_And_Aborts_File_Update()
    {
        _tempDir = TestHelpers.GetTempDir();
        try
        {
            var registryPath = Path.Combine(_tempDir, "registry");
            Directory.CreateDirectory(registryPath);
            _stateStore = new StateStore(_tempDir);
            _checksum = new Checksum();
            _driftDetector = new DriftDetector(_checksum);
            var pathExpander = new PathExpander();
            _linkInstaller = new LinkInstaller(new FileSystem(), pathExpander);
            _copyInstaller = new CopyInstaller(new FileSystem(), _checksum);

            CreateRegistryFile(registryPath, "core/skills/existing.md", "# Original");

            var destPath = Path.Combine(_tempDir, "dest", "existing.md");
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllText(destPath, "# Original");

            _stateStore.Save("test", new List<InstallRecord>
            {
                new()
                {
                    SourcePath = "core/skills/existing.md",
                    DestinationPath = destPath,
                    InstallMode = InstallMode.Copy,
                    Plugin = "core",
                    SourceChecksum = _checksum.ComputeSha256(destPath), // matches original
                    InstalledChecksum = _checksum.ComputeSha256(destPath), // matches original
                    Registry = "test"
                }
            });

            // User modifies the installed file
            File.WriteAllText(destPath, "# Modified by user");

            var desired = new List<DesiredInstall>
            {
                new("test", "core/skills/existing.md", destPath, "core", InstallMode.Copy, Scope.Global, null)
            };

            var reconciler = CreateReconciler();
            var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

            Assert.Equal(1, report.Drifted);
            Assert.Equal(0, report.Created);
            Assert.Equal(0, report.Updated);

            // File should NOT be overwritten
            var content = File.ReadAllText(destPath);
            Assert.Equal("# Modified by user", content);

            // State should still contain the record
            var state = _stateStore.Load("test");
            Assert.Single(state);
        }
        finally
        {
            TestHelpers.CleanupDir(_tempDir);
        }
    }
}
