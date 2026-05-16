# Domain Glossary

## Skill
A reusable, domain-specific knowledge module. May be stored either as a single markdown file (`core/skills/foo.md`) or as a directory containing a `SKILL.md` plus supplementary files (`core/skills/foo/SKILL.md`, `core/skills/foo/AGENT-BRIEF.md`, `core/skills/foo/scripts/run.sh`). Both shapes are first-class artifacts.

## Artifact Shape
A single tracked unit in a registry. Either a single `.md` file or a directory containing `SKILL.md`. When `SKILL.md` is present in a directory, that directory becomes the artifact and every file under it belongs to that artifact (see ADR 0004). All other glossary items below — Skill, Agent, Config, Drift, etc. — apply to both shapes.

## SKILL.md Convention
A directory containing a file literally named `SKILL.md` is treated as a single directory-shaped artifact. The directory's basename is the artifact's name. The convention is borrowed from the broader agent-skills ecosystem (e.g. `mattpocock/skills`, `vercel-labs/agent-skills`) so Koru can host content from those sources unchanged.

## Agent
A named, versioned configuration profile that assembles resources (skills, model settings, harness parameters) into a runnable definition. Agents are markdown files that declare *what* to run and *which* context to load.

## Config / Markdown File
Any other discrete markdown artifact governed by placement rules. Examples include Chimera mode definitions, harness manifests, or environment templates. These are unified under the same CLI because they share the same storage, versioning, and distribution concerns.

## Registry
A git repository that stores skills, agents, and config files under a standardized directory structure. A user may have multiple registries (e.g. one for work, one for personal). Each registry is linked to a remote URL and has a local working tree. Koru resolves artifacts against any linked registry. The registry root contains a manifest that lists the plugins required to interpret its contents.

## Registry Manifest
A configuration file at the registry root (e.g. `registry.yaml`) that declares which plugins are required to resolve artifacts in that registry. During `koru init` or `koru use`, the Koru CLI reads the manifest. During `koru use`, missing plugins are reported to the user for manual installation via `koru plugin add`.

## Installation Mode
When placing an artifact into a project, Koru supports two strategies: **link** (symlink to the registry working tree) and **copy** (write a standalone snapshot into the project). Updates must detect the original mode and act accordingly.

## Scope
When placing an artifact, the target location may be **global** (e.g. `~/.chimera/modes/`) or **project-local** (e.g. `.chimera/modes/`). Scope is independent of Installation Mode.

## Plugin
A compiled .NET assembly distributed as a NuGet package. A plugin implements a standard interface (`IPlugin`) with `PathClaims` (glob patterns like `chimera/**`) and `GetInstallPlan` for placement rules. Plugins are installed into a local plugin directory via `koru plugin add <nuget name>` and validated at load time. Overlapping plugin claims are additive. The core owns git operations, registry resolution, and the install/update/remove lifecycle.

## Sync
The primary CLI workflow: reads the Koru CLI's global configuration to find all registered registries and all "tended" directories (global and project-local scopes). For each registry, it pulls changes from the remote and resolves merge conflicts. Then it deterministically installs all managed artifacts to every tended destination based on active plugins, scope, and installation mode. A `--global-only` or `--project <path>` flag can restrict the scope of a sync. Before pulling, if the working tree is dirty, the user is prompted to abort and commit/push, or proceed with local changes.

## Drift Detection
Koru tracks the checksum of every installed copy-mode artifact. If an installed file is modified independently of the registry, `koru sync` detects the mismatch and fails with a clear message. The user may run `koru reset <artifact>` to restore the registry version. For directory artifacts, the checksum is a tree-hash (`sha256-tree:`) over a sorted manifest of every file's `(relpath, sha256)` pair — modifying any file in the tree counts as drift.

## Import
Copy a skill from an external git repo into a local registry, with provenance metadata recorded in a YAML frontmatter `source:` block (repo, path, ref, commit, timestamp). Once imported, the artifact behaves like any other Koru asset — install, sync, and reset all work normally. The originating repo is no longer queried after import; refreshing requires a re-run of `koru import --force`. See `koru import` in SPEC.md.

## Provenance Frontmatter
A YAML frontmatter block on an imported artifact's `SKILL.md` (or single `.md` file) recording where it came from. Top-level key is `source:` with sub-keys `repo`, `path`, `ref`, `commit`, `imported_at`. Other frontmatter keys present in the source are preserved. Koru does not interpret any other frontmatter — `source:` is the one exception, and only `koru import` writes it.

## Key Invariant
All managed artifacts are markdown files at rest. Koru does not dictate or interpret artifact frontmatter — that belongs to the consuming harness. Artifact identity and routing is determined by file system shape (registry directory layout), plugin path claims, and the installation state database.
