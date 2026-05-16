using Koru.Cli.Commands.Config;
using Koru.Cli.Commands.Import;
using Koru.Cli.Commands.Install;
using Koru.Cli.Commands.List;
using Koru.Cli.Commands.Plugin;
using Koru.Cli.Commands.Registry;
using Koru.Cli.Commands.Sync;
using Koru.Cli.Core;
using Koru.Cli.Core.Util;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        AnsiConsole.Console = new VimAnsiConsole(AnsiConsole.Console);

        var services = new ServiceCollection();
        Bootstrap.RegisterServices(services);

        var registrar = new ServiceCollectionRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("koru");

            config.AddCommand<InitCommand>("init");
            config.AddCommand<LinkCommand>("link");
            config.AddCommand<UseCommand>("use");
            config.AddCommand<SyncCommand>("sync");
            config.AddCommand<StatusCommand>("status");
            config.AddCommand<ResetCommand>("reset");
            config.AddCommand<InstallCommand>("install");
            config.AddCommand<ImportCommand>("import");
            config.AddCommand<RemoveCommand>("remove");
            config.AddCommand<UntendCommand>("untend");

            config.AddBranch<ListSettings>("list", list =>
            {
                list.AddCommand<ListRegistriesCommand>("registries");
                list.AddCommand<ListPluginsCommand>("plugins");
                list.AddCommand<ListArtifactsCommand>("artifacts");
            });

            config.AddBranch<PluginSettings>("plugin", plugin =>
            {
                plugin.AddCommand<PluginAddCommand>("add");
                plugin.AddCommand<PluginRemoveCommand>("remove");
            });

            config.AddBranch<RegistrySettings>("registry", registry =>
            {
                registry.AddCommand<RegistryStatusCommand>("status");
            });

            config.AddBranch<ConfigSettings>("config", cfg =>
            {
                cfg.AddCommand<ConfigGetCommand>("get");
                cfg.AddCommand<ConfigSetCommand>("set");
                cfg.AddCommand<ConfigListCommand>("list");
            });
        });

        return app.Run(args);
    }
}

public class ServiceCollectionRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public ServiceCollectionRegistrar(IServiceCollection services)
    {
        _services = services;
    }

    public ITypeResolver Build() => new ServiceCollectionTypeResolver(_services.BuildServiceProvider());

    public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => _services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) => _services.AddSingleton(service, _ => factory());
}

public class ServiceCollectionTypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public ServiceCollectionTypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
}
