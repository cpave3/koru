## ADDED Requirements

### Requirement: User can install an artifact into a project
The Koru CLI SHALL allow a user to install a named artifact from any registered registry into the current project directory.

#### Scenario: Installing an artifact by path
- **GIVEN** the user is in a project directory
- **AND GIVEN** a registry contains an artifact at `core/skills/review.md`
- **WHEN** the user runs `koru install core/skills/review.md`
- **THEN** the Koru CLI prompts for scope (global or project-local)
- **THEN** the Koru CLI prompts for installation mode (link or copy)
- **THEN** the artifact is installed to the chosen destination
- **THEN** if project-local scope was selected, the project directory is added to the tended projects list

### Requirement: Disambiguate artifacts across registries
When an artifact name exists in multiple registries, the Koru CLI SHALL present all matches and let the user choose.

#### Scenario: Artifact in multiple registries
- **GIVEN** `work` registry contains `core/skills/review.md`
- **AND GIVEN** `personal` registry contains `core/skills/review.md`
- **WHEN** the user runs `koru install review`
- **THEN** the Koru CLI lists both matches with their registry and path
- **THEN** the user selects which one to install

### Requirement: Install creates link or copy
The Koru CLI SHALL support both symlink and copy strategies when installing an artifact.

#### Scenario: Link mode
- **WHEN** the user selects link mode during install
- **THEN** a symbolic link is created from the destination to the registry source file

#### Scenario: Copy mode
- **WHEN** the user selects copy mode during install
- **THEN** a standalone copy of the file is written to the destination
- **THEN** the checksum is recorded in the installation database

### Requirement: Scope selection during install
The Koru CLI SHALL allow the user to choose between global and project-local scope.

#### Scenario: Global scope install
- **WHEN** the user selects global scope
- **THEN** the artifact is placed in the global directory (e.g. `~/.chimera/modes/`)

#### Scenario: Project-local scope install
- **WHEN** the user selects project-local scope
- **THEN** the artifact is placed in the current project directory (e.g. `./.chimera/modes/`)
- **THEN** the project path is registered in the Koru CLI's tended projects list
