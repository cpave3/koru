## Context

The project is a greenfield .NET global CLI tool for managing team-shared agent configurations. the Koru CLI treats a git repository as a "registry" of markdown artifacts (skills, agents, harness configs). Plugins teach the Koru CLI how to place those artifacts on the filesystem.

Key constraints:
- the Koru CLI does not interpret artifact frontmatter — it is content-agnostic.
- Artifact routing is driven by registry directory structure + plugin path claims.
- The tool must support multiple registries (work, personal) and be shareable across a team.
- The user is building this in .NET primarily for learning, so the plugin system will use compiled assemblies and reflection.

## Goals

1. Provide a git-backed registry for agent skills/configs that multiple developers can sync from.
2. Support multiple registries per user with global and project-local installation scopes.
3. Enable a plugin architecture where NuGet packages teach the Koru CLI about new harness-specific artifact types and placement rules.
4. Make `koru sync` the one command that pulls updates and deterministically installs everything.
5. Detect local drift in copy-mode installations and fail fast with a clear remediation path.

## Non-Goals

1. the Koru CLI will not scaffold or generate artifact boilerplate (e.g. no `koru add skill` command).
2. the Koru CLI will not act as a package manager with version resolution, dependency graphs, or lockfiles.
3. the Koru CLI will not transform file formats (plugins do not convert markdown into JSON, etc.).
4. No built-in plugin registry or marketplace — plugins are referenced by NuGet package name/feed.
5. No built-in plugin registry or marketplace.

## Decisions

### Additive Plugin Claims
When two plugins claim the same registry path, both participate. The same artifact may be installed to multiple destinations. This favors flexibility over out-of-the-box simplicity. Plugin authors must register narrow claims. (ADR 0001)

### Installation State Database
Each registry maintains a local `state.json` that records every file the Koru CLI has placed: source path, destination path, install mode, plugin name, and content checksum. This enables correct garbage collection of removed artifacts and drift detection for copy-mode files. (ADR 0002)

### Sync Leverages Git
the Koru CLI inspects the git working tree before pulling. If dirty, the user is prompted to abort (commit/push first) or proceed with local changes. This aligns the workflow tightly with git and reinforces the mental model that the registry repo is the source of truth. (ADR 0003)

### Registry Manifest for Plugins
The `registry.yaml` at the registry root lists required plugins. When a user links a registry, the Koru CLI reads this file and reports missing plugins. The user installs them with `koru plugin add <nuget-name>`.

### Link vs. Copy as Install-Time Decision
When installing an artifact to a project, the user chooses link (symlink to registry) or copy (snapshot). The installation mode is recorded in the state database. On sync, link-mode artifacts are always refreshed from the working tree. For copy-mode artifacts, sync first checks the installed file's checksum against the recorded checksum (drift detection). If no drift, it compares the registry source checksum to the recorded source checksum; if changed, it overwrites the installed file.

### Global Configuration Location
User-level config lives at `~/.config/koru/config.json` (cross-platform via `Environment.SpecialFolder.ApplicationData`). It stores linked registries and the list of "tended" project directories.

### Plugin Loading via Assembly Reflection
Plugins are .NET assemblies loaded from a local `~/.koru/plugins/` directory. the Koru CLI scans for types implementing `IPlugin`, validates the interface contract, and registers them. Invalid assemblies are logged and ignored.

## Risks / Trade-offs

- **Plugin ecosystem complexity**: Compiled plugins require NuGet distribution and assembly loading. If no one writes plugins beyond the first author, this is heavy for the benefit. → Mitigation: keep the interface minimal and well-documented.
- **Overlapping claims causing surprise installs**: A broadly-claiming plugin could install artifacts everywhere. → Mitigation: sync output must clearly list every file placed and which plugin claimed it.
- **Synchronizing many tended projects is slow**: `koru sync` walks every tended project. A developer with 50+ projects may see latency. → Mitigation: future optimization with selective sync flags.
- **Drift detection noise**: Copy-mode files in project directories may legitimately be templates that users are expected to edit. Failing sync may be too strict. → Mitigation: start strict; add opt-out per-plugin later if needed.

## Open Questions

1. What is the migration path when a plugin changes its placement rules between versions?
2. Should there be a way to opt out of drift detection per-plugin or per-file?
