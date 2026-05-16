namespace Koru.Contracts;

public interface IPlugin
{
    string Name { get; }

    /// <summary>
    /// Declarative path claims used for routing. Each entry is a glob pattern
    /// relative to the registry root (e.g. "chimera/**", "core/skills/*").
    /// </summary>
    IEnumerable<string> PathClaims { get; }

    /// <summary>
    /// Returns the destination path for the given artifact, scope, and mode.
    /// May return null to opt out of installing this artifact.
    /// </summary>
    InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode);
}
