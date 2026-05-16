using Koru.Contracts;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Install;
using Koru.Cli.Core.Util;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Koru.Cli.Commands.Install;

public class InstallCommand : Command<InstallCommand.InstallSettings>
{
    private readonly IConfigStore _configStore;
    private readonly IArtifactResolver _resolver;
    private readonly IPathExpander _pathExpander;
    private readonly ArtifactInstaller _installer;

    public InstallCommand(
        IConfigStore configStore,
        IArtifactResolver resolver,
        IPathExpander pathExpander,
        ArtifactInstaller installer)
    {
        _configStore = configStore;
        _resolver = resolver;
        _pathExpander = pathExpander;
        _installer = installer;
    }

    public class InstallSettings : CommandSettings
    {
        [CommandArgument(0, "[artifacts]")]
        public string[] Artifacts { get; set; } = Array.Empty<string>();

        [CommandOption("--yes")]
        public bool Yes { get; set; }
    }

    public override int Execute(CommandContext context, InstallSettings settings)
    {
        var selected = settings.Artifacts.Length == 0
            ? PickInteractively()
            : ResolveQueries(settings.Artifacts);

        if (selected is null)
            return 1;

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No artifacts selected.[/]");
            return 0;
        }

        Scope scope;
        InstallMode mode;
        if (settings.Yes)
        {
            scope = Scope.ProjectLocal;
            mode = InstallMode.Copy;
        }
        else
        {
            scope = AnsiConsole.Prompt(
                new SelectionPrompt<Scope>()
                    .Title($"Select scope for [bold]{selected.Count}[/] artifact(s)")
                    .AddChoices(Scope.Global, Scope.ProjectLocal)
                    .UseConverter(s => s == Scope.Global ? "global" : "project-local"));

            mode = AnsiConsole.Prompt(
                new SelectionPrompt<InstallMode>()
                    .Title("Select install mode")
                    .AddChoices(InstallMode.Link, InstallMode.Copy)
                    .UseConverter(m => m == InstallMode.Link ? "link" : "copy"));
        }

        var config = _configStore.Load();
        var projectDir = Directory.GetCurrentDirectory();

        var table = new Table();
        table.AddColumn("Artifact");
        table.AddColumn("Plugin");
        table.AddColumn("Destination");
        table.AddColumn("Mode");

        var hadErrors = false;

