using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class GlobMatcherStub : IGlobMatcher
{
    public bool Matches(string pattern, string path) => throw new NotImplementedException("stub");
}
