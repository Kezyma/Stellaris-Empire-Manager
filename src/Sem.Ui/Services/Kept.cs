namespace Sem.Ui.Services;

/// <summary>
/// How a designs file is held in a browser's store, which takes text and not bytes.
/// </summary>
/// <remarks>
/// <para>
/// The file was put in as <c>Encoding.UTF8.GetString(bytes)</c> and taken out as text, which is not
/// a round trip. A file that arrived with a byte-order mark kept the mark as a character, and
/// <c>char.IsWhiteSpace('﻿')</c> is false, so the next visit lexed it as a stray token ahead of
/// the first empire. A file that had to be decoded as Latin-1 fared worse: every byte that is not
/// valid UTF-8 came back as U+FFFD, and the player's kept file was quietly corrupted.
/// </para>
/// <para>
/// So the bytes go in as bytes. Text kept by an older version is still read as text — it is opened,
/// and written back in this form the next time the list changes — because the alternative is losing
/// whatever somebody had open when they updated.
/// </para>
/// </remarks>
internal static class Kept
{
    /// <summary>Marks a value as bytes rather than as the text an older version wrote.</summary>
    /// <remarks>
    /// A prefix that cannot begin a designs file: the format's first token is a key, and a key
    /// cannot start with a brace. Anything without it is text from before.
    /// </remarks>
    private const string Prefix = "{sem/b64}";

    /// <summary>Wraps a file for the store.</summary>
    public static string Encode(byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        return Prefix + Convert.ToBase64String(contents);
    }

    /// <summary>The file a stored value holds, or null when the value is text from an older version.</summary>
    public static byte[]? TryDecode(string kept)
    {
        ArgumentNullException.ThrowIfNull(kept);

        if (!kept.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        // A truncated or edited value is treated as nothing kept, which starts an empty file rather
        // than throwing on the way into the app.
        var buffer = new byte[kept.Length];

        return Convert.TryFromBase64String(kept[Prefix.Length..], buffer, out var written)
            ? buffer[..written]
            : null;
    }
}
