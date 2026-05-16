using Koru.Cli.Commands.Config;
using Koru.Cli.Commands.Install;
using Koru.Cli.Commands.List;
using Koru.Cli.Commands.Plugin;
using Koru.Cli.Commands.Registry;
using Koru.Cli.Commands.Sync;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Koru.IntegrationTests;

public sealed class ServiceProviderRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _provider;
    public ServiceProviderRegistrar(IServiceProvider provider) => _provider = provider;
    public ITypeResolver Build() => new ServiceProviderTypeResolver(_provider);
    public void Register(Type service, Type implementation) { }
    public void RegisterInstance(Type service, object implementation) { }
    public void RegisterLazy(Type service, Func<object> factory) { }
}

public sealed class ServiceProviderTypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    public ServiceProviderTypeResolver(IServiceProvider provider) => _provider = provider;
    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
}

/// <summary>
/// Programmatically invokes the Koru CLI with a captured text output.
/// </summary>
public static class CommandRunner
{
    /// <summary>Run with no predefined interactive inputs.</summary>
    public static (int ExitCode, string Output) Run(IServiceProvider services, params string[] args)
        => Run(services, args, null);

    /// <summary>Run and feed <paramref name="inputs"/> to any interactive prompts.</summary>
    public static (int ExitCode, string Output) Run(IServiceProvider services, string[] args, string[]? inputs)
    {
        var registrar = new ServiceProviderRegistrar(services);
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

        var console = new TestConsole().Width(120);
        console.Profile.Capabilities.Interactive = true;
        if (inputs is not null)
        {
            foreach (var line in inputs)
            {
                console.Input.PushTextWithEnter(line);
            }
        }

        var oldConsole = AnsiConsole.Console;
        AnsiConsole.Console = console;
        try
        {
            int exitCode = app.Run(args);
            return (exitCode, console.Output);
        }
        catch (Exception ex)
        {
            return (-1, console.Output + "\nEX: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
        }
        finally
        {
            AnsiConsole.Console = oldConsole;
        }
    }
}
