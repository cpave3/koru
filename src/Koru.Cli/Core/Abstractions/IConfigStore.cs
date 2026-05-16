using Koru.Cli.Core.Models;

namespace Koru.Cli.Core.Abstractions;

public interface IConfigStore
{
    CliConfig Load();
    void Save(CliConfig config);
    string ConfigPath { get; }
}
