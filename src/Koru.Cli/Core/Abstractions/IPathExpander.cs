namespace Koru.Cli.Core.Abstractions;

public interface IPathExpander
{
    string Expand(string path);
    string KoruRoot { get; }
    string RegistriesRoot { get; }
    string PluginsRoot { get; }
}
