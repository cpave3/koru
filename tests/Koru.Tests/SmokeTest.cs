using Koru.Cli;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Xunit;

namespace Koru.Tests;

public class SmokeTest
{
    [Fact]
    public void CommandApp_Builds_Without_Throwing()
    {
        var services = new ServiceCollection();
        Koru.Cli.Core.Bootstrap.RegisterServices(services);

        var registrar = new ServiceCollectionRegistrar(services);
        var app = new CommandApp(registrar);

        Assert.NotNull(app);
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
