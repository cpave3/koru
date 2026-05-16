## 1. Foundations: scanner + remote head lookup

- [ ] 1.1 Add `IGitOps.GetRemoteHeadSha(string remoteUrl, string @ref)` and implement via `Repository.ListRemoteReferences` in `GitOperations`; throw a typed `RemoteUnreachableException` on auth/network failure
- [ ] 1.2 Update `GitOpsStub` and the test `FakeGitOps` in `InstallPlanBuilderTests` to satisfy the new interface member
- [ ] 1.3 Add `Frontmatter.TryReadSource(string content, out SourceBlock)` helper that returns true only when a well-formed `source:` block is present
- [ ] 1.4 Create `Core/Import/ImportedArtifactScanner.cs` with `IEnumerable<ImportedArtifact> Scan(string registryName)`; walk via `ArtifactDiscovery`, read the primary content file (SKILL.md for dir, the .md for file), parse frontmatter, emit a record when a `source:` block is present
- [ ] 1.5 Wire `IImportedArtifactScanner` and `IGitOps.GetRemoteHeadSha` into `Bootstrap.cs`
- [ ] 1.6 Unit tests for the scanner: single-file and directory imports detected; non-imported artifacts skipped; malformed frontmatter does not crash

## 2. Shared Importer helper

- [ ] 2.1 Extract the body of `ImportCommand.Execute` (clone → resolve subpath → copy → stamp → commit) into `Core/Import/Importer.cs` as `ImportResult ImportInto(RegistryEntry, string url, string subpath, string localName, bool force)`
- [ ] 2.2 Refactor `ImportCommand` to be a thin wrapper that parses CLI args and calls `Importer.ImportInto`
- [ ] 2.3 Re-run existing `ImportCommandTests` to confirm no regression; suite must remain green

## 3. `koru import --check`

- [ ] 3.1 Add `--check` boolean option to `ImportCommand.ImportSettings`; when set, ignore `<git-url>` requirement (the URL becomes optional/unused under `--check`)
- [ ] 3.2 In `ImportCommand.Execute`, if `--check`, dispatch to a new `CheckRunner.RunAsync(targetRegistries)` instead of the import flow
- [ ] 3.3 Implement `CheckRunner` in `Core/Import/CheckRunner.cs`: iterate scanned artifacts, dedupe by source repo URL, call `GetRemoteHeadSha`, classify each as `UpToDate | Behind | Unreachable`, return a `CheckReport`
- [ ] 3.4 Render the report via Spectre.Console table: columns Artifact, Source (repo + path), Status. Status `behind` shows `behind {old[..7]}→{new[..7]}`; `unreachable` shows the reason in red
- [ ] 3.5 Set exit code: `0` all up-to-date, `1` at least one behind and none unreachable, `2` any unreachable
- [ ] 3.6 Integration test in `Koru.IntegrationTests`: import two artifacts from two file:// repos, advance one upstream by a commit, run `--check`, assert one row is `up-to-date` and one is `behind`; assert exit code `1`

## 4. `koru update <name>`

- [ ] 4.1 Create `Commands/Update/UpdateCommand.cs` with settings `[CommandArgument(0, "[name]")]`, `--registry`, `--yes`, `--force`
- [ ] 4.2 If a name is given: resolve the imported artifact by (registry filter, artifact name) via `IImportedArtifactScanner`. Error clearly if not found or if the artifact has no `source:` block
- [ ] 4.3 Check the registry working tree for uncommitted changes touching that artifact's files (use `IGitOps.Status`); refuse with a remediation message if dirty (do NOT honor `--force` here — force is only for destination overwrite within the import helper)
- [ ] 4.4 Call `Importer.ImportInto(registry, source.repo, source.path, artifactName, force: true)` to overwrite the registry copy, restamp `source.commit` to the new HEAD, restamp `imported_at`, preserve other source fields
- [ ] 4.5 Commit with `update: <name> from <repo>@<short-sha>`
- [ ] 4.6 Wire `UpdateCommand` into `Bootstrap.cs`, `Program.cs`, and `CommandRunner.cs` (integration tests)
- [ ] 4.7 Integration test: import a single artifact, advance upstream, run `koru update <name> --yes`, assert the source commit is updated and the content matches the latest

## 5. `koru update` (no args)

- [ ] 5.1 In `UpdateCommand`, when no positional name is given: run the check pipeline first to identify `Behind` artifacts; skip `UpToDate` and `Unreachable`
- [ ] 5.2 For each behind artifact, check working-tree cleanliness; skip with reason `local edits` if dirty
- [ ] 5.3 Without `--yes`, prompt `Refresh <name> ({old[..7]} → {new[..7]})? [Y/n]` for each candidate; with `--yes`, refresh unconditionally
- [ ] 5.4 Print a summary table of every imported artifact with outcome: refreshed, up-to-date, local-edits, unreachable
- [ ] 5.5 Exit non-zero if any artifact ended in `unreachable` or `local-edits` (partial completion); zero otherwise
- [ ] 5.6 Integration test: registry with three artifacts (one behind, one up-to-date, one with a local edit), run `koru update --yes`, assert outcomes match and exit code is non-zero

## 6. Documentation

- [ ] 6.1 Add a "Checking for updates" subsection to README.md under the import section, with examples for `--check`, `update <name>`, and `update`
- [ ] 6.2 Add `koru import --check`, `koru update <name>`, and `koru update` rows to the command reference table
- [ ] 6.3 Update CLAUDE.md invariants to mention that `source:` frontmatter is also READ by the check/update commands (previously: only written by `koru import`)
- [ ] 6.4 Update SPEC.md `koru import` section to mention the `--check` flag; add a `koru update` section beside it; add an entry in the Decisions table referencing this change

## 7. Test suite gate

- [ ] 7.1 `dotnet build` succeeds with zero errors
- [ ] 7.2 `dotnet test` reports all unit + integration tests passing
- [ ] 7.3 Manual smoke test against a real public skills repo: `koru import --check --registry <reg>` against a registry that has imports from `mattpocock/skills`, then `koru update <name>` on one of them
