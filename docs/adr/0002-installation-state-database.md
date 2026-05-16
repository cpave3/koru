# ADR 0002: Installation State Database

## Status
Accepted

## Context
The primary CLI workflow is `koru sync`, which pulls the latest registry state and deterministically installs artifacts to their destinations. For sync to be correct and idempotent, the Koru CLI must know:
- Which files it previously placed
- Where each file came from in the registry
- Whether the installation was a link or a copy
- Which plugin claimed the artifact

Without stored state, the Koru CLI cannot reliably remove artifacts that have been deleted from the registry, nor can it distinguish its own files from user-created files in the same directories.

## Decision
the Koru CLI maintains an **installation database** per registry at `~/.koru/registries/<name>/state.json` (or equivalent). Each entry records:

- `sourcePath`: path within the registry working tree
- `destinationPath`: absolute path on disk
- `installMode`: `link` or `copy`
- `plugin`: name of the plugin that claimed the artifact
- `sourceChecksum`: hash of the registry source file at time of last install/sync
- `installedChecksum`: hash of the installed file at time of last install/sync (null for link mode)

On every `sync`:
1. Compute the desired state by walking the **git-tracked** registry tree and asking each active plugin where its claimed artifacts should go.
2. Compare desired state to the stored installation database.
3. For copy-mode updates: check drift via `installedChecksum`, then check upstream changes via `sourceChecksum`.
4. Create, update, or remove files to converge to the desired state.
5. Write the updated database.

## Consequences

- Abandoned and renamed artifacts are correctly cleaned up.
- Sync is deterministic and auditable; the database is a clear record of what the Koru CLI owns.
- The database is a local-only file and must never be committed to the registry repo (it should be ignored).
- Drift detection requires a strategy for when a copied file has been modified locally. This is left to a follow-up decision.
