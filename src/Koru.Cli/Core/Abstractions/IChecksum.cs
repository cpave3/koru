namespace Koru.Cli.Core.Abstractions;

public interface IChecksum
{
    string ComputeSha256(string filePath);
}