        foreach (var artifact in selected)
        {
            var registry = config.Registries.FirstOrDefault(r =>
                r.Name.Equals(artifact.RegistryName, StringComparison.OrdinalIgnoreCase));
            if (registry is null)
            {
                table.AddRow(
                    $"{artifact.RegistryName}/{artifact.SourcePath}",
                    "[red]error[/]",
                    "[red]registry not in config[/]",
                    "-");
                hadErrors = true;
                continue;
            }

            var registryRoot = registry.Path.Contains('~')
                ? _pathExpander.Expand(registry.Path)
                : registry.Path;

            try
            {
                var results = _installer.Install(artifact, scope, mode, projectDir, registryRoot);
                if (results.Count == 0)
                {
                    table.AddRow(
                        $"{artifact.RegistryName}/{artifact.SourcePath}",
                        "-",
                        "[yellow]no plugin claimed this artifact[/]",
                        "-");
                    continue;
                }

                foreach (var entry in results)
                {
                    table.AddRow(
                        $"{artifact.RegistryName}/{artifact.SourcePath}",
                        entry.PluginName,
                        entry.DestinationPath,
                        entry.Mode.ToString().ToLowerInvariant());
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Conflict", StringComparison.OrdinalIgnoreCase))
            {
                table.AddRow(
                    $"{artifact.RegistryName}/{artifact.SourcePath}",
                    "[red]conflict[/]",
                    Markup.Escape(ex.Message),
                    "-");
                hadErrors = true;
            }
        }

        AnsiConsole.Write(table);
        return hadErrors ? 1 : 0;
    }

    private List<ResolvedArtifact>? PickInteractively()
    {
        var all = _resolver.ResolveAll();
        if (all.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No installable artifacts found across registered registries.[/]");
            return new List<ResolvedArtifact>();
        }

        var prompt = new MultiSelectionPrompt<PickNode>()
            .Title("Select artifacts to install ([grey]space toggles (whole groups too), enter confirms; j/k to move[/])")
            .PageSize(25)
            .MoreChoicesText("[grey](more above/below — j/k or arrow keys[/])")
            .InstructionsText("[grey]<space> toggle  ·  <enter> install  ·  j/k move  ·  g/G top/bottom[/]")
            .UseConverter(n => n.Display);

        BuildTree(prompt, all);

        var console = new VimAnsiConsole(AnsiConsole.Console);
        try
        {
            var picked = console.Prompt(prompt);
            return picked.Where(p => p.Artifact is not null).Select(p => p.Artifact!).ToList();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static void BuildTree(MultiSelectionPrompt<PickNode> prompt, IReadOnlyList<ResolvedArtifact> all)
    {
        foreach (var registryGroup in all.GroupBy(a => a.RegistryName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var registryHeader = PickNode.Group($"[bold yellow]{Markup.Escape(registryGroup.Key)}[/]");
            var registryItem = prompt.AddChoice(registryHeader);

            foreach (var dirGroup in registryGroup
                         .GroupBy(a => GetDirectory(a.SourcePath), StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var dirHeader = PickNode.Group($"[cyan]{Markup.Escape(dirGroup.Key.TrimEnd('/'))}/[/]");
                var dirItem = registryItem.AddChild(dirHeader);

                foreach (var artifact in dirGroup.OrderBy(a => a.SourcePath, StringComparer.OrdinalIgnoreCase))
                {
                    dirItem.AddChild(PickNode.Leaf(artifact, Markup.Escape(Path.GetFileName(artifact.SourcePath))));
                }
            }
        }
    }

    private static string GetDirectory(string sourcePath)
    {
        var idx = sourcePath.LastIndexOf('/');
        return idx < 0 ? string.Empty : sourcePath[..idx];
    }

    private sealed class PickNode
    {
        public string Display { get; }
        public ResolvedArtifact? Artifact { get; }
        private PickNode(string display, ResolvedArtifact? artifact)
        {
            Display = display;
            Artifact = artifact;
        }
        public static PickNode Group(string display) => new(display, null);
        public static PickNode Leaf(ResolvedArtifact artifact, string display) => new(display, artifact);
    }

    private List<ResolvedArtifact>? ResolveQueries(string[] queries)
    {
        var picked = new List<ResolvedArtifact>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            var matches = _resolver.Resolve(query);
            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No matching artifact for '{Markup.Escape(query)}'.[/]");
                continue;
            }

            if (HasDistinctRegistries(matches))
            {
                ResolvedArtifact chosen;
                try
                {
                    chosen = AnsiConsole.Prompt(
                        new SelectionPrompt<ResolvedArtifact>()
                            .Title($"Multiple matches for [bold]{Markup.Escape(query)}[/]. Select one:")
                            .AddChoices(matches.ToArray())
                            .UseConverter(m => $"{m.RegistryName}: {m.SourcePath}"));
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                AddUnique(picked, seen, chosen);
            }
            else
            {
                foreach (var match in matches)
                    AddUnique(picked, seen, match);
            }
        }

        return picked;
    }

    private static bool HasDistinctRegistries(IReadOnlyList<ResolvedArtifact> matches)
    {
        if (matches.Count <= 1) return false;
        var first = matches[0].RegistryName;
        return matches.Any(m => !m.RegistryName.Equals(first, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddUnique(List<ResolvedArtifact> picked, HashSet<string> seen, ResolvedArtifact item)
    {
        var key = $"{item.RegistryName}:{item.SourcePath}";
        if (seen.Add(key))
            picked.Add(item);
    }
}
