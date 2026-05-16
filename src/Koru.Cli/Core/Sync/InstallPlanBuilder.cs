using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Plugins;
using Koru.Contracts;

namespace Koru.Cli.Core.Sync;

public class InstallPlanBuilder
{
    private readonly IGlobMatcher _globMatcher;
    private readonly IPathExpander _pathExpander;
    private readonly IChecksum _checksum;
    private readonly IStateStore _stateStore;
    private readonly IGitOps _gitOps;

    public InstallPlanBuilder(
        IGlobMatcher globMatcher,
        IPathExpander pathExpander,
        IChecksum checksum,
        IStateStore stateStore,
        IGitOps gitOps)
    {
        _globMatcher = globMatcher;
        _pathExpander = pathExpander;
        _checksum = checksum;
        _stateStore = stateStore;
        _gitOps = gitOps;
    }

    public IReadOnlyList<DesiredInstall> Build(
        string registryName,
        string registryPath,
        IReadOnlyList<IPlugin> plugins,
        ScopeFilter scopeFilter,
        IReadOnlyList<string> tendedProjects)
    {
        var result = new List<DesiredInstall>();
        var existingRecords = _stateStore.Load(registryName);
        var trackedFiles = _gitOps.ListTrackedFiles(registryPath);

        foreach (var filePath in trackedFiles)
        {
            var normalizedPath = filePath.Replace('\\', '/');

            // Exclude registry metadata files
            if (string.Equals(normalizedPath, "registry.yaml", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(normalizedPath, "state.json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (normalizedPath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
                continue;

            var sourceFullPath = Path.Combine(registryPath, filePath);
            var artifact = new Artifact(filePath, registryPath);

            // Find claiming plugins
            var claimingPlugins = new List<IPlugin>();
            foreach (var plugin in plugins)
            {
                foreach (var claim in plugin.PathClaims)
                {
                    if (_globMatcher.Matches(claim, normalizedPath))
                    {
                        claimingPlugins.Add(plugin);
                        break;
                    }
                }
            }

            // Core synthetic plugin fallback
            var isClaimedByCore = false;
            foreach (var claimTable in new[] { "core/skills/", "core/agents/" })
            {
                if (normalizedPath.StartsWith(claimTable, StringComparison.OrdinalIgnoreCase))
                {
                    isClaimedByCore = true;
                    break;
                }
            }

            if (!claimingPlugins.Any() && isClaimedByCore)
            {
                // If no plugin matched but it's in core/, the CorePlugin should have matched.
                // CorePlugin is in plugins list. If it didn't match, it means the path pattern
                // didn't match. Let's add core plugin manually if applicable.
                var corePlugin = plugins.FirstOrDefault(p => p.Name == "core");
                if (corePlugin is null)
                {
                    // This is the synthetic core plugin — won't happen if PluginHost loaded it,
                    // but just in case.
                    corePlugin = new CorePlugin();
                }
                claimingPlugins.Add(corePlugin);
            }

            foreach (var plugin in claimingPlugins)
            {
                // (a) GLOBAL scope — every claiming plugin that returns a non-null plan
                if (scopeFilter.IncludeGlobal)
                {
                    var globalPlan = plugin.GetInstallPlan(artifact, Scope.Global, InstallMode.Copy);
                    if (globalPlan is not null)
                    {
                        var absoluteDest = MakeAbsolute(globalPlan.DestinationPath, projectDirectory: null);

                        // Check for conflict
                        CheckConflict(result, normalizedPath, plugin.Name, absoluteDest);

                        var mode = GetExistingMode(existingRecords, normalizedPath, plugin.Name, Scope.Global, null)
                                    ?? InstallMode.Copy;

                        result.Add(new DesiredInstall(
                            registryName,
                            normalizedPath,
                            absoluteDest,
                            plugin.Name,
                            mode,
                            Scope.Global,
                            null));
                    }
                }

                // (b) PROJECT_LOCAL scope — for each tended project that already has a record
                if (scopeFilter.IncludeProjectLocal && (scopeFilter.ProjectPath is null || tendedProjects.Contains(scopeFilter.ProjectPath)))
                {
                    var projectsToCheck = scopeFilter.ProjectPath is not null
                        ? new[] { scopeFilter.ProjectPath }
                        : tendedProjects;

                    foreach (var project in projectsToCheck)
                    {
                        // Only emit if there is already at least one InstallRecord for this (artifact, plugin, project)
                        var hasExisting = existingRecords.Any(r =>
                            r.SourcePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                            r.Plugin.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase) &&
                            r.DestinationPath.StartsWith(project, StringComparison.OrdinalIgnoreCase));

                        if (!hasExisting)
                            continue;

                        var localPlan = plugin.GetInstallPlan(artifact, Scope.ProjectLocal, InstallMode.Copy);
                        if (localPlan is not null)
                        {
                            var absoluteDest = MakeAbsolute(localPlan.DestinationPath, project);

                            // Check for conflict
                            CheckConflict(result, normalizedPath, plugin.Name, absoluteDest);

                            var mode = GetExistingMode(existingRecords, normalizedPath, plugin.Name, Scope.ProjectLocal, project)
                                        ?? InstallMode.Copy;

                            result.Add(new DesiredInstall(
                                registryName,
                                normalizedPath,
                                absoluteDest,
                                plugin.Name,
                                mode,
                                Scope.ProjectLocal,
                                project));
                        }
                    }
                }
            }
        }

        return result;
    }

    private string MakeAbsolute(string destinationPath, string? projectDirectory)
    {
        var expanded = _pathExpander.Expand(destinationPath);
        if (Path.IsPathFullyQualified(expanded))
            return Path.GetFullPath(expanded);

        if (!string.IsNullOrEmpty(projectDirectory))
            return Path.GetFullPath(Path.Combine(projectDirectory, expanded));

        // Relative path without project context — use current directory (shouldn't happen for global)
        return Path.GetFullPath(expanded);
    }

    private static void CheckConflict(List<DesiredInstall> result, string sourcePath, string pluginName, string destinationPath)
    {
        foreach (var existing in result)
        {
            if (string.Equals(existing.DestinationPath, destinationPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) &&
                existing.Plugin != pluginName)
            {
                throw new SyncConflictException(sourcePath, existing.Plugin, pluginName, destinationPath);
            }
        }
    }

    private static InstallMode? GetExistingMode(
        IReadOnlyList<InstallRecord> existingRecords,
        string sourcePath,
        string pluginName,
        Scope scope,
        string? projectDirectory)
    {
        foreach (var r in existingRecords)
        {
            if (!r.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!r.Plugin.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                continue;
            return r.InstallMode;
        }
        return null;
    }
}
