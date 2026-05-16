using System.IO.Abstractions;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Config;
using Koru.Cli.Core.Git;
using Koru.Cli.Core.Plugins;
using Koru.Cli.Core.Registry;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Stubs;
using Koru.Cli.Core.Util;
using Koru.Cli.Commands.Config;
using Koru.Cli.Commands.Install;
using Koru.Cli.Commands.Registry;
using Koru.Cli.Commands.List;
using Koru.Cli.Commands.Plugin;
using Koru.Cli.Commands.Sync;
using Koru.Cli.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace Koru.Cli.Core;

public static class Bootstrap
{
    public static IServiceCollection RegisterServices(IServiceCollection services)
    {
        // --- real implementations owned by this worker ---
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IConfigStore, ConfigStore>();
        services.AddSingleton<IStateStore, StateStore>();
        services.AddSingleton<IChecksum, Checksum>();
        services.AddSingleton<IPathExpander, PathExpander>();
        services.AddSingleton<IGlobMatcher, GlobMatcher>();

        // --- plugin host (owned by sibling worker but real implementation exists) ---
        services.AddSingleton<IPluginHost, PluginHost>();
        services.AddSingleton<IArtifactResolver, Core.Install.ArtifactResolver>();
        services.AddSingleton<Core.Install.ArtifactInstaller>();

        // --- registry management (filled by this worker) ---
        services.AddSingleton<IGitOps, GitOperations>();
        services.AddSingleton<IRegistryManifestStore, RegistryManifestStore>();

        // --- sync engine ---
        services.AddTransient<InstallPlanBuilder>();
        services.AddTransient<SyncReconciler>();
        services.AddTransient<DriftDetector>();
        services.AddTransient<LinkInstaller>();
        services.AddTransient<CopyInstaller>();

        // --- commands with DI constructors ---
        services.AddTransient<InitCommand>();
        services.AddTransient<LinkCommand>();
        services.AddTransient<UseCommand>();
        services.AddTransient<RegistryStatusCommand>();
        services.AddTransient<ListRegistriesCommand>();
        services.AddTransient<ConfigGetCommand>();
        services.AddTransient<ConfigSetCommand>();
        services.AddTransient<ConfigListCommand>();
        services.AddTransient<PluginAddCommand>();
        services.AddTransient<PluginRemoveCommand>();
        services.AddTransient<ListPluginsCommand>();
        services.AddTransient<InstallCommand>();
        services.AddTransient<RemoveCommand>();
        services.AddTransient<UntendCommand>();
        services.AddTransient<ListArtifactsCommand>();
        services.AddTransient<SyncCommand>();
        services.AddTransient<StatusCommand>();
        services.AddTransient<ResetCommand>();

        return services;
    }
}
