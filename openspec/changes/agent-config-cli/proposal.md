## Why

Developer teams need a shared, version-controlled source of truth for agent configurations, skills, and harness-specific config files. Today these are scattered in local dotfiles, copy-pasted between projects, or maintained in ad-hoc git repos with no standard tooling. A purpose-built CLI backed by git and extensible via plugins solves the distribution, update, and consistency problem.

## What Changes

- A new .NET global tool named Koru for managing agent configurations and skills.
- **Registry**: A git repo that stores all artifacts (skills, agents, config files) as markdown. the Koru CLI creates, links, and syncs registries.
- **Plugin architecture**: Plugins are compiled .NET assemblies distributed via NuGet. They teach the Koru CLI about artifact types and placement rules for specific harnesses (e.g. Chimera, Claude).
- **Sync workflow**: The primary command `koru sync` pulls registry changes and deterministically installs artifacts to global and project-local destinations, respecting link vs. copy mode and drift detection.
- **State tracking**: An installation database per registry records every placed file, its source, mode, and checksum. Enables correct updates and garbage collection.

## Capabilities

### New Capabilities

- `registry-management`: Create, link, clone, and manage git-backed registries. Multiple registries (work, personal) are supported.
- `plugin-system`: Install NuGet-based plugins that declare artifact types and placement rules. Overlapping plugin claims are additive.
- `sync-engine`: Compute desired state from registry + plugins, diff against installation database, and converge the filesystem. Handle git pull, dirty working tree prompts, and merge conflicts.
- `project-install`: Install artifacts from a registry into a project directory (link or copy, global or project-local scope). Disambiguate when an artifact exists in multiple registries.
- `drift-detection`: Detect when installed copy-mode artifacts have been modified locally. Fail sync with clear remediation (`koru reset`).

### Modified Capabilities

- (none)

## Impact

- New .NET global tool to build, test, and distribute.
- Plugin authors need a public `IPlugin` interface contract.
- Team members adopt a new workflow: edit in the registry repo, commit, push, then `koru sync` everywhere.
