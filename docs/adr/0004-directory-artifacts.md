# ADR 0004: Directory Artifacts (SKILL.md folders)

## Status
Accepted

## Context
Koru's original artifact model was "one markdown file = one artifact". This is sufficient for simple skills consisting of a single body of prose, but it cannot represent the broader agent-skills ecosystem convention where a skill is a *directory* containing:

- A primary `SKILL.md` describing the skill.
- Supplementary docs (`AGENT-BRIEF.md`, `OUT-OF-SCOPE.md`).
- Scripts, fixtures, and other supporting files (`scripts/run.sh`, `templates/`).

Tools like `npx skills add` and registries like `skills.sh` index repos that exclusively use this directory shape. Without first-class support, Koru users had to flatten a directory-shaped skill into a single file (losing the supplementary artefacts) or vendor it manually as a tree outside the registry's model (losing drift detection and reset).

A previous shorthand discussed in design was to keep the single-file model and let plugins materialise an entire directory tree from a manifest file. That preserves the data model but pushes complexity onto every plugin author and obscures the user's mental picture of "what is in the registry". We rejected it for the simpler approach below.

## Decision
An artifact in a Koru registry is now either:

- **A single `.md` file** (e.g. `core/skills/foo.md`) — the existing shape, unchanged.
- **A directory containing `SKILL.md`** (e.g. `core/skills/grill-me/`) — every file under that directory belongs to the same artifact. The directory itself is the artifact identity (`Artifact.Path = "core/skills/grill-me"`, `Artifact.IsDirectory = true`).

Discovery rules:

1. When walking a registry's git-tracked files, any directory containing a `SKILL.md` becomes a directory-artifact.
2. Every tracked file under that directory belongs to that artifact (not to itself).
3. A tracked `.md` file *not* inside any `SKILL.md`-containing directory is a single-file artifact.
4. Non-`.md` files outside any `SKILL.md` directory are ignored.
5. Most-specific-match wins: if both `foo/SKILL.md` and `foo/bar/SKILL.md` exist, files under `foo/bar/` belong to `bar`; other files under `foo/` belong to `foo`.

`ArtifactDiscovery` (in `Core/Util/`) is the single implementation of these rules. The resolver, the sync planner, and the import command all funnel through it.

Plugin behaviour:

- `Artifact.IsDirectory` is exposed to plugin authors via the existing `IPlugin.GetInstallPlan` signature. A plugin can return one `InstallPlan` whose destination is either a file path or a directory path; Koru installs accordingly.
- The built-in `CorePlugin` produces `<scope>/.claude/skills/<name>.md` for file artifacts and `<scope>/.claude/skills/<name>/` for directory artifacts.

Filesystem behaviour:

- **Copy mode** copies the entire tree atomically: stage to a sibling `*.koru-<rand>/`, rename existing destination to a tombstone (if any), then `Directory.Move` the staged tree in. Tombstone is cleaned up afterwards. Existing single-file copy uses the same temp-rename pattern via `AtomicFile.Copy`.
- **Link mode** creates one symlink at the destination pointing at the registry directory. Single symlink, not symlink-per-file — atomic on update and less filesystem metadata.

Drift detection for directory artifacts:

- The `installedChecksum` / `sourceChecksum` fields in `state.json` carry a `sha256-tree:` prefix for directory artifacts. The hash is SHA-256 over a sorted manifest of every file's `(relpath, sha256)` pair. Modifying any file in the tree changes the manifest hash, which surfaces as drift exactly the way file-level drift does today.
- `DriftDetector` branches on either the live filesystem (`Directory.Exists(source)`) or the recorded checksum prefix (`sha256-tree:`) to decide which hash function to use.

## Consequences

- The agent-skills convention (`SKILL.md` + siblings) is supported natively. Users can import skills from `mattpocock/skills`, `vercel-labs/agent-skills`, and similar repos without flattening or losing supplementary files.
- Existing single-file artifacts continue to work without change. The two shapes coexist in the same registry.
- Sync output prints one line per artifact, not per file in a directory — consistent with the "one artifact, one entry" mental model.
- A `state.json` record stores one entry per artifact regardless of how many files are in it. Schema is unchanged; the meaning of the checksum fields is generalised.
- Symlinking a whole directory means edits made on disk to the linked tree propagate to the registry working tree. This matches single-file link mode's semantics.
- Plugin authors writing new plugins need to think about whether their plugin claims directory artifacts; for most plugins targeting `<namespace>/**`, the existing claim still works because `ArtifactDiscovery` hides the per-file detail.
- Nested `SKILL.md` directories are supported but uncommon. The longest-prefix rule means a deeper `SKILL.md` carves a sub-artifact out of its parent.
