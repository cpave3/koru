# ADR 0001: Additive Plugin Claims

## Status
Accepted

## Context
A single artifact in the registry may be claimed by more than one plugin.
For example, a file under `chimera/modes/review.md` might be claimed by:

- The `chimera` plugin, which wants it installed at `.chimera/modes/review.md`.
- A hypothetical `backup-sync` plugin, which wants it installed at `.backups/chimera-modes/review.md`.

We needed to decide whether overlapping claims are additive, disambiguated, or treated as errors.

## Decision
Overlapping plugin claims are **additive**.

When a user installs an artifact, every plugin that has registered interest in that artifact's registry path receives the install request and may specify a destination. the Koru CLI installs the artifact to ALL destinations returned by claiming plugins.

**Exception:** If two plugins return the same absolute destination path for the same artifact, this is a **conflict**. Sync errors out naming both plugins.

The user MAY add a `--plugin <name>` filter to scope an install to a single plugin.
Without a filter, all matching plugins participate.

## Consequences

- A user might see the same file placed in multiple locations without realizing why.
- Plugin authors must be careful not to register overly broad claims (e.g. claiming `**/*.md`) because it would cause every artifact to be installed everywhere.
- the Koru CLI must clearly report which plugin claimed which file and where it was placed, so the output is auditable.
- This design favors flexibility over out-of-the-box simplicity. A team installing many plugins must be intentional about what each plugin captures.
- Collisions on exact destination paths produce explicit errors, preventing silent overwrites.
