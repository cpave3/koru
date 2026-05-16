# Agent CLI Spec

## Overview

A .NET-based CLI tool named Koru for managing coding agent skills, agent definitions, and configuration files. It is designed around a **git-backed registry** model with a **plugin system**, supporting team distribution and deterministic installation across developer machines.

All managed artifacts are markdown files. Their identity and routing are determined by the registry directory layout, the plugin path-claim model, and an installation state database. The tool does **not** interpret or mandate artifact frontmatter — that belongs to the consuming harness.

---

## Core Entities

### Skill
A reusable, domain-specific knowledge module. Stored either as a single markdown file or as a directory containing a `SKILL.md` plus supplementary files. Consumed by an agent harness at runtime to specialize behavior.

**Two storage shapes:**
- **File**: `core/skills/foo.md` — a single markdown file. Simplest case.
- **Directory**: `core/skills/foo/SKILL.md` plus any siblings (`AGENT-BRIEF.md`, `scripts/`, etc.). The directory becomes the artifact; the directory's basename is the artifact's name. Convention borrowed from `mattpocock/skills`, `vercel-labs/agent-skills`, etc.

Discovery rules (ADR 0004):
1. Any directory containing a `SKILL.md` is a directory artifact; every tracked file beneath it belongs to that artifact.
2. A tracked `.md` file outside any `SKILL.md` directory is a single-file artifact.
3. Non-`.md` files outside any `SKILL.md` directory are ignored.
4. Most-specific match wins: a deeper `SKILL.md` carves a sub-artifact out of its parent.

### Agent
A named configuration profile (markdown) that assembles resources (skills, model settings, harness parameters) into a runnable definition. Declares *what* to run and *which* context to load.

### Config / Markdown File
Any other discrete markdown artifact governed by placement rules. Examples include Chimera mode definitions, harness manifests, or environment templates. Unified under the same CLI because they share storage, versioning, and distribution concerns.

### Registry
A git repository storing skills, agents, and config files. A user may have multiple registries (one for work, one for personal). Each registry is linked to a remote URL and has a local working tree.

Every registry root contains a `registry.yaml` manifest declaring which plugins are required.

### Plugin
A compiled .NET assembly distributed as a NuGet package. Implements a standard interface (`IPlugin`) to teach the Koru CLI about artifact placement rules. Plugins declare interest in registry paths (e.g., `chimera/**`).

**Key properties:**
- Installed via `koru plugin add <nuget-name>` into a local plugin directory.
- Validated at load time; invalid assemblies are ignored.
- Overlapping claims between plugins are **additive** (ADR 0001).
- The core owns git, registry resolution, and the install/update/remove lifecycle.
- The registry manifest lists required plugins. `koru use` reports missing plugins for manual installation.

### Installation Mode
How an artifact is placed into a project:
- **`link`** — Symlink to the registry working tree. Updates are automatic (point to the new working tree file after sync).
- **`copy`** — Standalone snapshot. Updates replace the file.

### Scope
Where an artifact is installed:
- **`global`** — System-wide, typically under the user's home directory (`~/.chimera/modes/`, `~/.claude/skills/`).
- **`project-local`** — Inside the current project directory (`.chimera/modes/`, `.claude/skills/`).

Scope is independent of installation mode.

---

## CLI Commands

### `koru init <name>`
Scaffolds a new registry locally. Creates:
- A directory under `~/.koru/registries/<name>/`
- A `registry.yaml` with a starter plugin list (prompted)
- Default directory structure (`core/skills/`, plugin namespaces)
- A local git repo (user pushes to a remote afterward)

### `koru link <remote-url>`
Sets the git origin remote for the current registry and pushes the current state. Must be run inside a registry directory (`~/.koru/registries/<name>/`).

### `koru use <repo-url> [name]`
Clones an existing registry from a remote URL into `~/.koru/registries/<name>/` (derived from URL or provided). Reads `registry.yaml`, reports any missing plugins (user installs with `koru plugin add`), and runs an initial sync.

### `koru sync`
The primary workflow. Iterates **all** linked registries.

1. For each registry:
   - Checks git working tree status.
   - If dirty: prompt user — **abort** (exit, commit/push manually) or **proceed** (install current local state, skip pull).
   - If clean: `git pull`. If merge conflicts, present for resolution.
