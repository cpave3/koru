## Context

Koru imports a skill from an external git repo by cloning the source, copying the artifact, and stamping a `source:` YAML frontmatter block onto the imported `SKILL.md` (or single `.md`) recording `repo`, `path`, `ref`, `commit`, and `imported_at`. That frontmatter is the only durable record of provenance — `state.json` does not track import lineage, and the upstream URL is never queried again after the initial import. The user can `grep` for `source:` blocks and re-run `koru import --force` to refresh, but neither operation is ergonomic at scale.

This change adds a check command (read-only audit of imported artifacts vs. their upstream `HEAD`) and an update command (refresh one or all imported artifacts) on top of the existing provenance frontmatter. No schema changes; everything builds on the data we already write.

Stakeholders: any user maintaining a personal or team registry that imports skills from third-party repos like `mattpocock/skills`.

## Goals / Non-Goals

**Goals:**
- Make it possible to ask "which of my imported skills have newer upstream versions?" in one command, without manual grepping.
- Make refreshing a known imported skill a single command that does not require remembering the upstream URL or path.
- Refresh many skills at once with sensible safety: skip drifted ones, prompt before overwriting unless `--yes`.
- Reuse the existing `koru import` machinery and the existing `source:` frontmatter convention. No new on-disk schema.

**Non-Goals:**
- Live "pull at install time" references (the previously-deferred ref artifact idea). Imported skills remain copies; the source block is provenance metadata, not a live binding.
- Pinning to a specific ref or sha (every import refreshes to the upstream `HEAD` of whatever branch the source commit lived on). A future change could add `--pin` / `--unpin`.
- Multi-source artifacts (an artifact with multiple `source:` entries). One artifact, one provenance block.
- Watching upstream for changes (no daemons, no webhooks).
- Bringing skills from non-git sources (no HTTP, no archive URLs).

## Decisions

### Provenance lives in the artifact's frontmatter, not `state.json`
We continue treating the imported `SKILL.md` (or single `.md`) as the durable record. `state.json` is a per-registry installation database — it tracks files placed by Koru on destinations, not provenance of files inside the registry working tree.

**Alternatives considered:**
- A separate `imports.json` per registry. Rejected: duplicates information and can drift out of sync with the frontmatter; the frontmatter has the virtue of travelling with the file when it's committed/pushed.
- Storing provenance in `registry.yaml`. Rejected: same drift concern; also pollutes a file whose role is plugin manifest, not artifact ledger.

### Discovery via `IImportedArtifactScanner`
A new abstraction enumerates every artifact in a registry whose frontmatter has a top-level `source:` block. Implementation walks `ArtifactDiscovery`-produced artifacts, reads the file (SKILL.md for directory, the .md itself for file artifacts), and uses the existing `Frontmatter` helper. Returns a record per imported artifact: `{ArtifactPath, IsDirectory, Source}` where `Source` is a strongly-typed `SourceBlock` (already defined in `Core/Util/Frontmatter.cs`).

**Rationale:** Reuses the directory/file shape rules already settled in ADR 0004. Centralises the "is this an imported artifact?" check so both `--check` and `update` see the same set.

### Upstream HEAD lookup uses `git ls-remote`, not a full clone
For `--check`, we do not need the repo contents — only the current sha at the remote's `HEAD` (or at the `ref` recorded in the source block). LibGit2Sharp exposes `Repository.ListRemoteReferences(url)`; we wrap that as `IGitOps.GetRemoteHeadSha(string url, string ref)`. Returns the sha or throws on auth/network failure.

