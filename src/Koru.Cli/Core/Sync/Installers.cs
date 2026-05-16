using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Util;
using System.IO.Abstractions;

namespace Koru.Cli.Core.Sync;

public interface ILinkInstaller
{
    void Install(string sourcePath, string destinationPath);
}

public interface ICopyInstaller
{
    (string SourceChecksum, string InstalledChecksum) Install(string sourcePath, string destinationPath);
}

public class LinkInstaller : ILinkInstaller
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathExpander _pathExpander;

    public LinkInstaller(IFileSystem fileSystem, IPathExpander pathExpander)
    {
        _fileSystem = fileSystem;
        _pathExpander = pathExpander;
    }

    public void Install(string sourcePath, string destinationPath)
    {
        var absoluteSource = Path.IsPathFullyQualified(sourcePath)
            ? sourcePath
            : Path.GetFullPath(sourcePath);

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent) && !_fileSystem.Directory.Exists(parent))
            _fileSystem.Directory.CreateDirectory(parent);

        try
        {
            if (_fileSystem.File.Exists(destinationPath))
                _fileSystem.File.Delete(destinationPath);
            else if (_fileSystem.Directory.Exists(destinationPath))
                _fileSystem.Directory.Delete(destinationPath, recursive: true);
        }
        catch (IOException) { }

        var sourceIsDirectory = Directory.Exists(absoluteSource);

        try
        {
            if (sourceIsDirectory)
                Directory.CreateSymbolicLink(destinationPath, absoluteSource);
            else
                File.CreateSymbolicLink(destinationPath, absoluteSource);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"Warning: Could not create symlink at '{destinationPath}' (insufficient privileges). Falling back to copy.");
            if (sourceIsDirectory)
                AtomicFile.CopyDirectory(absoluteSource, destinationPath);
            else
                AtomicFile.Copy(absoluteSource, destinationPath);
        }
    }
}

public class CopyInstaller : ICopyInstaller
{
    private readonly IFileSystem _fileSystem;
    private readonly IChecksum _checksum;

    public CopyInstaller(IFileSystem fileSystem, IChecksum checksum)
    {
        _fileSystem = fileSystem;
        _checksum = checksum;
    }

    public (string SourceChecksum, string InstalledChecksum) Install(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            AtomicFile.CopyDirectory(sourcePath, destinationPath);
            var sourceTreeHash = _checksum.ComputeSha256Tree(sourcePath);
            var installedTreeHash = _checksum.ComputeSha256Tree(destinationPath);
            return (sourceTreeHash, installedTreeHash);
        }

        AtomicFile.Copy(_fileSystem, sourcePath, destinationPath);
        var sourceChecksum = _checksum.ComputeSha256(sourcePath);
        var installedChecksum = _checksum.ComputeSha256(destinationPath);
        return (sourceChecksum, installedChecksum);
    }
}
