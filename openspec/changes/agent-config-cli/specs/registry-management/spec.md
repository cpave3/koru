## ADDED Requirements

### Requirement: User can initialize a new registry
The Koru CLI SHALL create a new local registry directory with a standard structure and initialize it as a git repository.

#### Scenario: Successful registry initialization
- **WHEN** the user runs `koru init <name>`
- **THEN** a directory is created at `~/.koru/registries/<name>/`
- **THEN** the directory contains a `registry.yaml` with a default structure
- **THEN** the directory is initialized as a git repository with an initial commit

### Requirement: User can link a registry to a remote
The Koru CLI SHALL record a remote URL for a local registry and push the current state.

#### Scenario: Linking a remote
- **WHEN** the user runs `koru link <remote-url>` in a registry directory
- **THEN** the remote URL is stored in the Koru CLI's global config for that registry
- **THEN** the current branch is pushed to the remote

### Requirement: User can use a remote registry
The Koru CLI SHALL clone a remote registry to the local machine and register it.

#### Scenario: Using a remote registry
- **WHEN** the user runs `koru use <remote-url>`
- **THEN** the repository is cloned to `~/.koru/registries/<name>/`
- **THEN** the registry is registered in the Koru CLI's global config
- **THEN** the registry's `registry.yaml` is read and required plugins are reported as missing

### Requirement: Multiple registries are supported
The Koru CLI SHALL allow multiple registries to be registered simultaneously.

#### Scenario: Registering a second registry
- **WHEN** the user has already registered a registry named `work`
- **AND WHEN** the user runs `koru use <personal-url>`
- **THEN** a second registry named `personal` is registered
- **THEN** both registries appear in `koru list registries`

### Requirement: Registry directory structure
The registry SHALL contain a `core/` directory for core-managed artifacts and allow plugin-named directories for plugin-managed artifacts.

#### Scenario: Default registry layout
- **WHEN** a user initializes a new registry
- **THEN** the directory structure includes `core/skills/` and `core/agents/`
- **THEN** the root contains `registry.yaml`
