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

        var isDirectory = Directory.Exists(sourcePath)
            || (record.SourceChecksum?.StartsWith("sha256-tree:", StringComparison.Ordinal) ?? false);

        if (isDirectory)
        {
            if (!Directory.Exists(destPath))
                return DriftStatus.SourceChanged;

            var currentInstalled = _checksum.ComputeSha256Tree(destPath);
            if (!string.Equals(currentInstalled, record.InstalledChecksum, StringComparison.OrdinalIgnoreCase))
                return DriftStatus.Drifted;

            if (!Directory.Exists(sourcePath))
                return DriftStatus.SourceChanged;

            var currentSource = _checksum.ComputeSha256Tree(sourcePath);
            return string.Equals(currentSource, record.SourceChecksum, StringComparison.OrdinalIgnoreCase)
                ? DriftStatus.NoChange
                : DriftStatus.SourceChanged;
        }

        if (!File.Exists(destPath))
            return DriftStatus.SourceChanged;

        var currentInstalledChecksum = _checksum.ComputeSha256(destPath);
        if (!string.Equals(currentInstalledChecksum, record.InstalledChecksum, StringComparison.OrdinalIgnoreCase))
            return DriftStatus.Drifted;

        if (!File.Exists(sourcePath))
            return DriftStatus.SourceChanged;

        var currentSourceChecksum = _checksum.ComputeSha256(sourcePath);
        if (!string.Equals(currentSourceChecksum, record.SourceChecksum, StringComparison.OrdinalIgnoreCase))
            return DriftStatus.SourceChanged;

        return DriftStatus.NoChange;
    }
}
