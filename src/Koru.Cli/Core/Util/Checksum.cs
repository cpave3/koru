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

    public string ComputeSha256Tree(string directoryPath)
    {
        var root = Path.GetFullPath(directoryPath);
        var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (
                Rel: Path.GetRelativePath(root, f).Replace('\\', '/'),
                Full: f))
            .OrderBy(t => t.Rel, StringComparer.Ordinal)
            .ToList();

        using var sha256 = SHA256.Create();
        using var manifest = new MemoryStream();
        using var writer = new BinaryWriter(manifest);
        foreach (var (rel, full) in entries)
        {
            var fileHash = ComputeSha256(full);
            writer.Write(System.Text.Encoding.UTF8.GetBytes(rel));
            writer.Write((byte)0);
            writer.Write(System.Text.Encoding.UTF8.GetBytes(fileHash));
            writer.Write((byte)'\n');
        }
        writer.Flush();
        manifest.Position = 0;
        var hash = sha256.ComputeHash(manifest);
        return $"sha256-tree:{Convert.ToHexStringLower(hash)}";
    }
}
