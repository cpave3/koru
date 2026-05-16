# ADR 0003: Sync Leverages Git

## Status
Accepted

## Context
`koru sync` is the primary workflow for pulling registry updates and installing artifacts. The user edits artifacts inside the local registry working tree (e.g. `~/.koru/registries/<name>/`), and those edits are real git-tracked changes. We needed a policy for how sync interacts with an uncommitted working tree.

## Decision
Before installing artifacts during `sync`, the Koru CLI checks the git status of the registry working tree.

If the working tree is dirty (uncommitted changes exist):

1. the Koru CLI prompts the user:  
   "Registry has local changes. Abort and commit/push? Or proceed with local changes (for testing)?"

2. If the user chooses **abort**, the sync command exits immediately. The user then commits and pushes their changes through normal git workflow before re-running sync.

3. If the user chooses **proceed**, the sync command skips the git pull and installs directly from the current working tree state. The local changes win; the remote is not consulted. This is explicitly for testing local edits without pushing them.

If the working tree is clean, sync proceeds normally: pull from the remote, resolve merge conflicts if necessary, then install.

## Consequences

- The team is trained to treat the registry working tree as the source of truth. Edits happen in the repo, not in installed copies.
- Copy-mode drift in installed copies is still possible, but is a secondary concern. The primary drift protection is that the registry working tree must be clean before pulling updates.
- Sync is not fully headless; it may block on a TTY prompt for dirty working trees. A `--yes` or `--no-interactive` flag may be needed for CI/automation later.
- This design aligns sync tightly with git rather than abstracting it away into a separate package manager style.
