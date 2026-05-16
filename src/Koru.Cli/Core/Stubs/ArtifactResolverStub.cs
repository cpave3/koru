using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class ArtifactResolverStub : IArtifactResolver
{
    public IReadOnlyList<ResolvedArtifact> Resolve(string query) => throw new NotImplementedException("stub");

    public IReadOnlyList<ResolvedArtifact> ResolveAll() => throw new NotImplementedException("stub");
}
