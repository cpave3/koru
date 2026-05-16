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

    /// Recursively copies a directory tree, then atomically swaps it into place.
    /// Stages the new tree at &lt;dest&gt;.koru-&lt;random&gt;, removes the existing destination
    /// (if any) by renaming it to a tombstone, then Directory.Move's the new tree in.
    /// Tombstone is deleted after the swap. Not strictly atomic (two rename ops),
    /// but minimises the window where the destination is missing.
    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var parent = Path.GetDirectoryName(destinationDir);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var staging = $"{destinationDir}.koru-{Path.GetRandomFileName()}";
        var tombstone = $"{destinationDir}.koru-old-{Path.GetRandomFileName()}";

        try
        {
            CopyTree(sourceDir, staging);

            if (Directory.Exists(destinationDir))
            {
                Directory.Move(destinationDir, tombstone);
            }
            else if (File.Exists(destinationDir))
            {
                File.Delete(destinationDir);
            }

            Directory.Move(staging, destinationDir);
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
            throw;
        }
        finally
        {
            try { if (Directory.Exists(tombstone)) Directory.Delete(tombstone, recursive: true); } catch { }
        }
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, target));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, target), overwrite: true);
        }
    }
}
