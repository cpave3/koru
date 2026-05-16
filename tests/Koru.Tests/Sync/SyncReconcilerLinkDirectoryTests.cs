using System.IO.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Sync;
using Koru.Cli.Core.Util;
using Koru.Contracts;

namespace Koru.Tests.Sync;

public class SyncReconcilerLinkDirectoryTests : IDisposable
{
    private readonly string _tempDir;

    public SyncReconcilerLinkDirectoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"koru-recon-linkdir-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Reconcile_Link_Update_With_Directory_Source_Uses_Tree_Checksum()
    {
        var registryPath = Path.Combine(_tempDir, "registry");
        var sourceDir = Path.Combine(registryPath, "core", "skills", "grill-with-docs");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "SKILL.md"), "# skill");
        File.WriteAllText(Path.Combine(sourceDir, "NOTES.md"), "# notes");

        var destPath = Path.Combine(_tempDir, "dest", "grill-with-docs");

        var checksum = new Checksum();
        var stateStore = new StateStore(_tempDir);
        var driftDetector = new DriftDetector(checksum);
        var linkInstaller = new LinkInstaller(new FileSystem(), new PathExpander());
        var copyInstaller = new CopyInstaller(new FileSystem(), checksum);

        var existing = new InstallRecord
        {
            SourcePath = "core/skills/grill-with-docs",
            DestinationPath = destPath,
            InstallMode = InstallMode.Link,
            Plugin = "core",
            SourceChecksum = "sha256-tree:stale",
            InstalledChecksum = null,
            Registry = "test",
        };
        stateStore.Save("test", new[] { existing });

        var desired = new List<DesiredInstall>
        {
            new("test", "core/skills/grill-with-docs", destPath, "core", InstallMode.Link, Scope.Global, null),
        };

        var reconciler = new SyncReconciler(stateStore, driftDetector, linkInstaller, copyInstaller, new FileSystem(), checksum);
        var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

        Assert.Equal(0, report.Created);
        Assert.Equal(1, report.Updated);

        var saved = stateStore.Load("test").Single();
        Assert.StartsWith("sha256-tree:", saved.SourceChecksum);
    }

    [Fact]
    public void Reconcile_Idempotent_Link_Update_Reports_Zero_Updates()
    {
        var registryPath = Path.Combine(_tempDir, "registry");
        var sourceDir = Path.Combine(registryPath, "core", "skills", "grill-with-docs");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "SKILL.md"), "# skill");

        var destPath = Path.Combine(_tempDir, "dest", "grill-with-docs");

        var checksum = new Checksum();
        var stateStore = new StateStore(_tempDir);
        var driftDetector = new DriftDetector(checksum);
        var linkInstaller = new LinkInstaller(new FileSystem(), new PathExpander());
        var copyInstaller = new CopyInstaller(new FileSystem(), checksum);

        var desired = new List<DesiredInstall>
        {
            new("test", "core/skills/grill-with-docs", destPath, "core", InstallMode.Link, Scope.Global, null),
        };

        var reconciler = new SyncReconciler(stateStore, driftDetector, linkInstaller, copyInstaller, new FileSystem(), checksum);

        // First sync: creates the symlink.
        var first = reconciler.Reconcile("test", registryPath, desired, dryRun: false);
        Assert.Equal(1, first.Created);

        // Second sync: symlink already points where we want. Should be a no-op.
        var second = reconciler.Reconcile("test", registryPath, desired, dryRun: false);
        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Updated);

        // Third sync: still a no-op.
        var third = reconciler.Reconcile("test", registryPath, desired, dryRun: false);
        Assert.Equal(0, third.Updated);
    }

    [Fact]
    public void Reconcile_Link_Create_With_Directory_Source_Uses_Tree_Checksum()
    {
        var registryPath = Path.Combine(_tempDir, "registry");
        var sourceDir = Path.Combine(registryPath, "core", "skills", "grill-with-docs");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "SKILL.md"), "# skill");

        var destPath = Path.Combine(_tempDir, "dest", "grill-with-docs");

        var checksum = new Checksum();
        var stateStore = new StateStore(_tempDir);
        var driftDetector = new DriftDetector(checksum);
        var linkInstaller = new LinkInstaller(new FileSystem(), new PathExpander());
        var copyInstaller = new CopyInstaller(new FileSystem(), checksum);

        var desired = new List<DesiredInstall>
        {
            new("test", "core/skills/grill-with-docs", destPath, "core", InstallMode.Link, Scope.Global, null),
        };

        var reconciler = new SyncReconciler(stateStore, driftDetector, linkInstaller, copyInstaller, new FileSystem(), checksum);
        var report = reconciler.Reconcile("test", registryPath, desired, dryRun: false);

        Assert.Equal(1, report.Created);

        var saved = stateStore.Load("test").Single();
        Assert.StartsWith("sha256-tree:", saved.SourceChecksum);
        Assert.Null(saved.InstalledChecksum);
    }
}
