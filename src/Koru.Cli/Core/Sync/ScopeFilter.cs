using Koru.Contracts;

namespace Koru.Cli.Core.Sync;

public record ScopeFilter
{
    public bool IncludeGlobal { get; init; }
    public bool IncludeProjectLocal { get; init; }
    public string? ProjectPath { get; init; }

    public bool Includes(Scope scope) => scope == Scope.Global ? IncludeGlobal : IncludeProjectLocal;

    public static ScopeFilter All() => new() { IncludeGlobal = true, IncludeProjectLocal = true };
    public static ScopeFilter GlobalOnly() => new() { IncludeGlobal = true, IncludeProjectLocal = false };
    public static ScopeFilter ProjectOnly(string? projectPath = null) => new()
    {
        IncludeGlobal = false,
        IncludeProjectLocal = true,
        ProjectPath = projectPath
    };
}
