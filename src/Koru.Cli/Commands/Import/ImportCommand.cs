using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Models;
using Koru.Cli.Core.Util;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Koru.Cli.Commands.Import;

public class ImportCommand : Command<ImportCommand.ImportSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IGitOps _gitOps;
    private readonly IPathExpander _pathExpander;

    public ImportCommand(IConfigStore configStore, IGitOps gitOps, IPathExpander pathExpander)
    {
        _configStore = configStore;
        _gitOps = gitOps;
        _pathExpander = pathExpander;
    }

    public class ImportSettings : CommandSettings
    {
        [CommandArgument(0, "<git-url>")]
        [Description("Source git URL (https, ssh, or file://). Owner/repo shorthand is NOT supported here — pass a full URL.")]
        public string GitUrl { get; set; } = string.Empty;

        [CommandArgument(1, "[subpath]")]
        [Description("Path inside the source repo: a .md file, or a directory containing SKILL.md. Omit for an interactive picker.")]
        public string? Subpath { get; set; }

        [CommandOption("--name <local-name>")]
        [Description("Override the artifact name used in the target registry. Defaults to the source basename.")]
        public string? Name { get; set; }

        [CommandOption("--registry <registry>")]
        [Description("Target registry name. Defaults to the default registry; prompted if unset and multiple exist.")]
        public string? Registry { get; set; }

        [CommandOption("--force")]
        [Description("Overwrite an existing same-named artifact in the target registry.")]
        public bool Force { get; set; }

        [CommandOption("--yes")]
        [Description("Skip prompts.")]
        public bool Yes { get; set; }
    }

    public override int Execute(CommandContext context, ImportSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.GitUrl))
        {
            AnsiConsole.MarkupLine("[red]git URL is required.[/]");
            return 1;
        }

        var config = _configStore.Load();
        var targetRegistry = ResolveTargetRegistry(config, settings);
        if (targetRegistry is null) return 1;

        var registryPath = targetRegistry.Path.Contains('~')
            ? _pathExpander.Expand(targetRegistry.Path)
            : targetRegistry.Path;

        if (!Directory.Exists(registryPath))
        {
            AnsiConsole.MarkupLine($"[red]Registry directory not found: {Markup.Escape(registryPath)}[/]");
            return 1;
        }

        if (!_gitOps.IsClean(registryPath) && !settings.Yes)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Target registry '{targetRegistry.Name}' has uncommitted changes.[/]");
            if (!AnsiConsole.Confirm("Continue anyway?", defaultValue: false))
                return 1;
        }

        var clonePath = Path.Combine(Path.GetTempPath(), $"koru-import-{Guid.NewGuid():N}");
        try
        {
            AnsiConsole.MarkupLine($"[grey]Cloning[/] {Markup.Escape(settings.GitUrl)} ...");
            _gitOps.Clone(settings.GitUrl, clonePath);

            var headSha = _gitOps.GetHeadSha(clonePath);
            var trackedFiles = _gitOps.ListTrackedFiles(clonePath);
            var sourceArtifacts = ArtifactDiscovery.Discover(trackedFiles);

            var selected = settings.Subpath is null
                ? PickInteractively(sourceArtifacts, settings.Yes)
                : SelectByPath(sourceArtifacts, settings.Subpath);

            if (selected is null || selected.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No artifacts to import.[/]");
                return 1;
            }

            if (selected.Count > 1 && !string.IsNullOrEmpty(settings.Name))
            {
                AnsiConsole.MarkupLine("[red]--name can only be used with a single source artifact.[/]");
                return 1;
            }

            var importedAt = DateTimeOffset.UtcNow;
            var summary = new Table();
            summary.AddColumn("Imported");
            summary.AddColumn("From");

            foreach (var artifact in selected)
            {
                var localName = !string.IsNullOrEmpty(settings.Name)
                    ? settings.Name
                    : DeriveLocalName(artifact);

                var sourceBlock = new SourceBlock(
                    Repo: settings.GitUrl,
                    Path: artifact.Path,
                    Ref: "HEAD",
                    Commit: headSha,
                    ImportedAt: importedAt);

                var (registryRelative, error) = ImportOne(
                    clonePath: clonePath,
                    artifact: artifact,
                    registryPath: registryPath,
                    localName: localName,
                    sourceBlock: sourceBlock,
                    force: settings.Force);

                if (error is not null)
                {
                    AnsiConsole.MarkupLine($"[red]Skipping {Markup.Escape(artifact.Path)}: {Markup.Escape(error)}[/]");
                    continue;
                }

                summary.AddRow(Markup.Escape(registryRelative!), Markup.Escape($"{settings.GitUrl}#{artifact.Path}@{headSha[..7]}"));
            }

            AnsiConsole.Write(summary);

            if (_gitOps.IsClean(registryPath))
            {
                AnsiConsole.MarkupLine("[yellow]Nothing changed in the registry — no commit made.[/]");
                return 0;
            }

            var commitMsg = selected.Count == 1
                ? $"import: {DeriveLocalName(selected[0])} from {settings.GitUrl}@{headSha[..7]}"
                : $"import: {selected.Count} skills from {settings.GitUrl}@{headSha[..7]}";
            _gitOps.CommitAll(registryPath, commitMsg);

            AnsiConsole.MarkupLine($"[green]Committed to {Markup.Escape(targetRegistry.Name)}.[/]");
            return 0;
        }
        finally
        {
            TryDelete(clonePath);
        }
    }

    private RegistryEntry? ResolveTargetRegistry(CliConfig config, ImportSettings settings)
    {
        if (config.Registries.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No registries are registered. Run `koru init` or `koru use` first.[/]");
            return null;
        }

        if (!string.IsNullOrEmpty(settings.Registry))
        {
            var match = config.Registries.FirstOrDefault(r =>
                r.Name.Equals(settings.Registry, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                AnsiConsole.MarkupLine($"[red]Registry '{settings.Registry}' is not registered.[/]");
                return null;
            }
            return match;
        }

        if (!string.IsNullOrEmpty(config.DefaultRegistry))
        {
            var defaultMatch = config.Registries.FirstOrDefault(r =>
                r.Name.Equals(config.DefaultRegistry, StringComparison.OrdinalIgnoreCase));
            if (defaultMatch is not null)
                return defaultMatch;
        }

        if (config.Registries.Count == 1)
            return config.Registries[0];

        if (settings.Yes)
        {
            AnsiConsole.MarkupLine("[red]Multiple registries and no --registry given.[/]");
            return null;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<RegistryEntry>()
                .Title("Import into which registry?")
                .AddChoices(config.Registries)
                .UseConverter(r => $"{r.Name} ({r.Path})"));
    }

    private static List<ArtifactDiscovery.DiscoveredArtifact>? SelectByPath(
        IReadOnlyList<ArtifactDiscovery.DiscoveredArtifact> all,
        string subpath)
    {
        var normalized = subpath.Replace('\\', '/').TrimEnd('/');
        var exact = all.FirstOrDefault(a =>
            a.Path.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return new List<ArtifactDiscovery.DiscoveredArtifact> { exact };

        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var withMd = all.FirstOrDefault(a =>
                a.Path.Equals(normalized + ".md", StringComparison.OrdinalIgnoreCase));
            if (withMd is not null)
                return new List<ArtifactDiscovery.DiscoveredArtifact> { withMd };
        }

        AnsiConsole.MarkupLine($"[red]No artifact at '{Markup.Escape(subpath)}' in the source repo.[/]");
        return null;
    }

    private static List<ArtifactDiscovery.DiscoveredArtifact>? PickInteractively(
        IReadOnlyList<ArtifactDiscovery.DiscoveredArtifact> all,
        bool yes)
    {
        if (yes)
        {
            AnsiConsole.MarkupLine("[red]Subpath is required with --yes.[/]");
            return null;
        }

        if (all.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Source repo contains no .md files or SKILL.md directories.[/]");
            return new List<ArtifactDiscovery.DiscoveredArtifact>();
        }

        var prompt = new MultiSelectionPrompt<ArtifactDiscovery.DiscoveredArtifact>()
            .Title("Select source artifacts to import ([grey]space toggles, enter confirms[/])")
            .PageSize(25)
            .MoreChoicesText("[grey](more above/below)[/]")
            .InstructionsText("[grey]<space> toggle · <enter> import · j/k move[/]")
            .UseConverter(a => a.IsDirectory ? $"[cyan]{Markup.Escape(a.Path)}/[/]" : Markup.Escape(a.Path));

        foreach (var a in all.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            prompt.AddChoice(a);

        return AnsiConsole.Prompt(prompt).ToList();
    }

    private static string DeriveLocalName(ArtifactDiscovery.DiscoveredArtifact artifact)
    {
        if (artifact.IsDirectory)
        {
            var idx = artifact.Path.LastIndexOf('/');
            return idx < 0 ? artifact.Path : artifact.Path[(idx + 1)..];
        }
        return Path.GetFileNameWithoutExtension(artifact.Path);
    }

    private static (string? RegistryRelative, string? Error) ImportOne(
        string clonePath,
        ArtifactDiscovery.DiscoveredArtifact artifact,
        string registryPath,
        string localName,
        SourceBlock sourceBlock,
        bool force)
    {
        if (artifact.IsDirectory)
        {
            var targetDir = Path.Combine(registryPath, "core", "skills", localName);
            if (Directory.Exists(targetDir) && !force)
                return (null, $"core/skills/{localName}/ already exists (use --force to overwrite)");

            var sourceDir = Path.Combine(clonePath, artifact.Path);
            if (!Directory.Exists(sourceDir))
                return (null, "source directory missing in clone");

            AtomicFile.CopyDirectory(sourceDir, targetDir);

            var skillMdPath = Path.Combine(targetDir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                var body = File.ReadAllText(skillMdPath);
                var stamped = Frontmatter.SetSource(body, sourceBlock);
                File.WriteAllText(skillMdPath, stamped);
            }

            return ($"core/skills/{localName}/", null);
        }
        else
        {
            var targetFile = Path.Combine(registryPath, "core", "skills", localName + ".md");
            if (File.Exists(targetFile) && !force)
                return (null, $"core/skills/{localName}.md already exists (use --force to overwrite)");

            var sourceFile = Path.Combine(clonePath, artifact.Path);
            if (!File.Exists(sourceFile))
                return (null, "source file missing in clone");

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            var body = File.ReadAllText(sourceFile);
            var stamped = Frontmatter.SetSource(body, sourceBlock);
            File.WriteAllText(targetFile, stamped);

            return ($"core/skills/{localName}.md", null);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
