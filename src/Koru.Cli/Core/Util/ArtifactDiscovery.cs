namespace Koru.Cli.Core.Util;

/// Group a list of tracked file paths into discrete artifacts.
///
/// Rules:
///   - Any directory containing a SKILL.md file becomes a directory-artifact.
///     Every tracked file under that directory belongs to that artifact.
///   - A *.md file outside any SKILL.md-containing directory is a single-file artifact.
///   - Files that are neither *.md nor inside a SKILL.md directory are ignored.
///   - Most-specific match wins: if /foo/SKILL.md and /foo/bar/SKILL.md both exist,
///     files under /foo/bar belong to bar; other files under /foo belong to foo.
public static class ArtifactDiscovery
{
    public sealed record DiscoveredArtifact(
        string Path,
        bool IsDirectory,
        IReadOnlyList<string> Files);

    public static IReadOnlyList<DiscoveredArtifact> Discover(IEnumerable<string> trackedPaths)
    {
        var normalized = trackedPaths
            .Select(p => p.Replace('\\', '/'))
            .Where(IsCandidate)
            .ToList();

        var skillRoots = normalized
            .Where(p => IsSkillFile(p))
            .Select(GetParent)
            .Where(dir => dir.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(d => d.Length)
            .ToList();

        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var singleFiles = new List<string>();

        foreach (var path in normalized)
        {
            var owner = FindOwningRoot(path, skillRoots);
            if (owner is not null)
            {
                if (!grouped.TryGetValue(owner, out var list))
                {
                    list = new List<string>();
                    grouped[owner] = list;
                }
                list.Add(path);
            }
            else if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                singleFiles.Add(path);
            }
        }

        var results = new List<DiscoveredArtifact>();

        foreach (var (dir, files) in grouped)
        {
            files.Sort(StringComparer.OrdinalIgnoreCase);
            results.Add(new DiscoveredArtifact(dir, IsDirectory: true, files));
        }

        foreach (var file in singleFiles)
        {
            results.Add(new DiscoveredArtifact(file, IsDirectory: false, new[] { file }));
        }

        results.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private static bool IsCandidate(string path)
    {
        if (path.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Equals("registry.yaml", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Equals("state.json", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool IsSkillFile(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetParent(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? string.Empty : path[..idx];
    }

    private static string? FindOwningRoot(string path, IReadOnlyList<string> rootsLongestFirst)
    {
        foreach (var root in rootsLongestFirst)
        {
            if (path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                return root;
        }
        return null;
    }
}
