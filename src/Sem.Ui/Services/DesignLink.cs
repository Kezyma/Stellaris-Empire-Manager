using System.IO.Compression;
using Sem.Clausewitz;
using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Packs a whole empire into something that fits in a link.
/// </summary>
/// <remarks>
/// <para>
/// What a design amounts to is not a handful of choices. It is a species with three names that may
/// each be a template with its own nested variables, a flag with three colours and two images, a
/// ruler with a title, a homeworld, a starting system, and every trait, ethic and civic. Encoding
/// those field by field would need a parser per field and would quietly drop whatever was added
/// next.
/// </para>
/// <para>
/// So the design travels as itself: the block the file holds, written out, compressed and made safe
/// for a URL. It is opaque, and it is exact — a design that goes through it comes back the same
/// design, and nothing has to be taught about new fields.
/// </para>
/// </remarks>
public static class DesignLink
{
    /// <summary>The query parameter a shared design travels in.</summary>
    public const string Parameter = "d";

    /// <summary>Packs a design into a string that can be put in a URL.</summary>
    public static string Encode(EmpireDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        // The key is written between quotation marks, so one inside it would close them early and
        // the link would not parse back. The game's own files escape it the same way.
        var document = new CwDocument();

        document.Add(CwNode.Assignment(
            design.Key.Replace("\"", "\\\"", StringComparison.Ordinal),
            design.Block.Clone(),
            quoteKey: true));

        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(document.ToBytes());
        }

        return Base64Url(packed.ToArray());
    }

    /// <summary>
    /// Unpacks a design from a link, or returns nothing when the link does not hold one.
    /// </summary>
    /// <remarks>
    /// A link can be truncated by a chat client, mistyped, or simply old. None of those should throw
    /// on the way into a page, so anything that will not unpack is treated as no design at all.
    /// </remarks>
    public static EmpireDesign? Decode(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            var packed = Convert.FromBase64String(Unpad(encoded));

            using var source = new MemoryStream(packed);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);
            using var text = new MemoryStream();

            deflate.CopyTo(text);

            return EmpireDesignsFile.Load(text.ToArray()).Designs.FirstOrDefault();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or CwSyntaxException)
        {
            return null;
        }
    }

    /// <summary>
    /// Base64 as a URL will carry it: no plus, no slash, and no padding to be stripped by a chat
    /// client that thinks a trailing equals sign is punctuation.
    /// </summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Unpad(string encoded)
    {
        var restored = encoded.Replace('-', '+').Replace('_', '/');
        return restored.PadRight(restored.Length + ((4 - (restored.Length % 4)) % 4), '=');
    }
}
