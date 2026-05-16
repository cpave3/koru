using Koru.Contracts;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Install;

public record InstallResultEntry(
    string PluginName,
    string DestinationPath,
    InstallMode Mode,
    string SourceChecksum,
    string? InstalledChecksum);

public class ArtifactInstaller
{
    private readonly IConfigStore _configStore;
    private readonly IStateStore _stateStore;
    private readonly IChecksum _checksum;
    private readonly IPathExpander _pathExpander;

    public ArtifactInstaller(
        IConfigStore configStore,
        IStateStore stateStore,
        IChecksum checksum,
        IPathExpander pathExpander)
    {
        _configStore = configStore;
        _stateStore = stateStore;
        _checksum = checksum;
        _pathExpander = pathExpander;
    }

    public IReadOnlyList<InstallResultEntry> Install(
        ResolvedArtifact artifact,
        Scope scope,
        InstallMode mode,
        string projectDirectory,
        string registryRoot)
    {
        var sourcePath = Path.Combine(registryRoot, artifact.SourcePath);
        var results = new List<InstallResultEntry>();
        var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // dest -> plugin

        foreach (var plugin in artifact.ClaimingPlugins)
        {
            var plan = plugin.GetInstallPlan(new Artifact(artifact.SourcePath, registryRoot), scope, mode);
            if (plan is null)
                continue;

            var expandedDest = _pathExpander.Expand(plan.DestinationPath);
            var absoluteDest = Path.IsPathFullyQualified(expandedDest)
                ? expandedDest
                : Path.GetFullPath(Path.Combine(projectDirectory, expandedDest));

            if (destinations.TryGetValue(absoluteDest, out var existingPlugin) && existingPlugin != plugin.Name)
            {
                throw new InvalidOperationException(
                    $"Conflict: plugins '{existingPlugin}' and '{plugin.Name}' both target the same destination: {absoluteDest}");
            }

            destinations[absoluteDest] = plugin.Name;

            var parent = Path.GetDirectoryName(absoluteDest);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                Directory.CreateDirectory(parent);

            string sourceChecksum;
            string? installedChecksum = null;

            if (mode == InstallMode.Link)
            {
                // Remove existing file or symlink before creating new one
                try { File.Delete(absoluteDest); } catch { }

                File.CreateSymbolicLink(absoluteDest, sourcePath);
                sourceChecksum = _checksum.ComputeSha256(sourcePath);
                installedChecksum = null;
            }
            else
            {
                File.Copy(sourcePath, absoluteDest, overwrite: true);
                sourceChecksum = _checksum.ComputeSha256(sourcePath);
                installedChecksum = _checksum.ComputeSha256(absoluteDest);
            }

            results.Add(new InstallResultEntry(plugin.Name, absoluteDest, mode, sourceChecksum, installedChecksum));
        }

        if (results.Count == 0)
            return results;

        // Update state
        var records = _stateStore.Load(artifact.RegistryName).ToList();
        records.RemoveAll(r =>
            r.SourcePath.Equals(artifact.SourcePath, StringComparison.OrdinalIgnoreCase) &&
            artifact.ClaimingPlugins.Any(p => p.Name == r.Plugin));

        foreach (var entry in results)
        {
            records.Add(new InstallRecord
            {
                SourcePath = artifact.SourcePath,
                DestinationPath = entry.DestinationPath,
                InstallMode = entry.Mode,
                Plugin = entry.PluginName,
                SourceChecksum = entry.SourceChecksum,
                InstalledChecksum = entry.InstalledChecksum,
                Registry = artifact.RegistryName
            });
        }

        _stateStore.Save(artifact.RegistryName, records);

        // If project-local, add project to config
        if (scope == Scope.ProjectLocal)
        {
            var config = _configStore.Load();
            var normalizedProject = Path.GetFullPath(projectDirectory);
            if (!config.Projects.Any(p => Path.GetFullPath(p) == normalizedProject))
            {
                config.Projects.Add(normalizedProject);
                _configStore.Save(config);
            }
        }

        return results;
    }
}
