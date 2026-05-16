using System.Reflection;
using System.Runtime.Loader;
using Koru.Contracts;
using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Plugins;

public class PluginLoader
{
    private readonly IPathExpander _pathExpander;

    public PluginLoader(IPathExpander pathExpander)
    {
        _pathExpander = pathExpander;
    }

    public IReadOnlyList<IPlugin> Load()
    {
        var plugins = new List<IPlugin>();
        var pluginsRoot = _pathExpander.PluginsRoot;

        if (!Directory.Exists(pluginsRoot))
        {
            return plugins;
        }

        var pluginDirs = Directory.GetDirectories(pluginsRoot);
        foreach (var pluginDir in pluginDirs)
        {
            var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
            if (dllFiles.Length == 0)
                continue;

            var alc = new AssemblyLoadContext(Path.GetFileName(pluginDir), isCollectible: true);

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var assembly = alc.LoadFromAssemblyPath(dllPath);
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t is not null).ToArray()!;
                    }

                    foreach (var type in types)
                    {
                        if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                            continue;

                        var ctor = type.GetConstructor(Type.EmptyTypes);
                        if (ctor is null)
                            continue;

                        var instance = Activator.CreateInstance(type);
                        if (instance is not IPlugin plugin)
                            continue;

                        if (string.IsNullOrWhiteSpace(plugin.Name))
                        {
                            Console.WriteLine($"Warning: plugin type '{type.FullName}' in '{dllPath}' has empty Name. Skipping.");
                            continue;
                        }

                        if (plugin.PathClaims is null || !plugin.PathClaims.Any())
                        {
                            Console.WriteLine($"Warning: plugin '{plugin.Name}' in '{dllPath}' has empty PathClaims. Skipping.");
                            continue;
                        }

                        plugins.Add(plugin);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: failed to load assembly '{dllPath}' from '{pluginDir}': {ex.Message}");
                }
            }
        }

        return plugins;
    }
}