2. Walk the **git-tracked** registry tree (ignores untracked files, `.git/`, `state.json`).
3. For each file, match against plugin `PathClaims`:
   - If matched by core handler (`core/**`), compute core placement.
   - If matched by plugin, ask each claiming plugin for `GetInstallPlan(artifact, scope, mode)`.
4. If two plugins return the **same absolute destination path** for the same artifact, this is a **conflict**. Sync errors out naming both plugins.
5. Compute desired state: every artifact × every claiming plugin × valid scope.
6. Reconcile against the installation state database.
7. Write updated database.

Flags:
- `--global-only` — Sync only global-scope artifacts.
- `--project <path>` — Sync only the specified project's local artifacts.
- `--dry-run` — Preview changes without applying.

### `koru import <git-url> [<subpath>]`
Copy a skill from an external git repo into a local Koru registry, recording its provenance in a `source:` YAML frontmatter block.

Flags:
- `--name <local-name>` — Override the artifact name in the target registry. Defaults to the source basename.
- `--registry <name>` — Which registry to import into. Defaults to the global default; prompted if unset and multiple registries exist.
- `--force` — Overwrite an existing same-named artifact in the target registry.
- `--yes` — Skip prompts (subpath becomes required in this mode).

Workflow:
1. Clone `<git-url>` to a temporary directory.
2. Run `ArtifactDiscovery` over the cloned tree to enumerate source artifacts.
3. If `<subpath>` is given, select the matching artifact (a `.md` file or a `SKILL.md` directory). Otherwise open an interactive picker over discovered artifacts.
4. For each selected artifact:
   - Compute the target path: `core/skills/<name>.md` for file artifacts or `core/skills/<name>/` for directory artifacts.
   - Refuse if the target already exists, unless `--force` is set.
   - Copy the file or tree into the target registry's working tree.
   - Read the imported `SKILL.md` (or single `.md`), parse any existing YAML frontmatter, set/replace the top-level `source:` block with `{repo, path, ref, commit, imported_at}`, write it back. Other frontmatter keys are preserved.
5. `git add` + `git commit` in the target registry working tree with a message like `import: <name> from <repo>@<short-sha>`.

After import, the artifact is a normal Koru asset: `koru install`, `koru sync`, `koru reset`, and drift detection all apply. The originating repo is **not** queried again; refresh requires re-running `koru import --force`.

### `koru install <artifact-path>`
Install a specific artifact from a registry into the current project directory.

Workflow:
1. Search all linked registries for the artifact (by source path relative to registry root).
2. If found in multiple registries, present a numbered list and ask which.
3. Prompt for scope: global or project-local.
4. Prompt for installation mode: link or copy.
5. If scope is **project-local**, add the current project directory to the "tended projects" list.
6. Compute placement via the claiming plugin(s).
7. Install and record in the state database.

### `koru remove <artifact-path> [--plugin <name>]`
Remove an installed artifact. Looks up the installation database entry by `(sourcePath, plugin)` tuple. If multiple plugins installed this artifact and `--plugin` is not provided, prompt for disambiguation. Removes the destination file and the database entry.

### `koru plugin add <nuget-name>`
Downloads the NuGet package into the local plugin directory, validates the assembly implements `IPlugin`, and loads it. Supports additional NuGet feeds configured in global config.

### `koru plugin list`
List installed plugins.

### `koru plugin remove <name>`
Remove a plugin.

### `koru reset <artifact-path> [--plugin <name>]`
Given a copy-mode artifact that has drifted, re-copy the current registry version and update the checksum in the state database. If multiple plugin records exist and `--plugin` is not provided, prompt for disambiguation. Requires a clean registry working tree.

---

## Registry Layout

A registry is a git repository with this convention:

```
my-registry/
├── registry.yaml                 # Required. Plugin manifest.
├── core/
│   └── skills/
│       ├── foo.md                # Single-file skill
│       └── grill-me/             # Directory-shaped skill
│           ├── SKILL.md          # Marks the directory as one artifact
│           ├── AGENT-BRIEF.md    # Supplementary doc, belongs to "grill-me"
│           └── scripts/run.sh    # Any non-.md sibling, also belongs to "grill-me"
├── chimera/                      # Plugin namespace: chimera plugin claims everything here
│   └── modes/
├── claude/                       # Plugin namespace: claude plugin claims everything here
│   └── skills/
└── ...                           # Other plugin namespaces
```

