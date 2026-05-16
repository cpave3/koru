using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class GitOpsStub : IGitOps
{
    public void Clone(string remote, string path) => throw new NotImplementedException("stub");
    public void CommitAll(string path, string message) => throw new NotImplementedException("stub");
    public void Init(string path) => throw new NotImplementedException("stub");
    public bool IsClean(string path) => throw new NotImplementedException("stub");
    public IReadOnlyList<string> ListTrackedFiles(string path) => throw new NotImplementedException("stub");
    public void Pull(string path) => throw new NotImplementedException("stub");
    public void Push(string path) => throw new NotImplementedException("stub");
    public void SetRemote(string path, string remote) => throw new NotImplementedException("stub");
    public IReadOnlyList<string> Status(string path) => throw new NotImplementedException("stub");
    public string GetHeadSha(string path) => throw new NotImplementedException("stub");
}
