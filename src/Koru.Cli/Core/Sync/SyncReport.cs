namespace Koru.Cli.Core.Sync;

public record SyncReport(
    int Created,
    int Updated,
    int Removed,
    int Drifted,
    bool Conflict,
    string? ConflictMessage = null);
