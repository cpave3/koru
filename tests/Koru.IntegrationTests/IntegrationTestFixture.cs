using System.Diagnostics;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Config;
using Koru.Cli.Core.State;
using Koru.Cli.Core.Util;
using Microsoft.Extensions.DependencyInjection;

namespace Koru.IntegrationTests;

public sealed class IntegrationTestFixture : IDisposable
{
    public string KoruHome { get; }
    public string ProjectDir { get; }
    public IServiceProvider Services { get; }

    public IntegrationTestFixture()
    {
        KoruHome = Path.Combine(Path.GetTempPath(), $"koru-integ-{Guid.NewGuid():N}");
        Directory.CreateDirectory(KoruHome);
        ProjectDir = Path.Combine(Path.GetTempPath(), $"koru-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ProjectDir);
        Environment.SetEnvironmentVariable("KORU_HOME", KoruHome);

        var serviceCollection = new ServiceCollection();
        Koru.Cli.Core.Bootstrap.RegisterServices(serviceCollection);

        var pathExpander = new TestPathExpander(KoruHome);
        serviceCollection.AddSingleton<IPathExpander>(pathExpander);
        serviceCollection.AddSingleton<IConfigStore>(new ConfigStore(Path.Combine(KoruHome, "config.json")));
        serviceCollection.AddSingleton<IStateStore>(new StateStore(pathExpander));

        Services = serviceCollection.BuildServiceProvider();
    }

    public T Resolve<T>() where T : notnull => Services.GetRequiredService<T>();

    public IDisposable UseProjectDir()
    {
        return new WorkingDirectoryScope(ProjectDir);
    }

    public void GitInit(string registryName)
    {
        var path = Path.Combine(KoruHome, "registries", registryName);
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init -b main -q",
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        };
        p.Start();
        p.WaitForExit();
    }

    public void GitCommit(string registryName, string message)
    {
        var path = Path.Combine(KoruHome, "registries", registryName);
        RunGit(path, "add", ".");
        RunGit(path, "-c", "user.email=test@koru.local", "-c", "user.name=Koru Test", "commit", "-m", message);
    }

    public string StatePath(string registryName) => Path.Combine(KoruHome, "registries", registryName, "state.json");
    public string ConfigPath => Path.Combine(KoruHome, "config.json");

    public void Dispose()
    {
        try { Directory.Delete(KoruHome, recursive: true); } catch { }
        try { Directory.Delete(ProjectDir, recursive: true); } catch { }
    }

    private static void RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)!;
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git failed: {stderr}");
    }

    private sealed class WorkingDirectoryScope : IDisposable
    {
        private readonly string _original;
        public WorkingDirectoryScope(string dir)
        {
            _original = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(dir);
        }
        public void Dispose() => Directory.SetCurrentDirectory(_original);
    }
}

public class TestPathExpander : IPathExpander
{
    private readonly string _koruHome;
    public TestPathExpander(string koruHome) => _koruHome = koruHome;
    public string Expand(string path) => path.StartsWith("~/") ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2)) : path;
    public string KoruRoot => _koruHome;
    public string RegistriesRoot => Path.Combine(_koruHome, "registries");
    public string PluginsRoot => Path.Combine(_koruHome, "plugins");
}
