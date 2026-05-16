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

### Requirement: Artifacts may be a directory containing `SKILL.md`
The Koru CLI SHALL treat a directory containing a `SKILL.md` file as a single artifact, installing the entire directory tree to the destination.

#### Scenario: Directory artifact is installed as a tree
- **GIVEN** a registry contains `core/skills/grill-me/SKILL.md` plus sibling files `AGENT-BRIEF.md` and `scripts/run.sh`
- **WHEN** the user runs `koru install grill-me`
- **THEN** the destination is `<scope>/.claude/skills/grill-me/` (a directory)
- **THEN** every file under `core/skills/grill-me/` is copied to the corresponding path under the destination
- **THEN** a single installation database entry records the directory artifact, with an aggregate `sha256-tree:` checksum

#### Scenario: Directory artifact in link mode uses a single symlink
- **WHEN** the user installs a directory artifact with link mode
- **THEN** the destination is a single symbolic link to the registry directory
- **THEN** individual files inside the destination are not separately symlinked

### Requirement: User can import a skill from an external git repo
The Koru CLI SHALL clone an external git repository, copy a selected artifact into a local registry, and record provenance metadata in a YAML frontmatter `source:` block.

#### Scenario: Importing a directory artifact with provenance
- **GIVEN** an external repo at `<git-url>` contains `skills/productivity/grill-me/SKILL.md` plus siblings
- **WHEN** the user runs `koru import <git-url> skills/productivity/grill-me --registry <reg> --yes`
- **THEN** the Koru CLI clones the source to a temporary directory
- **THEN** the directory is copied to `<reg>/core/skills/grill-me/`
- **THEN** the imported `SKILL.md` has a top-level YAML frontmatter `source:` block containing `repo`, `path`, `ref`, `commit`, `imported_at`
- **THEN** any existing frontmatter keys on the source `SKILL.md` are preserved
- **THEN** the change is staged and committed in the target registry's working tree

#### Scenario: Importing a single file artifact
- **GIVEN** an external repo contains a markdown file at `notes/idea.md`
- **WHEN** the user runs `koru import <git-url> notes/idea.md --registry <reg> --yes`
- **THEN** the file is copied to `<reg>/core/skills/idea.md` (or to `<reg>/core/skills/<--name>.md` if `--name` is provided)
- **THEN** the file gains a YAML frontmatter `source:` block
- **THEN** the change is staged and committed

#### Scenario: Import refuses to overwrite without --force
- **GIVEN** the target registry already contains an artifact at the would-be destination
- **WHEN** the user runs `koru import` without `--force`
- **THEN** the command reports the conflict and skips the import
- **THEN** the existing artifact is left untouched

#### Scenario: Interactive picker over source artifacts
- **WHEN** the user runs `koru import <git-url>` with no subpath argument
- **THEN** the Koru CLI clones the source and discovers its artifacts via the same SKILL.md/`.md` rules as registry discovery
- **THEN** the user is presented with a multi-select picker of source artifacts
