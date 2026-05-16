## ADDED Requirements

### Requirement: Sync pulls remote changes
The Koru CLI SHALL pull the latest changes from the registry's remote before installing artifacts.

#### Scenario: Clean working tree
- **GIVEN** the registry working tree has no uncommitted changes
- **WHEN** the user runs `koru sync`
- **THEN** the Koru CLI pulls the latest changes from the remote
- **THEN** artifacts are installed based on the updated working tree

### Requirement: Sync prompts on dirty working tree
The Koru CLI SHALL prompt the user when the registry working tree has uncommitted changes.

#### Scenario: Dirty working tree
- **GIVEN** the registry working tree has local modifications
- **WHEN** the user runs `koru sync`
- **THEN** the Koru CLI prompts: "Registry has local changes. Abort and commit/push? Or proceed with local changes (for testing)?"
- **THEN** if the user chooses abort, the command exits
- **THEN** if the user chooses proceed, sync continues without pulling

### Requirement: Sync computes desired state from registry and plugins
The Koru CLI SHALL walk the registry and ask each active plugin where its claimed artifacts should be installed.

#### Scenario: Computing desired state
- **GIVEN** a registry contains `core/skills/review.md` and `chimera/modes/debug.md`
- **AND GIVEN** a plugin claims `chimera/**` and wants files in `.chimera/`
- **WHEN** sync computes desired state
- **THEN** it produces an install plan for `core/skills/review.md` and `.chimera/modes/debug.md`

### Requirement: Sync converges filesystem to desired state
The Koru CLI SHALL compare the desired state to the installation database and create, update, or remove files to match.

#### Scenario: New artifact added to registry
- **GIVEN** the installation database does not contain `core/skills/new-skill.md`
- **AND GIVEN** the registry now contains `core/skills/new-skill.md`
- **WHEN** the user runs `koru sync`
- **THEN** the file is installed according to the active plugin rules
- **THEN** the installation database is updated

#### Scenario: Artifact removed from registry
- **GIVEN** the installation database contains `core/skills/old-skill.md`
- **AND GIVEN** the registry no longer contains that file
- **WHEN** the user runs `koru sync`
- **THEN** the installed file is removed from disk
- **THEN** the installation database entry is removed

### Requirement: Sync handles both global and project-local scope
The Koru CLI SHALL install artifacts to global directories and to all "tended" project directories during sync.

#### Scenario: Sync with tended projects
- **GIVEN** the Koru CLI config lists `/home/dev/project-a` and `/home/dev/project-b` as tended
- **AND GIVEN** a plugin places `chimera/modes/review.md` at project-local scope
- **WHEN** the user runs `koru sync`
- **THEN** the file is installed to both `/home/dev/project-a/.chimera/modes/review.md` and `/home/dev/project-b/.chimera/modes/review.md`

### Requirement: Sync respects installation mode
The Koru CLI SHALL create symlinks for link-mode installations and copy files for copy-mode installations.

#### Scenario: Link mode installation
- **GIVEN** an artifact was previously installed with link mode
- **WHEN** the user runs `koru sync`
- **THEN** the symlink is updated if the source changed
- **THEN** the symlink target remains the registry working tree path

#### Scenario: Copy mode installation
- **GIVEN** an artifact was previously installed with copy mode
- **AND GIVEN** the registry file has changed
- **WHEN** the user runs `koru sync`
- **THEN** the Koru CLI checks drift: compare installed file checksum to recorded `installedChecksum`
  - **THEN** if drifted (checksums differ), abort sync for this artifact with clear error
  - **THEN** if not drifted, compare registry source checksum to recorded `sourceChecksum`
  - **THEN** if registry changed, overwrite destination with new content and update checksums
