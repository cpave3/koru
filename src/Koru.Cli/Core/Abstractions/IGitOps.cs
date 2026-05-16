namespace Koru.Cli.Core.Abstractions;

public interface IGitOps
{
    void Init(string path);
    void Clone(string remote, string path);
    IReadOnlyList<string> Status(string path);
    bool IsClean(string path);
    void Pull(string path);
    void Push(string path);
    void SetRemote(string path, string remote);
    IReadOnlyList<string> ListTrackedFiles(string path);
    void CommitAll(string path, string message);

    /// The full SHA-1 of HEAD. Throws if the repository has no commits.
    string GetHeadSha(string path);
}
