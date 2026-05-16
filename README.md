# Koru

A .NET-based CLI tool for managing coding agent skills, agent definitions, and configuration files via a git-backed registry model with a plugin system. Teams publish artifacts (markdown files) to a registry, and developers install them globally or per-project with deterministic, versioned placement.

## Install

### From NuGet (recommended)

```bash
dotnet tool install -g Koru
```

### From source

```bash
dotnet pack src/Koru.Cli -c Release
dotnet tool install -g --add-source ./src/Koru.Cli/nupkg Koru
```

## Quick start

### Team onboarding

```bash
koru use https://github.com/acme/agent-registry
# Clones to ~/.koru/registries/acme-team/ and does an initial sync
```

### Adding a skill to the team registry

```bash
cd ~/.koru/registries/acme-team
echo "# Database Review Skill" > core/skills/database-review.md
git add . && git commit -m "Add database review skill" && git push
# Teammates run:
koru sync
```

### Installing a skill in a project

```bash
cd ~/Projects/acme-webapp
koru install core/skills/database-review
# Select scope (global/project-local) and mode (link/copy)
```

### Daily sync

```bash
koru sync
```

### Resolving drift

```bash
koru sync
# Drift detected on a copy-mode artifact
koru reset core/skills/database-review
# Restores from registry
koru sync
```

## Command reference

| Command | Description |
|---|---|
| `koru init <name>` | Scaffold a new local registry with default layout. |
| `koru link <remote-url>` | Set the origin remote for the current registry and push. |
| `koru use <repo-url> [name]` | Clone an existing registry, report missing plugins, and run an initial sync. |
| `koru sync [--dry-run] [--global-only] [--project <path>] [--yes]` | Pull registries and reconcile installed artifacts. |
| `koru status` | Preview what a sync would create, update, remove, or detect as drifted. |
| `koru install <artifact-path> [--yes]` | Install an artifact into the current project directory. |
| `koru remove <artifact-path> [--plugin <name>]` | Remove an installed artifact and its state record. |
| `koru reset <artifact-path> [--plugin <name>]` | Re-copy a drifted artifact from the registry and refresh state. |
| `koru untend <path>` | Remove a project from the tended-projects list. |
| `koru list registries` | List configured registries. |
| `koru list plugins` | List installed plugins. |
| `koru list artifacts` | List installable artifacts across registries. |
| `koru plugin add <nuget-name>` | Download and load a plugin from a NuGet package. |
| `koru plugin remove <name>` | Uninstall a plugin. |
| `koru registry status` | Show the current registry's git status. |
| `koru config get <key>` | Read a config value. |
| `koru config set <key> <value>` | Write a config value. |
| `koru config list` | List the current global config. |

## Authoring plugins

Plugins are compiled .NET assemblies that implement `IPlugin` (from `Koru.Contracts`). They declare path claims and installation rules.

```csharp
using Koru.Contracts;

public class ChimeraPlugin : IPlugin
{
    public string Name => "chimera";

    public IEnumerable<string> PathClaims => new[] { "chimera/**" };

    public InstallPlan? GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
    {
        var relativePath = artifact.Path.Substring("chimera/".Length);
        var baseDir = scope == Scope.Global
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".chimera")
            : Path.Combine(".", ".chimera");
        return new InstallPlan(Path.Combine(baseDir, relativePath));
    }
}
```

Build the assembly, distribute it as a NuGet package, and install with `koru plugin add <nuget-name>`.

## Where state lives

| File / directory | Description |
|---|---|
| `~/.config/koru/config.json` | Global configuration: registries, projects, NuGet feeds, default registry. To override, set `KORU_HOME`. |
| `~/.koru/registries/<name>/state.json` | Per-registry installation state database. Never committed to git. |
| `~/.koru/plugins/<name>/` | Extracted plugin assemblies. |
