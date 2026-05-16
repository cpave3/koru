using System.IO.Abstractions;

namespace Koru.Cli.Core.Util;

/// Replaces a destination file by writing to a sibling tempfile and renaming.
/// Avoids "file in use" errors when the destination is open elsewhere
/// (e.g. an editor or watcher); rename(2) on Linux works even with open handles.
public static class AtomicFile
{
    public static void Copy(string sourcePath, string destinationPath)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var temp = $"{destinationPath}.koru-{Path.GetRandomFileName()}";
        try
        {
            File.Copy(sourcePath, temp, overwrite: false);
            File.Move(temp, destinationPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { /* best effort */ }
            throw;
        }
    }

    public static void Copy(IFileSystem fs, string sourcePath, string destinationPath)
    {
        var dir = fs.Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            fs.Directory.CreateDirectory(dir);

        var temp = $"{destinationPath}.koru-{Path.GetRandomFileName()}";
        try
        {
            fs.File.Copy(sourcePath, temp, overwrite: false);
            fs.File.Move(temp, destinationPath, overwrite: true);
        }
        catch
        {
            try { fs.File.Delete(temp); } catch { /* best effort */ }
            throw;
        }
    }
}
