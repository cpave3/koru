namespace Koru.Cli.Core.Abstractions;

public interface IGlobMatcher
{
    bool Matches(string pattern, string path);
}
