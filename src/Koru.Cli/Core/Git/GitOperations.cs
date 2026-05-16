using System.Text.RegularExpressions;
using Koru.Cli.Core.Abstractions;
using LibGit2Sharp;

namespace Koru.Cli.Core.Git;

public class GitOperations : IGitOps
{
    public void Init(string path)
    {
        Repository.Init(path, isBare: false);
        // Set default branch to main via HEAD symref
        using (var repo = new Repository(path))
        {
            // No commits yet; LibGit2Sharp doesn't expose a clean way to rename
            // an unborn branch. We'll record the intended default branch in git config.
            repo.Config.Set("init.defaultBranch", "main");
        }
    }

    public void Clone(string remote, string path)
    {
        Repository.Clone(remote, path);
    }

    public IReadOnlyList<string> Status(string path)
    {
        using var repo = new Repository(path);
        var statuses = new List<string>();
        foreach (var item in repo.RetrieveStatus())
        {
            // Collect changed file paths
            statuses.Add(item.FilePath);
        }
        return statuses;
    }

    public bool IsClean(string path)
    {
        using var repo = new Repository(path);
        var status = repo.RetrieveStatus();
        return !status.IsDirty;
    }

    public void Pull(string path)
    {
        using var repo = new Repository(path);

        var remoteName = repo.Head.RemoteName ?? "origin";
        var remote = repo.Network.Remotes[remoteName];
        if (remote is null)
            throw new InvalidOperationException($"No remote '{remoteName}' configured.");

        // Fetch
        var refSpecs = remote.FetchRefSpecs.Select(rs => rs.Specification).ToList();
        LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions(), string.Empty);

        // Determine merge target
        var tracked = repo.Head.TrackedBranch;
        if (tracked is null)
            throw new InvalidOperationException("No tracked branch to pull from.");

        var mergeResult = repo.Merge(tracked.Tip, GetSignature(repo), new MergeOptions());
        if (mergeResult.Status == MergeStatus.Conflicts)
        {
            var conflictFiles = string.Join(", ", repo.Index.Conflicts.Select(c => c.Ancestor?.Path ?? c.Ours?.Path ?? c.Theirs?.Path));
            throw new InvalidOperationException($"Merge conflicts detected: {conflictFiles}");
        }
    }

    public void Push(string path)
    {
        using var repo = new Repository(path);
        var remote = repo.Network.Remotes["origin"];
        if (remote is null)
            throw new InvalidOperationException("No 'origin' remote configured.");

        var pushRefSpec = $"refs/heads/{repo.Head.FriendlyName}:refs/heads/{repo.Head.FriendlyName}";
        repo.Network.Push(remote, pushRefSpec, new PushOptions());
    }

    public void SetRemote(string path, string remote)
    {
        using var repo = new Repository(path);
        var existing = repo.Network.Remotes["origin"];
        if (existing is not null)
            repo.Network.Remotes.Update("origin", r => r.Url = remote);
        else
            repo.Network.Remotes.Add("origin", remote);
    }

    public IReadOnlyList<string> ListTrackedFiles(string path)
    {
        using var repo = new Repository(path);
        var files = new List<string>();
        if (repo.Head.Tip is null)
            return files;

        var tree = repo.Head.Tip.Tree;
        CollectTreePaths(tree, "", files);
        return files;
    }

    public void CommitAll(string path, string message)
    {
        using var repo = new Repository(path);
        LibGit2Sharp.Commands.Stage(repo, "*");

        var sig = GetSignature(repo);
        repo.Commit(message, sig, sig);
    }

    private static Signature GetSignature(Repository repo)
    {
        var configName = repo.Config.Get<string>("user.name")?.Value;
        var configEmail = repo.Config.Get<string>("user.email")?.Value;

        var name = configName ?? "Koru";
        var email = configEmail ?? "koru@local";

        return new Signature(name, email, DateTimeOffset.UtcNow);
    }

    private static void CollectTreePaths(Tree tree, string prefix, List<string> files)
    {
        foreach (var entry in tree)
        {
            var relPath = string.IsNullOrEmpty(prefix) ? entry.Name : $"{prefix}/{entry.Name}";
            if (entry.TargetType == TreeEntryTargetType.Tree)
                CollectTreePaths(entry.Target.Peel<Tree>(), relPath, files);
            else
                files.Add(relPath.Replace("\\", "/"));
        }
    }
}