Rules:
- The tool does **not** mandate frontmatter schemas. Artifact routing is based on path and plugin claims.
- The core owns `core/`. Everything else is claimed by plugins.
- A single plugin may claim multiple root namespaces.
- Two plugins may claim overlapping paths; both get to install (additive), but not to the *same destination path* (conflict).

### `registry.yaml` Schema

```yaml
name: acme-team
plugins:
  - name: chimera
    nuget: Acme.Cli.Plugin.Chimera
  - name: claude
    nuget: Acme.Cli.Plugin.Claude
```

---

## Plugin Interface

```csharp
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

public record Artifact(string Path, string RegistryRoot, bool IsDirectory = false);
```

`Artifact.Path` is forward-slash-relative to the registry root. For directory artifacts the path is the directory itself (no trailing slash, no `SKILL.md` suffix). For file artifacts it includes the `.md` extension. `Artifact.IsDirectory` tells the plugin whether to plan a file destination or a directory destination; both shapes return a single `InstallPlan` with a single `DestinationPath`.

the Koru CLI uses `PathClaims` for routing and `GetInstallPlan` for placement. For any matched artifact, every claiming plugin's `GetInstallPlan` is called. If two plugins return the same absolute destination path, sync errors out.

---

## State & Configuration

### Global Config
Stored at cross-platform config directory (e.g. `~/.config/koru/config.json` on Linux/macOS, `%APPDATA%/koru/config.json` on Windows), resolved via `Environment.SpecialFolder.ApplicationData`.

```json
{
  "defaultRegistry": "acme-team",
  "registries": [
    {
      "name": "acme-team",
      "remote": "https://github.com/acme/agent-registry",
      "path": "~/.koru/registries/acme-team"
    },
    {
      "name": "personal",
      "remote": "https://github.com/me/agent-registry",
      "path": "~/.koru/registries/personal"
    }
  ],
  "projects": [
    "/home/dev/Projects/acme-webapp",
    "/home/dev/Projects/api-service"
  ],
  "nugetFeeds": [
    "https://api.nuget.org/v3/index.json"
  ]
}
```

### Installation State Database
Stored per registry at `~/.koru/registries/<name>/state.json`. Never committed to git.

```json
{
  "installs": [
    {
      "sourcePath": "chimera/modes/review.md",
      "destinationPath": "/home/dev/Projects/acme-webapp/.chimera/modes/review.md",
      "installMode": "link",
      "plugin": "chimera",
      "sourceChecksum": "sha256:abc...",
      "installedChecksum": null
    },
    {
      "sourcePath": "core/skills/database-review.md",
      "destinationPath": "/home/dev/Projects/acme-webapp/.claude/skills/database-review.md",
      "installMode": "copy",
      "plugin": "core",
      "sourceChecksum": "sha256:def...",
      "installedChecksum": "sha256:def..."
    }
  ]
}
```

- `installedChecksum`: `null` for `link` mode.
- `sourceChecksum`: SHA-256 of the source at time of last install/sync. For directory artifacts the value is a tree hash with `sha256-tree:` prefix — SHA-256 over a sorted `(relpath\0filehash\n)` manifest. For file artifacts the prefix is `sha256:`.

### Drift Handling
For copy-mode artifacts, the sync pipeline is:

1. Compute checksum of the **installed** artifact. Use `ComputeSha256` for a file destination; use `ComputeSha256Tree` for a directory destination. Selection is based on `Directory.Exists(destinationPath)` or the `sha256-tree:` prefix on the recorded checksum.
2. If it differs from `installedChecksum` → **drift detected**. Abort sync for this artifact. Report: `"<path> has local modifications. Run 'koru reset <artifact>' to restore, or revert your changes."`
3. If it matches, compute checksum of the **registry source** (same file-vs-tree selection).
4. If source checksum differs from `sourceChecksum` → overwrite the installed artifact and update both checksums. For directory artifacts, the copy is atomic: stage to `<dest>.koru-<rand>/`, rename existing destination to a tombstone, `Directory.Move` the staged tree in, delete the tombstone.
5. If both match → no-op.

Link-mode artifacts skip drift detection entirely. The symlink target is always refreshed to point to the current working tree path. For directory artifacts, the symlink is a single `Directory.CreateSymbolicLink` at the destination (not per-file symlinks).

---

## Sync Algorithm

For each registry in config:

