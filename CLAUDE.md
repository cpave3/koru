# CLAUDE.md — Koru

A .NET global tool that manages markdown artifacts (agent skills, harness configs) across **git-backed registries** with a **plugin system**. Each plugin teaches Koru how to place artifacts on disk for a specific harness (Chimera, Claude, etc.).

## Spec sources (read these first when changing behavior)

- `SPEC.md` — full specification.
- `CONTEXT.md` — domain glossary. Use this language; don't invent synonyms.
- `docs/adr/0001-additive-plugin-claims.md` — why two plugins can both claim the same path.
- `docs/adr/0002-installation-state-database.md` — why `state.json` exists.
- `docs/adr/0003-sync-git-workflow.md` — dirty-tree prompt policy.
- `openspec/changes/agent-config-cli/specs/*/spec.md` — per-capability EARS requirements (registry-management, plugin-system, sync-engine, project-install, drift-detection).

## Toolchain

- **Target framework: `net10.0`** for every project. The repo's `global.json` pins SDK 10.0.202. SPEC.md mentions net8.0 as a baseline but only .NET 10 is installed locally; do not downgrade unless you also install the .NET 8 runtime.
- Spectre.Console.Cli (command parsing + DI) · Spectre.Console (TTY output) · LibGit2Sharp (git) · YamlDotNet (`registry.yaml`) · NuGet.Protocol (plugin install) · System.IO.Abstractions.

## Build / test / run

```bash
dotnet build                                       # all 6 projects
dotnet test                                        # 54 unit + 7 integration
dotnet run --project src/Koru.Cli -- --help        # smoke run
dotnet pack src/Koru.Cli -c Release                # produces Koru.<version>.nupkg
```

The CLI must not be installed globally to work on it — `dotnet run --project src/Koru.Cli -- <args>` runs the just-built bits.

## Project layout

```
src/
  Koru.Contracts/        # public plugin surface — IPlugin, Artifact, Scope, InstallMode, InstallPlan.
  Koru.Cli/
    Program.cs           # Spectre.Console.Cli wiring (command tree + DI host). Do not add commands here.
    Core/
      Abstractions/      # I*-interfaces (IConfigStore, IStateStore, IGitOps, IPluginHost, IPathExpander, IChecksum, IGlobMatcher, IArtifactResolver, IRegistryManifestStore).
      Models/            # POCOs (CliConfig, InstallRecord, RegistryEntry, RegistryManifest, PluginRef).
      Bootstrap.cs       # SINGLE source of DI registrations. Add new services here.
      Config/, State/, Util/, Git/, Registry/, Plugins/, Sync/, Install/, Stubs/
    Commands/
      Config/, Registry/, Plugin/, Sync/, Install/, List/
                         # One file per command. Each derives from Command<TSettings> or AsyncCommand<TSettings>.
tests/
  Koru.Tests/            # xUnit unit tests, grouped by capability folder.
  Koru.Tests.SamplePlugin/  # real IPlugin assembly loaded by the plugin-loader integration test.
  Koru.IntegrationTests/ # xUnit end-to-end CLI scenarios.
```

## Conventions (follow these)

- **Add a service:** define `I*` in `Core/Abstractions/`, implement in `Core/<area>/`, register in `Core/Bootstrap.cs`. Inject via constructor.
- **Add a command:** new file under `Commands/<area>/<Name>Command.cs`, register in `Bootstrap.cs` as `Transient`, wire into the Spectre tree in `Program.cs`. Settings classes are nested or in `<Area>Settings.cs`.
- **Tests** live next to their unit: `tests/Koru.Tests/<area>/<Name>Tests.cs`. Integration scenarios go in `Koru.IntegrationTests/CliEndToEndTests.cs` and run the CLI through `CommandRunner`.
- **JSON enums** serialize as camelCase strings (`"link"`, `"copy"`, `"global"`, `"projectLocal"`). Use the existing `JsonStringEnumConverter` with `JsonNamingPolicy.CamelCase`.
- **Paths** are forward-slash relative to registry root in the data model (`Artifact.Path`, `InstallRecord.SourcePath`). Absolute filesystem paths only at the boundary, via `IPathExpander`.

## Key invariants (do not break)

- **Koru does not interpret artifact frontmatter.** Routing is by registry directory shape + plugin path claims. Don't add YAML-parsing of artifact contents.
- **`state.json` is per-registry, never committed.** Already in `.gitignore`. Don't move it into the registry tree.
- **Plugin claims are additive.** Multiple plugins may claim the same registry path; both install. Two plugins producing the **same absolute destination** is a conflict and must abort sync with both plugin names.
- **Copy-mode drift aborts sync.** Compare installed-file SHA-256 against recorded `installedChecksum`. Drift → bail with a `koru reset <artifact>` hint. Don't silently overwrite.
- **Link mode bypasses drift detection entirely.** The symlink target is always refreshed to the current working-tree path on sync.
- **Sync walks git-tracked files only.** Use `IGitOps.ListTrackedFiles`; exclude `.git/`, `state.json`, untracked files, and `registry.yaml`.
- **Dirty registry working tree prompts the user** (abort vs. proceed-with-local). `--yes` defaults to proceed. `koru reset` requires a clean working tree.
- **Core plugin** is synthetic, always loaded, claims `core/**`, and emits `~/.claude/{skills,agents}/` (global) or `./.claude/{skills,agents}/` (project-local).

## Where state lives at runtime

| Path | What |
|---|---|
| `~/.config/koru/config.json` | Global config: registries, tended projects, NuGet feeds, default registry. |
| `~/.koru/registries/<name>/` | Working tree of a registry. |
| `~/.koru/registries/<name>/state.json` | Per-registry installation database (gitignored). |
| `~/.koru/plugins/<name>/` | Extracted plugin assemblies, loaded via per-plugin `AssemblyLoadContext`. |

## Cross-platform notes

- Windows symlink creation may fail without privilege; `LinkInstaller` falls back to copy with a warning — keep it that way.
- Config path resolves via `Environment.SpecialFolder.ApplicationData`. Don't hard-code `~/.config/`.
- The `KORU_HOME` env var (used by integration tests) redirects all storage roots. Honor it in any new path resolution.
