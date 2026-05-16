## Why

`koru import` records where every imported skill came from in a `source:` frontmatter block, but there is no way for the user to find out which imports have new versions upstream. They must currently inspect each `source.commit` by hand and compare against the upstream `HEAD`. Once a user imports more than a handful of skills, this is unmanageable, and stale skills accumulate silently. Adding a check command + an explicit update flow closes the loop on the import workflow.

## What Changes

- New command `koru import --check` (no other args, optional `--registry <name>`) walks every artifact in the registry whose frontmatter has a `source:` block, fetches each upstream repo's `HEAD`, and prints a status table (up-to-date / behind by N commits / unreachable). No mutations.
- New command `koru update <artifact-name> [--registry <name>] [--force] [--yes]` re-imports a single named imported skill against its recorded `source.repo`/`source.path`. Equivalent to `koru import <recorded-repo> <recorded-path> --name <artifact-name> --force`, but resolves the URL/path from the registry copy so the user doesn't have to remember them.
- New command `koru update` with no args refreshes every imported artifact whose upstream `HEAD` differs from its recorded `source.commit`. Skips artifacts whose registry working tree has local edits (drift); reports them with a remediation hint. `--yes` bypasses the per-artifact confirmation prompt.
- A small new abstraction `IImportRegistry` (or similar) for discovering "imported artifacts" — walks the registry working tree, parses frontmatter, returns `(artifact_path, source_block)` tuples. Used by both `--check` and `update`.

## Capabilities

### New Capabilities
- `imported-skill-tracking`: Discover artifacts that originated from `koru import` (carry a `source:` frontmatter block); check the upstream `HEAD` for each; update one or all of them, respecting drift and registry working-tree cleanliness.

### Modified Capabilities
- (none — `project-install` already documents the import flow; this change layers commands on top without changing the import contract itself)

## Impact

- New code in `Core/Util/` for frontmatter-driven artifact enumeration (reuses the existing `Frontmatter` helper).
- New `Commands/Update/` namespace with the two update commands; `ImportCommand` gains a `--check` switch.
- No schema changes to `state.json` or `registry.yaml`. Provenance continues to live in the artifact's own frontmatter, which is the only durable record.
- `IGitOps` may need a `string GetRemoteHeadSha(string remoteUrl, string ref)` to query the upstream without a full clone; alternatively, the implementation uses a shallow clone to a temp dir per upstream URL. Decision deferred to design.md.
- Adds an integration test that spins up two fake source repos, imports, then runs `--check` / `update` and asserts the recorded `source.commit` advances.
