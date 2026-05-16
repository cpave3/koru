## ADDED Requirements

### Requirement: Imported artifacts are discoverable by their provenance frontmatter
The Koru CLI SHALL identify every artifact in a registry whose primary content file (`SKILL.md` for directory artifacts, the `.md` file for single-file artifacts) has a top-level YAML frontmatter `source:` block recording `repo`, `path`, `ref`, `commit`, and `imported_at`.

#### Scenario: Single-file imported artifact is discovered
- **GIVEN** a registry contains `core/skills/foo.md` whose frontmatter has a `source:` block
- **WHEN** the Koru CLI enumerates imported artifacts in that registry
- **THEN** `foo` appears in the result with its parsed source block

#### Scenario: Directory imported artifact is discovered via SKILL.md
- **GIVEN** a registry contains `core/skills/grill-me/SKILL.md` whose frontmatter has a `source:` block
- **WHEN** the Koru CLI enumerates imported artifacts in that registry
- **THEN** `grill-me` appears in the result with its parsed source block

#### Scenario: Non-imported artifacts are skipped
- **GIVEN** a registry contains an artifact whose frontmatter has no `source:` key
- **WHEN** the Koru CLI enumerates imported artifacts
- **THEN** that artifact does not appear in the result

### Requirement: `koru import --check` reports upstream status for every imported artifact
The Koru CLI SHALL audit every imported artifact in a registry against its recorded upstream by querying each remote's current `HEAD` sha (or the sha at the recorded `ref`), then printing a table indicating whether each artifact is up-to-date, behind, or unreachable. The command SHALL NOT modify the registry.

#### Scenario: Up-to-date artifact
- **GIVEN** an imported artifact whose recorded `commit` equals the upstream's current `HEAD` sha
- **WHEN** the user runs `koru import --check`
- **THEN** the artifact row shows status `up-to-date`

#### Scenario: Behind artifact
- **GIVEN** an imported artifact whose recorded `commit` differs from the upstream's current `HEAD` sha
- **WHEN** the user runs `koru import --check`
- **THEN** the artifact row shows status `behind` along with the recorded commit and the upstream commit

#### Scenario: Unreachable upstream
- **GIVEN** an imported artifact whose recorded `repo` URL cannot be reached (network failure, auth failure, path-not-found)
- **WHEN** the user runs `koru import --check`
- **THEN** the artifact row shows status `unreachable` with a human-readable reason
- **THEN** the check continues for remaining artifacts and does not abort

#### Scenario: Exit code reflects aggregate status
- **WHEN** every artifact is up-to-date
- **THEN** the command exits with code `0`
- **WHEN** at least one artifact is behind and none are unreachable
- **THEN** the command exits with code `1`
- **WHEN** at least one upstream is unreachable
- **THEN** the command exits with code `2`

#### Scenario: Dedupe queries by upstream URL
- **GIVEN** two imported artifacts share the same `source.repo` URL
- **WHEN** the user runs `koru import --check`
- **THEN** the Koru CLI queries that upstream's HEAD exactly once and uses the result for both artifacts

### Requirement: `koru update <artifact-name>` refreshes a single imported artifact
The Koru CLI SHALL re-import a named imported artifact against its recorded `source.repo` and `source.path`, replacing the registry copy and stamping a new `source:` block. The command SHALL refuse if the registry working tree has uncommitted changes touching the artifact.

#### Scenario: Refresh a behind artifact
- **GIVEN** the artifact `grill-me` is an imported artifact and its upstream `HEAD` has advanced past the recorded commit
- **WHEN** the user runs `koru update grill-me --yes`
- **THEN** the artifact's files are replaced with the latest upstream content
- **THEN** the `source:` block's `commit` and `imported_at` fields are refreshed; `repo`, `path`, and `ref` are unchanged
- **THEN** the change is staged and committed in the registry working tree

#### Scenario: Skip an artifact with uncommitted local edits
- **GIVEN** the artifact `grill-me` has uncommitted changes in the registry working tree
- **WHEN** the user runs `koru update grill-me`
- **THEN** the command reports the local edits and refuses to refresh
- **THEN** the registry working tree is unchanged

#### Scenario: Reject a non-imported artifact name
- **GIVEN** the artifact `foo` exists in the registry but has no `source:` frontmatter block
- **WHEN** the user runs `koru update foo`
- **THEN** the command exits non-zero with a message explaining that `foo` has no recorded provenance
- **THEN** the registry working tree is unchanged

### Requirement: `koru update` refreshes every behind artifact
With no positional argument, the Koru CLI SHALL run the check pipeline to identify imported artifacts whose upstream has advanced, then refresh each one. Artifacts that are up-to-date or have local edits SHALL be skipped with a clear message. The default behaviour SHALL prompt before each refresh; `--yes` SHALL skip the prompts.

#### Scenario: Refresh a mix of behind, up-to-date, and locally-edited artifacts
- **GIVEN** the registry has three imported artifacts: `a` (behind), `b` (up-to-date), `c` (locally edited)
- **WHEN** the user runs `koru update --yes`
- **THEN** `a` is refreshed and committed
- **THEN** `b` is skipped with reason `up-to-date`
- **THEN** `c` is skipped with reason `local edits`
- **THEN** the summary lists each artifact and its outcome

#### Scenario: Interactive confirmation per artifact
- **GIVEN** two artifacts are behind upstream
- **WHEN** the user runs `koru update` (without `--yes`) and declines the first prompt
- **THEN** the first artifact is left unchanged
- **THEN** the second artifact's prompt is still presented

#### Scenario: Unreachable upstream during bulk update
- **GIVEN** at least one imported artifact's upstream is unreachable
- **WHEN** the user runs `koru update --yes`
- **THEN** unreachable artifacts are skipped with their failure reason
- **THEN** reachable behind artifacts are still refreshed
- **THEN** the exit code is non-zero to signal partial completion

### Requirement: `--registry` flag scopes commands to one registry
For each new command (`koru import --check`, `koru update <name>`, `koru update`), the Koru CLI SHALL accept a `--registry <name>` option that limits the operation to a single registered registry. With no flag, the command SHALL operate on every registered registry.

#### Scenario: Scoped check
- **GIVEN** two registries `work` and `personal` are registered, each with imported artifacts
- **WHEN** the user runs `koru import --check --registry personal`
- **THEN** only `personal`'s imported artifacts appear in the table
