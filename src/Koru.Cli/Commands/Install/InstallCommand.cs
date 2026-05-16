using Koru.Contracts;
using Koru.Cli.Core.Abstractions;
using Koru.Cli.Core.Install;
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
        [CommandArgument(0, "<artifact>")]
        public string Artifact { get; set; } = string.Empty;

        [CommandOption("--yes")]
        public bool Yes { get; set; }
    }

    public override int Execute(CommandContext context, InstallSettings settings)
    {
        var matches = _resolver.Resolve(settings.Artifact);

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No matching artifact found.[/]");
            return 1;
        }

        ResolvedArtifact selectedArtifact;
        if (matches.Count == 1)
        {
            selectedArtifact = matches[0];
        }
        else
        {
            var prompt = new SelectionPrompt<ResolvedArtifact>()
                .Title("Multiple matches found. Select one:")
                .AddChoices(matches.ToArray())
                .UseConverter(m => $"{m.RegistryName}: {m.SourcePath}");

            try
            {
                selectedArtifact = AnsiConsole.Prompt(prompt);
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
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
                    .Title("Select scope")
                    .AddChoices(Scope.Global, Scope.ProjectLocal)
                    .UseConverter(s => s == Scope.Global ? "global" : "project-local"));

            mode = AnsiConsole.Prompt(
                new SelectionPrompt<InstallMode>()
                    .Title("Select install mode")
                    .AddChoices(InstallMode.Link, InstallMode.Copy)
                    .UseConverter(m => m == InstallMode.Link ? "link" : "copy"));
        }

        var registry = _configStore.Load().Registries
            .FirstOrDefault(r => r.Name.Equals(selectedArtifact.RegistryName, StringComparison.OrdinalIgnoreCase));
        if (registry is null)
        {
            AnsiConsole.MarkupLine("[red]Registry not found in config.[/]");
            return 1;
        }

        var registryRoot = registry.Path.Contains('~')
            ? _pathExpander.Expand(registry.Path)
            : registry.Path;

        var projectDir = Directory.GetCurrentDirectory();

        try
        {
            var results = _installer.Install(selectedArtifact, scope, mode, projectDir, registryRoot);
            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No plugins returned an install plan for this artifact.[/]");
                return 0;
            }

            var table = new Table();
            table.AddColumn("Plugin");
            table.AddColumn("Destination");
            table.AddColumn("Mode");

            foreach (var entry in results)
            {
                table.AddRow(entry.PluginName, entry.DestinationPath, entry.Mode.ToString().ToLowerInvariant());
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Conflict", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }
    }
}
