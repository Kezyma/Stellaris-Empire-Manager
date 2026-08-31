using System.IO.Compression;
using System.Text;
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
/// <para>
/// Three things are done to it, in this order, and each is reversed on the way back. It is written
/// with no indentation, since nobody reads it. Runs of text that every design contains are replaced
/// with a two-byte code from a fixed table — the compressor only sees the second and later
/// occurrences of a run as repetition, so the first <c>variables=</c> in a design costs ten literal
/// bytes and this is what removes them. Then it is compressed and made URL-safe. Nothing in the
/// chain looks at what any field means, so the guarantee is unchanged: whatever goes in comes back.
/// </para>
/// <para>
/// Measured over eighteen hand-built empires, that took the average link from 1,129 characters to
/// 677 and the longest from 1,258 to 938 — a whole shared address, site and all, now fits inside a
/// thousand characters. The table is what does most of it: compression alone reached 986.
/// </para>
/// </remarks>
public static class DesignLink
{
    /// <summary>The query parameter a shared design travels in.</summary>
    public const string Parameter = "d";

    /// <summary>
    /// What the first byte of a packed design says about the rest of it.
    /// </summary>
    /// <remarks>
    /// One byte spent so that the packing can be changed later without every link already sent
    /// becoming an empire nobody can open. <see cref="Tokens"/> is part of what this number
    /// describes: changing that table changes the format, and must come with a new number here.
    /// </remarks>
    private const byte Version = 1;

    /// <summary>
    /// Marks a coded run. Never occurs in the text itself, which is why it can mark anything.
    /// </summary>
    private const byte Marker = 0;

    /// <summary>
    /// Follows the marker where the text really did contain a zero byte, so that even a design
    /// carrying one comes back intact rather than being read as a code.
    /// </summary>
    private const byte Escaped = 0xFF;

    /// <summary>
    /// The runs of text a design is mostly made of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All of it is this format's own punctuation and field names rather than the game's vocabulary.
    /// That is deliberate. Adding the game's trait, civic and origin keys as well was measured and
    /// saved a further two and a half per cent, because a design names only fifteen or so of them —
    /// not worth a table that a game patch could quietly make wrong.
    /// </para>
    /// <para>
    /// Longest first: a run is coded by the first entry that matches, so the order is what makes
    /// that the longest match. Anything not in the table is written out as itself, so the table
    /// being incomplete costs length and nothing else. It may hold 254 entries at the most, since
    /// an entry is named by one byte and <see cref="Escaped"/> has taken the last of them.
    /// </para>
    /// </remarks>
    private static readonly byte[][] Tokens =
    [
        .. new[]
        {
            "ignore_portrait_duplication=no\n",
            "city_graphical_culture=\"",
            "variables={\n{\nkey=\"",
            "spawn_enabled=always\n",
            "graphical_culture=\"",
            "ethic=\"ethic_fanatic_",
            "spawn_as_fallen=no\n",
            "species_adjective",
            "room=\"personality_",
            "\"\nvalue={\nkey=\"",
            "origin=\"origin_",
            "planet_class=\"pc_",
            "\"\n}\n}\n{\nkey=\"",
            "authority=\"auth_",
            "government=\"gov_",
            "initializer=\"\"\n",
            "gender=not_set\n",
            "evolution_mask=0\n",
            "species_plural",
            "leader_class=\"",
            "leader_trait_",
            "is_nomadic=no\n",
            "gender=female\n",
            "species_name",
            "ship_prefix",
            "planet_name",
            "system_name",
            "empire_flag",
            "%ADJECTIVE%",
            "%LEADER_1%",
            "gender=male\n",
            "attachment=0\n",
            "trait=\"trait_",
            "ethic=\"ethic_",
            "literal=yes\n",
            "\"\n}\n}\n}\n",
            "graphical_culture",
            "background",
            "full_names",
            "name_list=\"",
            "file=\"flag_",
            "adjective",
            "category=\"",
            "portrait=\"",
            "ship_size=\"",
            "civics={\n",
            "texture=",
            "clothes=",
            "colors",
            "civic_",
            "class=\"",
            "_CHR_",
            "SPEC_",
            "ruler",
            "icon",
            "key=\"",
            "\"\n}\n",
            "={\n",
            "\n}\n",
        }.OrderByDescending(token => token.Length).Select(Encoding.UTF8.GetBytes),
    ];

    /// <summary>Packs a design into a string that can be put in a URL.</summary>
    public static string Encode(EmpireDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        var document = new CwDocument();

        document.Add(CwNode.Assignment(design.Key, design.Block.Clone(), quoteKey: true));

        using var packed = new MemoryStream();
        packed.WriteByte(Version);

        // Deflate rather than anything stronger because this runs in a browser, where the runtime
        // has no Brotli: it is a native library that WebAssembly does not link. Measured, Brotli
        // would have saved a further ten per cent, and it is not on offer.
        using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(Tokenise(document.ToBytes(CwWriteOptions.Compact)));
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

            if (packed.Length < 2 || packed[0] != Version)
            {
                return null;
            }

            using var source = new MemoryStream(packed, 1, packed.Length - 1);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);
            using var coded = new MemoryStream();

            deflate.CopyTo(coded);

            return EmpireDesignsFile.Load(Expand(coded.ToArray())).Designs.FirstOrDefault();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or CwSyntaxException)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces every run the table knows with the two bytes that stand for it.
    /// </summary>
    /// <remarks>
    /// A single pass taking the first entry that matches, which is the longest because the table is
    /// ordered that way. A coded run cannot be matched into afterwards, since the marker is not a
    /// byte any entry contains, so the pass cannot code the same text twice.
    /// </remarks>
    private static byte[] Tokenise(byte[] text)
    {
        var coded = new List<byte>(text.Length);

        for (var i = 0; i < text.Length;)
        {
            var match = -1;

            for (var candidate = 0; candidate < Tokens.Length && match < 0; candidate++)
            {
                if (Matches(text, i, Tokens[candidate]))
                {
                    match = candidate;
                }
            }

            if (match >= 0)
            {
                coded.Add(Marker);
                coded.Add((byte)(match + 1));
                i += Tokens[match].Length;
            }
            else
            {
                if (text[i] == Marker)
                {
                    coded.Add(Marker);
                    coded.Add(Escaped);
                }
                else
                {
                    coded.Add(text[i]);
                }

                i++;
            }
        }

        return [.. coded];
    }

    /// <summary>Puts back what <see cref="Tokenise"/> took out.</summary>
    private static byte[] Expand(byte[] coded)
    {
        var text = new List<byte>(coded.Length * 2);

        for (var i = 0; i < coded.Length; i++)
        {
            if (coded[i] != Marker)
            {
                text.Add(coded[i]);
                continue;
            }

            // A marker with nothing after it is a truncated link, which is no design rather than an
            // error — the same treatment the rest of a damaged link gets.
            if (++i >= coded.Length)
            {
                throw new InvalidDataException("The link ends part way through a coded run.");
            }

            if (coded[i] == Escaped)
            {
                text.Add(Marker);
            }
            else if (coded[i] - 1 is var index && index < Tokens.Length)
            {
                text.AddRange(Tokens[index]);
            }
            else
            {
                throw new InvalidDataException($"The link uses a run this version does not know ({coded[i]}).");
            }
        }

        return [.. text];
    }

    private static bool Matches(byte[] text, int at, byte[] token)
    {
        if (at + token.Length > text.Length)
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            if (text[at + i] != token[i])
            {
                return false;
            }
        }

        return true;
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
