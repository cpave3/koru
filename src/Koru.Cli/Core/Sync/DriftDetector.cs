using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Contracts;

namespace Koru.Cli.Core.Sync;

public class DriftDetector
{
    private readonly IChecksum _checksum;

    public DriftDetector(IChecksum checksum)
    {
        _checksum = checksum;
    }

    public DriftStatus Check(InstallRecord record, string registryRoot)
    {
        if (record.InstallMode == InstallMode.Link)
            return DriftStatus.NoChange; // Links don't drift

        var sourcePath = Path.Combine(registryRoot, record.SourcePath);
        var destPath = record.DestinationPath;

        if (!File.Exists(destPath))
            return DriftStatus.SourceChanged; // Destination missing → treat as needing update

        // 1. Check installed file drift
        var currentInstalledChecksum = _checksum.ComputeSha256(destPath);
        if (!string.Equals(currentInstalledChecksum, record.InstalledChecksum, StringComparison.OrdinalIgnoreCase))
            return DriftStatus.Drifted;

        // 2. No drift — check if source changed
        if (!File.Exists(sourcePath))
            return DriftStatus.SourceChanged; // Source missing → needs cleanup

        var currentSourceChecksum = _checksum.ComputeSha256(sourcePath);
        if (!string.Equals(currentSourceChecksum, record.SourceChecksum, StringComparison.OrdinalIgnoreCase))
            return DriftStatus.SourceChanged;

        return DriftStatus.NoChange;
    }
}
