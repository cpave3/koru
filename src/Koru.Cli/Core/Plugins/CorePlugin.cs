using Koru.Contracts;

namespace Koru.Cli.Core.Plugins;

public class CorePlugin : IPlugin
{
    public string Name => "core";

    public IEnumerable<string> PathClaims => new[] { "core/**" };

    public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
    {
        var path = artifact.Path.Replace('\\', '/');

        if (path.StartsWith("core/skills/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = path["core/skills/".Length..];
            var dest = scope == Scope.Global
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills", fileName)
                : Path.Combine(".claude", "skills", fileName);
            return new InstallPlan(dest);
        }

        if (path.StartsWith("core/agents/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = path["core/agents/".Length..];
            var dest = scope == Scope.Global
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "agents", fileName)
                : Path.Combine(".claude", "agents", fileName);
            return new InstallPlan(dest);
        }

        return null;
    }
}
