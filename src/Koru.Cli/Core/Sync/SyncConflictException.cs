namespace Koru.Cli.Core.Sync;

public class SyncConflictException : Exception
{
    public SyncConflictException(string sourcePath, string plugin1, string plugin2, string destinationPath)
        : base($"Conflict detected for artifact '{sourcePath}': plugins '{plugin1}' and '{plugin2}' both target '{destinationPath}'.")
    {
        SourcePath = sourcePath;
        Plugin1 = plugin1;
        Plugin2 = plugin2;
        DestinationPath = destinationPath;
    }

    public string SourcePath { get; }
    public string Plugin1 { get; }
    public string Plugin2 { get; }
    public string DestinationPath { get; }
}
