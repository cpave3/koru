using Koru.Contracts;

namespace Koru.Cli.Core.Sync;

public record DesiredInstall(
    string RegistryName,
    string SourcePath,
    string DestinationPath,
    string Plugin,
    InstallMode Mode,
    Scope Scope,
    string? ProjectRoot);
