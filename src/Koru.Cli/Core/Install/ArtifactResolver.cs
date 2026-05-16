using Koru.Contracts;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Install;

public class ArtifactResolver : IArtifactResolver
{
    private readonly IConfigStore _configStore;
    private readonly IGlobMatcher _globMatcher;
    private readonly IPluginHost _pluginHost;
    private readonly IGitOps _gitOps;
    private readonly IPathExpander _pathExpander;

    public ArtifactResolver(
        IConfigStore configStore,
        IGlobMatcher globMatcher,
        IPluginHost pluginHost,
        IGitOps gitOps,
        IPathExpander pathExpander)
    {
        _configStore = configStore;
        _globMatcher = globMatcher;
        _pluginHost = pluginHost;
        _gitOps = gitOps;
        _pathExpander = pathExpander;
    }

    public IReadOnlyList<ResolvedArtifact> Resolve(string query)
    {
        var normalizedQuery = query.Replace('\\', '/');
        return Collect(trackedPath => IsMatch(normalizedQuery, trackedPath));
    }

    public IReadOnlyList<ResolvedArtifact> ResolveAll()
    {
        return Collect(_ => true);
    }

    private IReadOnlyList<ResolvedArtifact> Collect(Func<string, bool> predicate)
    {
        var results = new List<ResolvedArtifact>();
        var config = _configStore.Load();
        var plugins = _pluginHost.LoadedPlugins;

        if (plugins.Count == 0)
            return results;

        foreach (var registry in config.Registries)
        {
            var registryPath = registry.Path;
            if (registryPath.Contains('~'))
                registryPath = _pathExpander.Expand(registryPath);

            var trackedFiles = _gitOps.ListTrackedFiles(registryPath);

            foreach (var trackedPath in trackedFiles)
            {
                var normalizedTracked = trackedPath.Replace('\\', '/');

                if (!IsArtifactFile(normalizedTracked))
                    continue;

                if (!predicate(normalizedTracked))
                    continue;

                var claimingPlugins = new List<IPlugin>();
                foreach (var plugin in plugins)
                {
                    foreach (var claim in plugin.PathClaims)
                    {
                        if (_globMatcher.Matches(claim, normalizedTracked))
                        {
                            claimingPlugins.Add(plugin);
                            break;
                        }
                    }
                }

                if (claimingPlugins.Count > 0)
                {
                    results.Add(new ResolvedArtifact(registry.Name, normalizedTracked, claimingPlugins));
                }
            }
        }

        return results;
    }

    private bool IsMatch(string query, string trackedPath)
    {
        if (HasGlobChars(query))
            return _globMatcher.Matches(query, trackedPath);

        if (trackedPath.Equals(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!query.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && trackedPath.Equals(query + ".md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = query.TrimEnd('/') + "/";
        if (trackedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!query.Contains('/'))
        {
            var fileName = Path.GetFileNameWithoutExtension(trackedPath);
            if (fileName.Equals(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasGlobChars(string query) => query.Contains('*') || query.Contains('?');

    private static bool IsArtifactFile(string normalizedPath)
        => normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
}