**Alternatives considered:**
- Shallow clone per check. Rejected: hundreds of MB across many imports, slow, redundant if user only wants a status report.
- HTTP request to GitHub API. Rejected: locks the implementation to GitHub; `git ls-remote` works for any git host (GitLab, self-hosted, SSH, file:// for tests).

### `update <name>` re-uses `koru import` machinery via a shared helper
The actual mutate-the-registry path stays in one place. We extract the body of `ImportCommand.Execute` past the URL/subpath resolution into a `Importer.ImportInto(registry, sourceUrl, subpath, localName, force)` method on a new `Core/Install/Importer.cs` service. `ImportCommand` becomes a thin wrapper that parses CLI args; `UpdateCommand` becomes a thin wrapper that reads the source block from an existing artifact and calls the same helper with `force: true`.

**Rationale:** Keeps the file-copy + frontmatter-stamp + git-commit code in one path. Future imports gain features (e.g. pinning) without forking the update path.

### `update` (no args) iterates only imported artifacts that are behind
We don't refresh every imported artifact unconditionally — that re-clones every upstream repo for nothing if HEAD hasn't moved. Instead `update` runs the check pipeline first, then iterates only the artifacts whose recorded `commit` differs from `GetRemoteHeadSha`. For each, it prompts `Refresh <name> ([behind by N] {old-sha} → {new-sha})? [Y/n]` unless `--yes`.

**Rationale:** Avoids unnecessary clones. Aligns with user expectation that "no news = nothing to do."

### Drift in the registry copy aborts that artifact's update
If the registry's working tree has uncommitted changes touching an imported artifact, refreshing it would either lose those edits or produce a messy git state. Skip with a clear message — the user can commit/discard and re-run. `--force` does NOT override this; force is for "overwrite the destination" and was already part of import semantics, not for clobbering user edits in the registry.

**Alternatives considered:**
- Stash + restore. Rejected: surprising side-effect; the user might not realise their edits became a stash entry.
- Three-way merge against the upstream. Rejected: scope creep; let git's normal workflow handle conflicts after the user commits their edits.

### `--check` prints a compact table, returns non-zero if any are behind
Designed for CI use: a script can run `koru import --check --registry <reg>` and check the exit code to decide whether to open a PR. The table has columns `Artifact | Source | Status` where Status is one of `up-to-date`, `behind ({old}→{new})`, `unreachable: <reason>`.

### Networking failures during `--check` are non-fatal
If one upstream is unreachable, the row reports `unreachable: <reason>` and the loop continues. The overall exit code is `2` to distinguish from `1` (some behind, all reachable) and `0` (all up-to-date).

## Risks / Trade-offs

- **Many imports × slow `git ls-remote`** → check time scales linearly with imported-artifact count. Mitigation: dedupe by `repo` URL across imports (a repo with 10 imported skills triggers one `ls-remote`, not 10).
- **`source.ref: HEAD` is ambiguous** if the upstream re-points HEAD between import and check. Today's `koru import` writes `ref: "HEAD"` literally. We will preserve that wording but resolve it via `ls-remote --symref` to follow the current HEAD; for a future `--pin` flag we'd record the actual branch name (e.g. `main`).
- **Auth-walled private repos** require credentials LibGit2Sharp can resolve (SSH agent, HTTPS keychain). Failure shows up as `unreachable`. → Document in CLI help; same as `koru use` today.
- **State.json doesn't get touched** by an update — only the registry working tree changes. The next `koru sync` will see the new source content and update destinations through the normal drift-then-update pipeline. This is a feature, not a bug, but it means an update doesn't immediately propagate to installed copies; the user runs `koru sync` afterwards (or both commands chained). Surface in help text.
- **Renamed/deleted upstream paths** produce `unreachable: source path not found in latest upstream` on update. We don't try to "follow" renames. The user can re-import manually under a new path.

## Migration Plan

No data migration. Existing imported artifacts already carry the `source:` block; they immediately become discoverable by the new commands.

Rollback: revert the commit; no on-disk schema changes to undo.

## Open Questions

1. Should `update` refuse if `koru sync` reports drift on a destination installed copy of the artifact? I.e., should we look beyond the registry working tree to the actual installed copies? Current proposal: no — that's `koru sync`'s job, run it afterwards. Re-evaluate if users hit confusion.
2. For artifacts imported from a non-HEAD ref in the future, how do we choose what to "check" against? Probably: always check the recorded `ref` (treat HEAD as today's degenerate case). Defer until pinning lands.
