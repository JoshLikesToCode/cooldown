using System.Text;

namespace Cooldown.Steam;

/// <summary>
/// Minimal parser for Valve's text KeyValues format (.vdf and .acf files).
/// Values are either strings or nested case-insensitive dictionaries.
/// </summary>
public static class Vdf
{
    public static Dictionary<string, object> Parse(string text)
    {
        int i = 0;
        var root = NewNode();
        ParseInto(root, text, ref i, topLevel: true);
        return root;
    }

    public static Dictionary<string, object>? Child(this Dictionary<string, object> node, string key) =>
        node.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

    public static string? Value(this Dictionary<string, object> node, string key) =>
        node.TryGetValue(key, out var v) ? v as string : null;

    private static Dictionary<string, object> NewNode() => new(StringComparer.OrdinalIgnoreCase);

    private static void ParseInto(Dictionary<string, object> node, string s, ref int i, bool topLevel)
    {
        while (true)
        {
            SkipWhitespaceAndComments(s, ref i);
            if (i >= s.Length)
            {
                if (!topLevel) throw new FormatException("Unexpected end of file inside a block.");
                return;
            }
            if (s[i] == '}')
            {
                if (topLevel) throw new FormatException($"Unexpected '}}' at {i}.");
                i++;
                return;
            }

            var key = ReadToken(s, ref i);
            SkipWhitespaceAndComments(s, ref i);

            if (i < s.Length && s[i] == '{')
            {
                i++;
                var child = NewNode();
                ParseInto(child, s, ref i, topLevel: false);
                node[key] = child;
            }
            else
            {
                node[key] = ReadToken(s, ref i);
            }
        }
    }

    private static void SkipWhitespaceAndComments(string s, ref int i)
    {
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i]))
                i++;
            else if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/')
                while (i < s.Length && s[i] != '\n') i++;
            else
                return;
        }
    }

    private static string ReadToken(string s, ref int i)
    {
        if (i >= s.Length) throw new FormatException("Expected a token but reached end of file.");
        var sb = new StringBuilder();

        if (s[i] == '"')
        {
            i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', var c => c });
                }
                else
                {
                    sb.Append(s[i]);
                }
                i++;
            }
            if (i >= s.Length) throw new FormatException("Unterminated string.");
            i++;
            return sb.ToString();
        }

        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] is not ('{' or '}' or '"'))
            sb.Append(s[i++]);
        if (sb.Length == 0) throw new FormatException($"Unexpected '{s[i]}' at {i}.");
        return sb.ToString();
    }
}
