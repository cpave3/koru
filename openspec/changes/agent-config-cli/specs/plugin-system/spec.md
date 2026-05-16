## ADDED Requirements

### Requirement: User can install a plugin from NuGet
The Koru CLI SHALL download and install a NuGet package containing a plugin assembly to the local plugins directory.

#### Scenario: Installing a plugin
- **WHEN** the user runs `koru plugin add <nuget-package-name>`
- **THEN** the NuGet package is downloaded and extracted to `~/.koru/plugins/<package-name>/`
- **THEN** the assembly is scanned for types implementing `IPlugin`
- **THEN** valid plugins are loaded and registered

### Requirement: Plugin implements a standard interface
The Koru CLI SHALL validate that loaded plugin assemblies implement the `IPlugin` interface contract.

#### Scenario: Loading a valid plugin
- **WHEN** the Koru CLI scans plugin assemblies at startup
- **THEN** any assembly containing a type implementing `IPlugin` is registered
- **THEN** assemblies without a valid `IPlugin` implementation are ignored with a warning

### Requirement: Plugin declares path claims
A plugin SHALL declare which registry paths it claims interest in via a `PathClaims` property, and the Koru CLI SHALL route matching artifacts to the plugin.

#### Scenario: Plugin claims a directory
- **GIVEN** a plugin declares `PathClaims = ["chimera/**"]`
- **WHEN** the Koru CLI walks the registry and encounters `chimera/modes/review.md`
- **THEN** the plugin is invoked via `GetInstallPlan` to determine placement for that artifact

### Requirement: Overlapping plugin claims are additive
When multiple plugins claim the same registry path, the Koru CLI SHALL invoke all claiming plugins.

#### Scenario: Two plugins claim the same file
- **GIVEN** Plugin A claims `chimera/**` and wants files in `.chimera/`
- **AND GIVEN** Plugin B claims `chimera/**` and wants files in `.backups/chimera/`
- **WHEN** an artifact at `chimera/modes/review.md` is synced
- **THEN** it is installed to both `.chimera/modes/review.md` and `.backups/chimera/modes/review.md`

### Requirement: Registry manifest declares required plugins
A registry SHALL contain a `registry.yaml` that lists plugins required to interpret its contents.

#### Scenario: Using a registry with missing plugins
- **GIVEN** a registry's `registry.yaml` lists `chimera-plugin` as required
- **WHEN** the user runs `koru use <registry-url>`
- **THEN** the Koru CLI reports that `chimera-plugin` is required but not installed
- **THEN** the user is informed they can install it with `koru plugin add chimera-plugin`
