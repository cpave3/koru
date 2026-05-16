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
dotnet tool install -g \
  --add-source ./src/Koru.Cli/bin/Release \
  --version 1.0.1 \
  Koru
```

The `--version` pin is required: without it, `dotnet tool install` also queries the configured NuGet feeds and may pick up an unrelated `Koru` package from nuget.org that is not a .NET tool (producing a "Koru is not a .NET tool" error). Pinning a version the local source uniquely provides forces the local match.

To install to a non-global location (handy for testing):

```bash
dotnet tool install --tool-path ./.bin \
  --add-source ./src/Koru.Cli/bin/Release \
  --version 1.0.1 \
  Koru
./.bin/koru --help
```

### Updating a global install after a local rebuild

`dotnet tool update` is a no-op when the package version hasn't changed. Two reliable workflows:

```bash
# Workflow A: uninstall + reinstall (no version bump needed)
dotnet tool uninstall -g Koru
dotnet pack src/Koru.Cli -c Release
dotnet tool install -g \
  --add-source ./src/Koru.Cli/bin/Release \
  --version 1.0.1 \
  Koru
```

```bash
# Workflow B: bump <Version> in src/Koru.Cli/Koru.Cli.csproj, then:
dotnet pack src/Koru.Cli -c Release
dotnet tool update -g \
  --add-source ./src/Koru.Cli/bin/Release \
  --version <new-version> \
  Koru
```

## Quick start

### Create a new registry from scratch

```bash
koru init my-registry
# Scaffolds ~/.koru/registries/my-registry/ with core/skills/, core/agents/,
# registry.yaml, and an initialized git repo. Adds it to global config.

cd ~/.koru/registries/my-registry
echo "# Database Review Skill" > core/skills/database-review.md
git add . && git commit -m "Add database review skill"

# Push it to a remote so teammates can use it
koru link git@github.com:me/agent-registry.git
```

### Joining an existing team registry

```bash
koru use https://github.com/acme/agent-registry
# Clones to ~/.koru/registries/acme-team/, reads registry.yaml,
# reports any missing plugins (install with `koru plugin add <name>`)
koru sync
```

### Adding a skill to the team registry

A skill can be either a single `.md` file OR a directory containing `SKILL.md` plus any supplementary files. Both shapes live side-by-side under `core/skills/`.

```bash
cd ~/.koru/registries/acme-team

# Single-file shape
echo "# Database Review Skill" > core/skills/database-review.md

# Directory shape (preferred when you have supplementary docs/scripts)
mkdir -p core/skills/grill-me
cat > core/skills/grill-me/SKILL.md <<'EOF'
# Grill Me
Interview the user relentlessly...
EOF
echo "Agent brief..." > core/skills/grill-me/AGENT-BRIEF.md

git add . && git commit -m "Add new skills" && git push
# Teammates run:
koru sync
```

### Importing a skill from someone else's repo

If a skill lives in another git repo (your own or a public one like `mattpocock/skills`), `koru import` copies it into your registry with provenance frontmatter so you can `sync` and `reset` it as a normal artifact.

```bash
# Import a SKILL.md-style directory from a public skills repo
koru import https://github.com/mattpocock/skills skills/productivity/grill-me
# → cloned, copied into core/skills/grill-me/, committed.
# The imported SKILL.md gains a `source:` frontmatter block recording
# the repo, path, ref, commit, and import timestamp.

# Single .md import
koru import https://github.com/foo/bar README.md --name foo-readme

# Pick interactively (no subpath argument)
koru import https://github.com/mattpocock/skills

# Re-import on top of an existing one
koru import https://github.com/mattpocock/skills skills/productivity/grill-me --force
```

After import, the artifact is a regular Koru asset: `koru install grill-me` places `~/.claude/skills/grill-me/` (whole tree), `koru sync` keeps it fresh from your registry, `koru reset` recovers from drift.

### Installing a skill in a project

```bash
cd ~/Projects/acme-webapp
koru install core/skills/database-review
# Select scope (global/project-local) and mode (link/copy)
```

You can install several artifacts in one go:

```bash
# multiple artifacts at once — scope/mode prompted once for the batch
koru install core/skills/review core/skills/style core/agents/format

# a whole namespace (any tracked file under the prefix)
koru install core/agents

# glob pattern (quote it so the shell doesn't expand it locally)
koru install 'core/agents/review-*'

# pick interactively — space to toggle, enter to install selected
koru install
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
| `koru install [artifacts...] [--yes]` | Install one or more artifacts. Accepts exact paths, directory prefixes (`core/agents`), or globs (`'core/agents/review-*'`). Bare `koru install` opens an interactive multi-select picker. |
| `koru import <git-url> [<subpath>] [--name <local>] [--registry <reg>] [--force] [--yes]` | Copy a skill from another git repo into your registry, with a YAML `source:` frontmatter block recording its provenance. Supports both single-file (`.md`) and directory (`SKILL.md` + siblings) skills. Omit `<subpath>` for an interactive picker over the source repo. |
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
