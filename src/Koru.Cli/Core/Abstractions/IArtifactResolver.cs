using Koru.Contracts;

namespace Koru.Cli.Core.Abstractions;

public interface IArtifactResolver
{
    IReadOnlyList<ResolvedArtifact> Resolve(string query);
}
