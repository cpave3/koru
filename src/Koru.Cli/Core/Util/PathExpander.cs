using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Util;

public class PathExpander : IPathExpander
{
    public string Expand(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.Equals("~", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.Length == 1)
                return home;
            return Path.Combine(home, path.AsSpan(2).ToString());
        }

        return path;
    }

    public string KoruRoot
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("KORU_HOME");
            if (!string.IsNullOrEmpty(env))
                return env;
            return Expand("~/.koru");
        }
    }

    public string RegistriesRoot => Path.Combine(KoruRoot, "registries");

    public string PluginsRoot => Path.Combine(KoruRoot, "plugins");
}
