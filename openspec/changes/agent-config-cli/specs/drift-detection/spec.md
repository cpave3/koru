## ADDED Requirements

### Requirement: Copy-mode drift is detected during sync
The Koru CLI SHALL detect when a copy-mode installed artifact has been modified independently of the registry.

#### Scenario: Local modification detected
- **GIVEN** an artifact was installed with copy mode
- **AND GIVEN** the user modified the installed copy
- **WHEN** the user runs `koru sync`
- **THEN** the Koru CLI reports a drift error for that artifact
- **THEN** the Koru CLI prompts the user to run `koru reset <artifact>`
- **THEN** sync aborts without modifying the drifted file

### Requirement: Reset restores registry version
The Koru CLI SHALL provide a command to restore a copy-mode artifact to its registry version.

#### Scenario: Resetting a drifted artifact
- **GIVEN** an artifact has drifted from its registry version
- **WHEN** the user runs `koru reset <artifact>`
- **THEN** the installed copy is overwritten with the current registry version
- **THEN** the installation database checksum is updated

### Requirement: Link-mode artifacts do not drift
The Koru CLI SHALL not perform drift detection on symlinked artifacts.

#### Scenario: Symlink modified
- **GIVEN** an artifact was installed with link mode
- **AND GIVEN** the symlink target was modified in the registry
- **WHEN** the user runs `koru sync`
- **THEN** the symlink is transparently updated to point to the new location if needed
- **THEN** no drift error is reported

### Requirement: Installation database tracks both source and installed checksums
The Koru CLI SHALL record the SHA-256 checksum of both the registry source file and the installed copy at install time.

#### Scenario: Checksum recorded on install
- **WHEN** an artifact is installed with copy mode
- **THEN** the installation database stores `sourceChecksum` (registry file hash at install time)
- **THEN** the installation database stores `installedChecksum` (installed file hash at install time)
- **THEN** on subsequent syncs, the Koru CLI compares the installed file's hash against `installedChecksum` (drift detection)
- **THEN** if no drift, the Koru CLI compares the registry source hash against `sourceChecksum` (update detection)

### Requirement: Directory artifacts use aggregate tree checksums
The Koru CLI SHALL detect drift on directory artifacts by hashing every file in the tree and aggregating the result, so that a modification to *any* file under a directory artifact's destination is reported as drift.

#### Scenario: Tree checksum manifest
- **WHEN** the Koru CLI computes a checksum for a directory destination
- **THEN** it enumerates every file under the directory recursively
- **THEN** it produces a sorted manifest of `<relative-path>\0<sha256-of-file>\n` lines
- **THEN** the recorded checksum is the SHA-256 of that manifest, with a `sha256-tree:` prefix

#### Scenario: Drift detected when any file in the tree changes
- **GIVEN** a directory artifact was installed with copy mode and its tree checksum recorded
- **WHEN** the user modifies a sibling file inside the destination (not `SKILL.md` itself)
- **AND** the user runs `koru sync`
- **THEN** the Koru CLI reports drift for that artifact
- **THEN** sync aborts without overwriting the directory

#### Scenario: Detector branches on file vs directory
- **WHEN** the Koru CLI evaluates a drift record
- **THEN** it uses `ComputeSha256Tree` if either the live destination is a directory or the recorded checksum has the `sha256-tree:` prefix
- **THEN** otherwise it uses `ComputeSha256` for a single file
