using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Koru.Cli.Core.Util;

/// Reads, mutates, and writes YAML frontmatter on a markdown file.
/// A frontmatter block is the run from "---\n" on the first line
/// to the next "---\n" line. Everything after is the body.
public static class Frontmatter
{
    private const string Delim = "---";

    /// Parse a markdown source into (frontmatter, body).
    /// If no frontmatter is present, returns ({}, source).
    public static (Dictionary<string, object?> Front, string Body) Parse(string source)
    {
        var normalized = source.Replace("\r\n", "\n");

        if (!normalized.StartsWith(Delim + "\n", StringComparison.Ordinal))
            return (new Dictionary<string, object?>(), normalized);

        var endIdx = normalized.IndexOf("\n" + Delim + "\n", Delim.Length, StringComparison.Ordinal);
        if (endIdx < 0)
            return (new Dictionary<string, object?>(), normalized);

        var yamlBody = normalized.Substring(Delim.Length + 1, endIdx - Delim.Length - 1);
        var rest = normalized[(endIdx + ("\n" + Delim + "\n").Length)..];

        var front = string.IsNullOrWhiteSpace(yamlBody)
            ? new Dictionary<string, object?>()
            : ParseYaml(yamlBody);

        return (front, rest);
    }

    /// Serialise (frontmatter, body) back into a markdown string.
    /// Omits the frontmatter block entirely when the dict is empty.
    public static string Render(Dictionary<string, object?> front, string body)
    {
        if (front.Count == 0)
            return body;

        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(front).TrimEnd('\n');
        return $"{Delim}\n{yaml}\n{Delim}\n{body}";
    }

    /// Replace (or add) the top-level "source" key in the file's frontmatter.
    public static string SetSource(string source, SourceBlock block)
    {
        var (front, body) = Parse(source);
        front["source"] = block.ToMap();
        return Render(front, body);
    }

    private static Dictionary<string, object?> ParseYaml(string yaml)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);

        var result = new Dictionary<string, object?>();
        if (stream.Documents.Count == 0)
            return result;

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
            return result;

        foreach (var (key, value) in root.Children)
        {
            if (key is YamlScalarNode k && k.Value is not null)
                result[k.Value] = NodeToObject(value);
        }
        return result;
    }

    private static object? NodeToObject(YamlNode node) => node switch
    {
        YamlScalarNode s => s.Value,
        YamlMappingNode m => m.Children.ToDictionary(
            kv => ((YamlScalarNode)kv.Key).Value ?? string.Empty,
            kv => NodeToObject(kv.Value)),
        YamlSequenceNode seq => seq.Children.Select(NodeToObject).ToList(),
        _ => null,
    };
}

public sealed record SourceBlock(
    string Repo,
    string Path,
    string Ref,
    string Commit,
    DateTimeOffset ImportedAt)
{
    public Dictionary<string, object?> ToMap() => new()
    {
        ["repo"] = Repo,
        ["path"] = Path,
        ["ref"] = Ref,
        ["commit"] = Commit,
        ["imported_at"] = ImportedAt.ToString("o"),
    };
}
