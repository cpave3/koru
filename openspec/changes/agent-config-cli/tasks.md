## 1. Bootstrap .NET Project and Tooling

- [ ] 1.1 Create solution and `src/Cli` console project targeting .NET 8
- [ ] 1.2 Add `System.CommandLine` or `Spectre.Console.Cli` for command parsing
- [ ] 1.3 Add `Spectre.Console` for rich TTY output (prompts, tables, panels)
- [ ] 1.4 Add `LibGit2Sharp` for git operations
- [ ] 1.5 Set up `dotnet tool` packaging in `.csproj` for global tool distribution

## 2. Global Configuration Infrastructure

- [ ] 2.1 Define `CliConfig` model (registries list, tended projects, defaults)
- [ ] 2.2 Implement config read/write at cross-platform path (`~/.config/koru/config.json`)
- [ ] 2.3 Add `koru config get|set|list` commands for inspecting and editing config

## 3. Registry Management

- [ ] 3.1 Implement `koru init <name>` — scaffold registry directory with `core/skills/`, `core/agents/`, `registry.yaml`, and `git init`
- [ ] 3.2 Implement `koru use <remote-url> [name]` — clone registry, register in config, read `registry.yaml`, report missing plugins
- [ ] 3.4 Implement `koru list registries` — show all registered registries with names and remotes
- [ ] 3.5 Implement `koru registry status <name>` — show git status of a registry working tree

## 4. Plugin System

- [ ] 4.1 Define `IPlugin` interface with `PathClaims` (declarative glob patterns) and `GetInstallPlan`
- [ ] 4.2 Implement `koru plugin add <nuget-name>` — download/extract NuGet to `~/.koru/plugins/<name>/`
- [ ] 4.3 Implement plugin assembly scanning and `IPlugin` discovery on startup
- [ ] 4.4 Implement plugin loading — validate interface contract, register valid plugins, log/ignore invalid assemblies
- [ ] 4.5 Implement `koru list plugins` — show loaded plugins and their claims
- [ ] 4.6 On `koru use`, read `registry.yaml` plugins list and warn about missing plugins

## 5. Artifact Discovery and Install Planning

- [ ] 5.1 Implement registry tree walker that matches **git-tracked** files against plugin `PathClaims` (excludes untracked files, `.git/`, `state.json`)
- [ ] 5.2 Implement install plan builder — for each matched artifact, ask claiming plugins for destinations
- [ ] 5.3 Support additive claims (same artifact → multiple destinations from multiple plugins)
- [ ] 5.4 Implement `koru install <artifact-name>` — resolve across all registries, disambiguate if multiple matches, prompt for scope and mode
- [ ] 5.5 Implement `koru list artifacts` — show all artifacts across registries with claiming plugins

## 6. Installation State Database

- [ ] 6.1 Define `InstallRecord` model (sourcePath, destinationPath, installMode, plugin, checksum)
- [ ] 6.2 Implement read/write of `state.json` per registry at `~/.koru/registries/<name>/state.json`
- [ ] 6.3 Ensure state.json is created on first install and excluded from git

## 7. Sync Engine

- [ ] 7.1 Implement desired state computation by walking **git-tracked** registry tree + querying plugin `PathClaims`
- [ ] 7.2 Implement state diff — compare desired state against installation database
- [ ] 7.3 Implement file convergence — create new files, update changed files, remove orphaned files
- [ ] 7.4 Implement link-mode installs (create/update symlinks)
- [ ] 7.5 Implement copy-mode installs (write file, record SHA-256 checksum)
- [ ] 7.6 Implement dirty working tree detection — prompt user to abort or proceed before git pull
- [ ] 7.7 Implement git pull in clean working trees with merge conflict handling
- [ ] 7.8 Implement sync across all tended projects — install project-local artifacts to every tended directory
- [ ] 7.9 Implement `koru sync --global-only` and `koru sync --project <path>` flags

## 8. Drift Detection

- [ ] 8.1 Implement SHA-256 checksum computation on copy-mode install
- [ ] 8.2 Implement checksum comparison during sync — fail if copy-mode file differs from recorded checksum
- [ ] 8.3 Implement clear error message with remediation hint (`koru reset <artifact>`)
- [ ] 8.4 Implement `koru reset <artifact-name>` — overwrite drifted copy with registry version, update checksum
- [ ] 8.5 Ensure symlinked artifacts bypass drift detection entirely

## 9. CLI Commands and UX Polish

- [ ] 9.1 Implement `koru status` — show drift and pending changes without modifying anything
- [ ] 9.2 Implement `koru untend <path>` — remove a project from the tended list
- [ ] 9.3 Add `--dry-run` flag to `koru sync` for previewing changes
- [ ] 9.4 Add progress indicators and colored output for all mutation commands
- [ ] 9.5 Add `--yes` / `--no-interactive` flag for CI/automation use

## 10. Testing and Packaging

- [ ] 10.1 Add unit tests for config serialization/deserialization
- [ ] 10.2 Add unit tests for install plan builder and state diff logic
- [ ] 10.3 Add integration tests for `koru init`, `koru use`, `koru sync` using temp directories
- [ ] 10.4 Add integration tests for plugin loading with mock assemblies
- [ ] 10.5 Package and validate as `dotnet tool install --global`
- [ ] 10.6 Write README with installation, usage, and plugin authoring guide
