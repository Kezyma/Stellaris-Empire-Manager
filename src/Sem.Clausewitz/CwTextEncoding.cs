using System.Text;

namespace Sem.Clausewitz;

/// <summary>
/// Decodes and re-encodes Paradox script bytes without ever losing one.
/// </summary>
/// <remarks>
/// Stellaris writes UTF-8, sometimes with a byte order mark and sometimes without. A handful of
/// files contain bytes that are not valid UTF-8 at all. Since the round-trip tests compare bytes,
/// those files fall back to Latin-1, which maps every possible byte to exactly one character and
/// therefore always survives a decode and re-encode intact.
/// </remarks>
public static class CwTextEncoding
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>Decodes script bytes, reporting how they were encoded so writing can match.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes, out CwEncodingInfo encoding)
    {
        var hasBom = bytes.StartsWith(ByteOrderMark);
        var content = hasBom ? bytes[ByteOrderMark.Length..] : bytes;

        try
        {
            var text = StrictUtf8.GetString(content);
            encoding = new CwEncodingInfo(hasBom, IsUtf8: true);
            return text;
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8. Latin-1 is byte-for-byte reversible, so the file still round-trips.
            encoding = new CwEncodingInfo(hasBom, IsUtf8: false);
            return Encoding.Latin1.GetString(content);
        }
    }

    /// <summary>Encodes script text using the encoding it was decoded with.</summary>
    public static byte[] Encode(string text, CwEncodingInfo encoding)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = encoding.IsUtf8 ? StrictUtf8.GetBytes(text) : Encoding.Latin1.GetBytes(text);

        if (!encoding.HasByteOrderMark)
        {
            return body;
        }

        var result = new byte[ByteOrderMark.Length + body.Length];
        ByteOrderMark.CopyTo(result, 0);
        body.CopyTo(result, ByteOrderMark.Length);
        return result;
    }
}

/// <summary>How a script file was encoded.</summary>
/// <param name="HasByteOrderMark">Whether the file began with a UTF-8 byte order mark.</param>
/// <param name="IsUtf8">
/// Whether the content decoded as valid UTF-8. False means Latin-1 was used to preserve bytes
/// that UTF-8 would have rejected.
/// </param>
public readonly record struct CwEncodingInfo(bool HasByteOrderMark, bool IsUtf8)
{
    /// <summary>
    /// What Stellaris writes for the empire designs file: UTF-8 with no byte order mark.
    /// </summary>
    public static CwEncodingInfo Default => new(HasByteOrderMark: false, IsUtf8: true);
}
