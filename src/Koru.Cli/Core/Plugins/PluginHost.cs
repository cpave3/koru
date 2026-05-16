using System.IO.Compression;
using Koru.Contracts;
using Koru.Cli.Core.Abstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Koru.Cli.Core.Plugins;

public class PluginHost : IPluginHost
{
    private readonly IPathExpander _pathExpander;
    private readonly IConfigStore _configStore;
    private readonly List<IPlugin> _loadedPlugins = new();

    public PluginHost(IPathExpander pathExpander, IConfigStore configStore)
    {
        _pathExpander = pathExpander;
        _configStore = configStore;
        LoadAll();
    }

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins;

    public IReadOnlyList<string> InstalledPluginNames
    {
        get
        {
            var names = new List<string> { "core" };
            var pluginsRoot = _pathExpander.PluginsRoot;
            if (Directory.Exists(pluginsRoot))
            {
                foreach (var dir in Directory.GetDirectories(pluginsRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
            return names;
        }
    }

    public void LoadAll()
    {
        _loadedPlugins.Clear();

        var pluginsRoot = _pathExpander.PluginsRoot;
        Directory.CreateDirectory(pluginsRoot);

        var loader = new PluginLoader(_pathExpander);
        var discovered = loader.Load();

        _loadedPlugins.Add(new CorePlugin());
        _loadedPlugins.AddRange(discovered);
    }

    public string Install(string nugetName, IEnumerable<string> feeds)
    {
        var feedList = feeds.ToList();
        if (feedList.Count == 0)
        {
            feedList.Add("https://api.nuget.org/v3/index.json");
        }

        // Step 1: Find latest stable version across all feeds
        NuGetVersion? latestVersion = null;
        foreach (var feed in feedList)
        {
            try
            {
                var source = new PackageSource(feed);
                var repo = Repository.Factory.GetCoreV3(source.Source);
                var resource = repo.GetResourceAsync<FindPackageByIdResource>().GetAwaiter().GetResult();
                using var cacheContext = new SourceCacheContext();
                var versions = resource.GetAllVersionsAsync(nugetName, cacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
                var stable = versions.Where(v => !v.IsPrerelease).MaxBy(v => v.Version);
                if (stable != null && (latestVersion == null || stable > latestVersion))
                {
                    latestVersion = stable;
                }
            }
            catch
            {
                // Ignore feed errors and try next
            }
        }

        if (latestVersion == null)
        {
            throw new InvalidOperationException($"Could not find a stable version for package '{nugetName}' in any configured feed.");
        }

        // Step 2: Download the .nupkg
        var tempNupkg = Path.GetTempFileName();
        bool downloaded = false;
        foreach (var feed in feedList)
        {
            try
            {
                var source = new PackageSource(feed);
                var repo = Repository.Factory.GetCoreV3(source.Source);
                var resource = repo.GetResourceAsync<FindPackageByIdResource>().GetAwaiter().GetResult();
                using var cacheContext = new SourceCacheContext();
                using var fs = File.Create(tempNupkg);
                resource.CopyNupkgToStreamAsync(nugetName, latestVersion, fs, cacheContext, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
                downloaded = true;
                break;
            }
            catch
            {
                // Try next feed
            }
        }

        if (!downloaded)
        {
            File.Delete(tempNupkg);
            throw new InvalidOperationException($"Failed to download package '{nugetName}' v{latestVersion} from any configured feed.");
        }

        // Step 3: Extract
        var extractDir = Path.Combine(_pathExpander.PluginsRoot, nugetName);
        if (Directory.Exists(extractDir))
        {
            foreach (var file in Directory.GetFiles(extractDir))
            {
                File.Delete(file);
            }
        }
        else
        {
            Directory.CreateDirectory(extractDir);
        }

        using (var archive = new ZipArchive(File.OpenRead(tempNupkg), ZipArchiveMode.Read))
        {
            var net8Prefix = "lib/net8.0/";
            var ns2Prefix = "lib/netstandard2.0/";

            var entries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToList();

            var chosen = entries
                .Where(e => e.FullName.StartsWith(net8Prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (chosen.Count == 0)
            {
                chosen = entries
                    .Where(e => e.FullName.StartsWith(ns2Prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var entry in chosen)
            {
                var destName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(destName))
                    continue;

                var destPath = Path.Combine(extractDir, destName);
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        File.Delete(tempNupkg);

        LoadAll();
        return $"{nugetName} v{latestVersion}";
    }

    public void Remove(string name)
    {
        if (name == "core")
            throw new InvalidOperationException("The 'core' plugin cannot be removed.");

        var pluginsRoot = _pathExpander.PluginsRoot;
        var pluginDir = Path.Combine(pluginsRoot, name);
        if (Directory.Exists(pluginDir))
        {
            Directory.Delete(pluginDir, recursive: true);
        }

        _loadedPlugins.RemoveAll(p => p.Name == name);
    }
}
