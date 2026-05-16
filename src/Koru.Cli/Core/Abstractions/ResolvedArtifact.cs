using Koru.Contracts;

namespace Koru.Cli.Core.Abstractions;

public record ResolvedArtifact(string RegistryName, string SourcePath, IReadOnlyList<IPlugin> ClaimingPlugins);
