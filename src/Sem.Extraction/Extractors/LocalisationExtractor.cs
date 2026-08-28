using System.Text;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads the game's localisation files into a single lookup of key to text.
/// </summary>
/// <remarks>
/// <para>
/// The file names say nothing about what is in them. Origin names live in the federations file,
/// trait and ethic names in one main file and civic and authority names in another, with each
/// content pack scattering more across its own. The game itself loads the lot into one table, so
/// this does the same rather than pretending the layout means anything.
/// </para>
/// <para>
/// The format looks like YAML but is not. Every file starts with a language header, every entry
/// line begins with a single space, the version number after the key is optional, values may
/// contain escaped quotes, and a comment may follow the closing quote.
/// </para>
/// </remarks>
internal static class LocalisationExtractor
{
    /// <summary>Reads one language folder into a flat lookup, with later files winning.</summary>
    public static Dictionary<string, string> Extract(LayeredContent content, string language = "english")
    {
        ArgumentNullException.ThrowIfNull(content);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in content.EnumerateFiles($"localisation/{language}", "*.yml", recursive: true))
        {
            var text = DecodeSkippingByteOrderMark(content.Read(path));

            foreach (var line in text.Split('\n'))
            {
                if (TryParseLine(line, out var key, out var value))
                {
                    entries[key] = value;
                }
            }
        }

        return entries;
    }

    private static string DecodeSkippingByteOrderMark(byte[] bytes)
    {
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return Encoding.UTF8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
    }

    /// <summary>
    /// Parses one entry line. Returns false for blank lines, comments, and the language header.
    /// </summary>
    private static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var span = line.AsSpan().TrimStart();
        if (span.IsEmpty || span[0] == '#')
        {
            return false;
        }

        var colon = span.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var name = span[..colon].Trim();
        if (name.IsEmpty || name.StartsWith("l_"))
        {
            // The language header, which is a declaration rather than an entry.
            return false;
        }

        // An optional version number sits between the colon and the value.
        var rest = span[(colon + 1)..];
        var index = 0;
        while (index < rest.Length && char.IsAsciiDigit(rest[index]))
        {
            index++;
        }

        while (index < rest.Length && char.IsWhiteSpace(rest[index]))
        {
            index++;
        }

        if (index >= rest.Length || rest[index] != '"')
        {
            return false;
        }

        var builder = new StringBuilder(rest.Length - index);

        for (var i = index + 1; i < rest.Length; i++)
        {
            var c = rest[i];

            if (c == '\\' && i + 1 < rest.Length)
            {
                // Values may contain escaped quotes, which a naive scan to the last quote breaks on.
                builder.Append(rest[i + 1] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    var escaped => escaped,
                });

                i++;
                continue;
            }

            if (c == '"')
            {
                key = name.ToString();
                value = builder.ToString();
                return true;
            }

            builder.Append(c);
        }

        // An unterminated value; take what there was rather than dropping the entry.
        key = name.ToString();
        value = builder.ToString().TrimEnd('\r');
        return true;
    }
}