1. **Git Phase**
   - `git status --short`
   - If dirty:
     - Prompt: "Registry `<name>` has local changes. Abort and commit/push? Or proceed with local changes (for testing)?"
     - Abort → exit non-zero.
     - Proceed → skip pull, use working tree state.
   - If clean:
     - `git pull`.
     - If conflicts → present editor / resolution flow.

2. **Compute Desired State**
   - Walk **git-tracked** registry tree (exclude `.git/`, `state.json`, untracked files).
   - For each file, match against `PathClaims` of each loaded plugin.
   - For each matched plugin, call `GetInstallPlan(artifact, scope, mode)` for applicable scopes.
   - If two plugins produce the same absolute destination path → error naming both plugins.
   - Collect all `(sourcePath, destinationPath, mode, plugin, sourceChecksum)` tuples.

3. **Reconcile**
   - Read existing state database.
   - **Create**: tuples in desired not in state.
   - **Update**: tuples in both. For copy mode: run the drift-then-update pipeline. For link mode: update the symlink target.
   - **Remove**: tuples in state not in desired. Delete the destination file and database entry.
   - After changes, write new state database.

4. **Report**
   - Human-readable summary: created N, updated M, removed K, drift detected X.

---

## Plugin Ecosystem (V1)

### Built-in / Core
Handles `core/**` and any files not claimed by other plugins. Provides default placement rules.

### Chimera Plugin (NuGet example)
NuGet package name: `Koru.Plugin.Chimera`

```csharp
public class ChimeraPlugin : IPlugin
{
    public string Name => "chimera";

    public IEnumerable<string> PathClaims => new[] { "chimera/**" };

    public InstallPlan GetInstallPlan(Artifact artifact, Scope scope, InstallMode mode)
    {
        var relativePath = artifact.Path.Substring("chimera/".Length);
        var baseDir = scope == Scope.Global
            ? Path.Combine("~", ".chimera")
            : Path.Combine(".", ".chimera");
        return new InstallPlan(Path.Combine(baseDir, relativePath));
    }
}
```

---

## User Stories

### Team Onboarding
```bash
dotnet tool install -g Koru
koru use https://github.com/acme/agent-registry
# → clones to ~/.koru/registries/acme-team/
# → reads registry.yaml, installs chimera + claude plugins
# → initial sync (global modes to ~/.chimera/, etc.)
```

### Adding a Skill to the Team Registry
```bash
cd ~/.koru/registries/acme-team
echo "# Database Review Skill" > core/skills/database-review.md
git add . && git commit -m "Add database review skill" && git push
# Teammates run:
koru sync
```

### Installing a Skill in a Project
```bash
cd ~/Projects/acme-webapp
koru install core/skills/database-review
# → "Found in acme-team registry. Scope? [global/project-local]"
# → "Mode? [link/copy]"
# → Installed to ./.claude/skills/database-review.md
```

### Daily Sync
```bash
koru sync
# → Pulls acme-team registry
# → Updates ~/.chimera/modes/ (global)
# → Updates all tended projects (acme-webapp, api-service)
```

### Resolving Drift
```bash
koru sync
# ERROR: /home/dev/Projects/acme-webapp/.claude/skills/database-review.md has local modifications.
koru reset core/skills/database-review
# → Restores from registry
koru sync
# → Succeeds
```

---

## Decisions

| ID | Decision |
|---|---|
| ADR 0001 | Plugin claims are additive. A single artifact may be installed to multiple destinations by multiple plugins. Two plugins may not claim the same exact destination path. |
| ADR 0002 | Installation state database tracks every file placed by the Koru CLI, per registry. Enables safe cleanup, updates, and drift detection. |
| ADR 0003 | Sync leverages git. On dirty working tree, user is prompted to abort/commit/push or proceed with local changes. |
| ADR 0004 | An artifact is either a single `.md` file or a directory containing `SKILL.md`. Directory artifacts are installed as a tree; drift uses an aggregate `sha256-tree:` manifest hash. |

---

## Key Invariant

All managed artifacts are markdown — either a single `.md` file or a directory anchored by a `SKILL.md`. The Koru CLI does not interpret artifact frontmatter for routing — that belongs to the consuming harness. The one exception is `koru import`, which writes a top-level `source:` frontmatter block to record provenance; all other frontmatter keys are preserved untouched. Artifact identity and routing is determined by file system shape (registry directory layout + presence of `SKILL.md`), plugin path claims, and the installation state database.
