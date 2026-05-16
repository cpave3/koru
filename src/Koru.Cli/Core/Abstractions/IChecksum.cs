namespace Koru.Cli.Core.Abstractions;

public interface IChecksum
{
    string ComputeSha256(string filePath);

    /// SHA-256 over a sorted manifest of every file under the directory tree.
    /// Manifest line per file: "&lt;forward-slash-relative-path&gt;\0&lt;file-sha256&gt;\n".
    /// Result is prefixed "sha256-tree:" to disambiguate from file hashes.
    string ComputeSha256Tree(string directoryPath);
}
