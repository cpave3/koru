namespace Koru.Contracts;

public record Artifact(string Path, string RegistryRoot, bool IsDirectory = false);
