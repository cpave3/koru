using System.Security.Cryptography;
using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Util;

public class Checksum : IChecksum
{
    public string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        var hex = Convert.ToHexStringLower(hash);
        return $"sha256:{hex}";
    }
}
