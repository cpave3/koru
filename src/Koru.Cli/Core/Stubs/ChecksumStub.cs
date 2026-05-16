using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Stubs;

public class ChecksumStub : IChecksum
{
    public string ComputeSha256(string filePath) => throw new NotImplementedException("stub");
}
