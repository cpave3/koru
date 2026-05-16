using System.IO.Abstractions;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Contracts;
using Spectre.Console;

namespace Koru.Cli.Core.Sync;

public class SyncReconciler
{
    private readonly IStateStore _stateStore;
    private readonly DriftDetector _driftDetector;
    private readonly LinkInstaller _linkInstaller;
    private readonly CopyInstaller _copyInstaller;
    private readonly IFileSystem _fileSystem;
    private readonly IChecksum _checksum;

    public SyncReconciler(
        IStateStore stateStore,
        DriftDetector driftDetector,
        LinkInstaller linkInstaller,
        CopyInstaller copyInstaller,
        IFileSystem fileSystem,
        IChecksum checksum)
    {
        _stateStore = stateStore;
        _driftDetector = driftDetector;
        _linkInstaller = linkInstaller;
        _copyInstaller = copyInstaller;
        _fileSystem = fileSystem;
        _checksum = checksum;
    }

    public SyncReport Reconcile(
        string registryName,
        string registryRoot,
        IReadOnlyList<DesiredInstall> desired,
        bool dryRun)
    {
        var existingRecords = _stateStore.Load(registryName).ToList();
        var remaining = desired.ToList();
        var newRecords = new List<InstallRecord>();
        var created = 0;
        var updated = 0;
        var removed = 0;
        var drifted = 0;

        foreach (var record in existingRecords)
        {
            // Find matching desired install
            var match = remaining.FirstOrDefault(d =>
                d.SourcePath.Equals(record.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                d.Plugin.Equals(record.Plugin, StringComparison.OrdinalIgnoreCase) &&
                d.DestinationPath.Equals(record.DestinationPath, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                // REMOVE
                if (dryRun)
                {
                    AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would remove: {record.DestinationPath}");
                }
                else
                {
                    TryRemove(record.DestinationPath);
                }
                removed++;
                continue;
            }

            remaining.Remove(match);

            // UPDATE
            if (match.Mode == InstallMode.Link)
            {
                if (dryRun)
                {
                    AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would update link: {match.DestinationPath}");
                }
                else
                {
                    var sourcePath = Path.Combine(registryRoot, match.SourcePath);
                    _linkInstaller.Install(sourcePath, match.DestinationPath);

                    newRecords.Add(new InstallRecord
                    {
                        SourcePath = match.SourcePath,
                        DestinationPath = match.DestinationPath,
                        InstallMode = InstallMode.Link,
                        Plugin = match.Plugin,
                        SourceChecksum = HashSource(sourcePath),
                        InstalledChecksum = null,
                        Registry = registryName
                    });
                }
                updated++;
            }
            else
            {
                // Copy mode: run drift pipeline
                if (!dryRun)
                {
                    var status = _driftDetector.Check(record, registryRoot);
                    if (status == DriftStatus.Drifted)
                    {
                        AnsiConsole.MarkupLine($"[red]Drift detected: {record.DestinationPath} has local modifications. Run 'koru reset {record.SourcePath}' to restore, or revert your changes.[/]");
                        newRecords.Add(record); // Keep record unchanged
                        drifted++;
                        continue;
                    }

                    if (status == DriftStatus.SourceChanged)
                    {
                        var sourcePath = Path.Combine(registryRoot, match.SourcePath);
                        var (sourceChecksum, installedChecksum) = _copyInstaller.Install(sourcePath, match.DestinationPath);

                        newRecords.Add(new InstallRecord
                        {
                            SourcePath = match.SourcePath,
                            DestinationPath = match.DestinationPath,
                            InstallMode = InstallMode.Copy,
                            Plugin = match.Plugin,
                            SourceChecksum = sourceChecksum,
                            InstalledChecksum = installedChecksum,
                            Registry = registryName
                        });
                        updated++;
                    }
                    else
                    {
                        // NoChange — keep existing record
                        newRecords.Add(record);
                    }
                }
                else
                {
                    // In dry-run mode, we'd need to compute drift status to report accurately
                    var status = _driftDetector.Check(record, registryRoot);
                    if (status == DriftStatus.Drifted)
                    {
                        AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would detect drift: {record.DestinationPath}");
                        drifted++;
                    }
                    else if (status == DriftStatus.SourceChanged)
                    {
                        AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would update: {match.DestinationPath}");
                        updated++;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would keep unchanged: {match.DestinationPath}");
                    }
                }
            }
        }

        // Remaining items are new CREATEs
        foreach (var match in remaining)
        {
            if (dryRun)
            {
                AnsiConsole.MarkupLine($"[yellow][[DRY RUN]][/] Would create: {match.DestinationPath}");
            }
            else
            {
                var sourcePath = Path.Combine(registryRoot, match.SourcePath);
                if (match.Mode == InstallMode.Link)
                {
                    _linkInstaller.Install(sourcePath, match.DestinationPath);

                    newRecords.Add(new InstallRecord
                    {
                        SourcePath = match.SourcePath,
                        DestinationPath = match.DestinationPath,
                        InstallMode = InstallMode.Link,
                        Plugin = match.Plugin,
                        SourceChecksum = HashSource(sourcePath),
                        InstalledChecksum = null,
                        Registry = registryName
                    });
                }
                else
                {
                    var (sourceChecksum, installedChecksum) = _copyInstaller.Install(sourcePath, match.DestinationPath);

                    newRecords.Add(new InstallRecord
                    {
                        SourcePath = match.SourcePath,
                        DestinationPath = match.DestinationPath,
                        InstallMode = InstallMode.Copy,
                        Plugin = match.Plugin,
                        SourceChecksum = sourceChecksum,
                        InstalledChecksum = installedChecksum,
                        Registry = registryName
                    });
                }
            }
            created++;
        }

        if (!dryRun)
        {
            _stateStore.Save(registryName, newRecords);
        }

        return new SyncReport(created, updated, removed, drifted, false);
    }

    private void TryRemove(string path)
    {
        try
        {
            if (_fileSystem.File.Exists(path))
                _fileSystem.File.Delete(path);
            else if (_fileSystem.Directory.Exists(path))
                _fileSystem.Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: could not remove {path}: {ex.Message}[/]");
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: could not remove {path}: {ex.Message}[/]");
        }
    }

    private string HashSource(string sourcePath)
        => Directory.Exists(sourcePath)
            ? _checksum.ComputeSha256Tree(sourcePath)
            : _checksum.ComputeSha256(sourcePath);
}
