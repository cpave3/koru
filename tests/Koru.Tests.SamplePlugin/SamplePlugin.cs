using Koru.Contracts;

namespace Koru.Tests.SamplePlugin;

public class SamplePlugin : IPlugin
{
    public string Name => "sample";

    public IEnumerable<string> PathClaims => new[] { "sample/**" };

    public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
    {
        return new InstallPlan(Path.Combine("/tmp", artifact.Path));
    }
}
