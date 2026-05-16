using System.Text.RegularExpressions;
using Koru.Cli.Core.Abstractions;

namespace Koru.Cli.Core.Util;

public class GlobMatcher : IGlobMatcher
{
    public bool Matches(string pattern, string path)
    {
        var regex = GlobToRegex(pattern);
        return regex.IsMatch(path);
    }

    private static Regex GlobToRegex(string pattern)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('^');
        for (int i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*')
            {
                if (CanBeDoubleStar(pattern, i) && AfterDoubleStarIsEndOrSlash(pattern, i))
                {
                    // **/ or ** at end
                    if (i + 2 < pattern.Length && pattern[i + 2] == '/')
                    {
                        // **/
                        sb.Append("(?:.*/)?");
                        i += 2;
                    }
                    else
                    {
                        // ** at end
                        sb.Append(".*");
                        i += 1;
                    }
                }
                else
                {
                    // Single *
                    sb.Append("[^/]*");
                }
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else if (c == '.' || c == '^' || c == '{' || c == '}' || c == '[' || c == ']' ||
                     c == '(' || c == ')' || c == '|' || c == '+' || c == '\\' || c == '$')
            {
                sb.Append('\\');
                sb.Append(c);
            }
            else if (c == '/')
            {
                sb.Append("[/]");
            }
            else
            {
                sb.Append(c);
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.Compiled);
    }

    private static bool CanBeDoubleStar(string pattern, int i)
    {
        return i + 1 < pattern.Length && pattern[i + 1] == '*';
    }

    private static bool AfterDoubleStarIsEndOrSlash(string pattern, int i)
    {
        return i + 2 >= pattern.Length || pattern[i + 2] == '/';
    }
}
