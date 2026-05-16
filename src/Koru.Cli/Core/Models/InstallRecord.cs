using Koru.Contracts;

namespace Koru.Cli.Core.Models;

public class InstallRecord
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public InstallMode InstallMode { get; set; }
    public string Plugin { get; set; } = string.Empty;
    public string SourceChecksum { get; set; } = string.Empty;
    public string? InstalledChecksum { get; set; }
    public string Registry { get; set; } = string.Empty;
}
