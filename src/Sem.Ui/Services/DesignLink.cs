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
/// Measured over eighteen hand-built empires, that took the longest link from 1,258 characters to
/// 938. It is superseded by <see cref="DesignLinkV2"/>, which reaches a median of 372 by writing
/// the design as a tree rather than as text — but every word above still describes what a link
/// stamped <c>0x01</c> holds, and those are still out there.
/// </para>
/// <para>
/// The address around it is <c>…/e/&lt;payload&gt;</c>, 52 characters before the payload starts.
/// <c>…/designer?d=</c> was nine longer and is still read.
/// </para>
/// </remarks>
public static class DesignLink
{
    /// <summary>The query parameter a shared design travels in.</summary>
    public const string Parameter = "d";

    /// <summary>
    /// The first packing: compact text with its common runs coded, then compressed.
    /// </summary>
    /// <remarks>
    /// Superseded by <see cref="Packed"/> and still read, which is what the version byte was spent
    /// on. Every link shared while this was the current format opens exactly as it did; nothing
    /// below this line may change, because a link somebody sent last year is the test.
    /// </remarks>
    private const byte Coded = 1;

    /// <summary>
    /// The second packing: the design as a tree of nodes, against a frozen vocabulary.
    /// </summary>
    /// <remarks>
    /// One byte spent so that the packing can be changed without every link already sent becoming
    /// an empire nobody can open. <see cref="Tokens"/> and <see cref="LinkDictionary"/> are both
    /// part of what this number describes: changing either changes the format, and must come with a
    /// new number here.
    /// </remarks>
    private const byte Packed = 2;

    /// <summary>Set in the byte after <see cref="Packed"/> when the body is compressed.</summary>
    /// <remarks>
    /// A packed design is mostly indices, which are already dense — compressing one usually makes
    /// it a few bytes longer. So both are tried and the shorter is sent, which makes the
    /// compression never a cost. It earns its place on the empires that carry a biography, where
    /// the text at the end is prose.
    /// </remarks>
    private const byte Compressed = 1;

    /// <summary>
    /// How far a link may expand on the way in.
    /// </summary>
    /// <remarks>
    /// Deflate reaches about 1,032 to 1 and the run table can double again on top, so an address of
    /// ordinary length can name hundreds of megabytes. Nothing in a design comes near a megabyte —
    /// the largest in the corpus is under three kilobytes — so anything past this is not a design
    /// that got long, and is refused like any other link that will not unpack.
    /// </remarks>
    private const int Ceiling = 1 << 20;

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
    /// <summary>
    /// Where an empire lives, relative to wherever the app is served from.
    /// </summary>
    /// <remarks>
    /// A path rather than a query - nine characters shorter, and with no question mark or equals
    /// sign for a chat client to decide is punctuation. The query is still read, and always will
    /// be: it is what every link shared before this carries, and a link somebody sent is not ours
    /// to expire.
    /// </remarks>
    public static string Address(string encoded) => $"e/{encoded}";

    public static string Encode(EmpireDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        var entry = new CwBlock();
        entry.Add(CwNode.Assignment(design.Key, design.Block.Clone(), quoteKey: true));

        var body = DesignLinkV2.Pack(entry);
        var squashed = Deflate(body);

        var packed = new byte[2 + Math.Min(body.Length, squashed.Length)];
        packed[0] = Packed;

        if (squashed.Length < body.Length)
        {
            packed[1] = Compressed;
            squashed.CopyTo(packed, 2);
        }
        else
        {
            body.CopyTo(packed, 2);
        }

        return Base64Url(packed);
    }

    /// <remarks>
    /// Deflate rather than anything stronger because this runs in a browser, where the runtime has
    /// no Brotli: it is a native library that WebAssembly does not link. Measured, Brotli would
    /// have saved a further ten per cent, and it is not on offer.
    /// </remarks>
    private static byte[] Deflate(byte[] body)
    {
        using var squashed = new MemoryStream();

        using (var deflate = new DeflateStream(squashed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(body);
        }

        return squashed.ToArray();
    }

    /// <summary>Expands a compressed body, refusing one that will not stop.</summary>
    private static byte[] Inflate(byte[] packed, int from)
    {
        using var source = new MemoryStream(packed, from, packed.Length - from);
        using var deflate = new DeflateStream(source, CompressionMode.Decompress);
        using var body = new MemoryStream();

        var buffer = new byte[8192];

        while (deflate.Read(buffer) is var read && read > 0)
        {
            if (body.Length + read > Ceiling)
            {
                throw new InvalidDataException("The link expands to more than a design can be.");
            }

            body.Write(buffer, 0, read);
        }

        return body.ToArray();
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

            if (packed.Length < 2)
            {
                return null;
            }

            return packed[0] switch
            {
                Coded => FromCoded(packed),
                Packed => FromPacked(packed),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or CwSyntaxException
                                      or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Reads a link from before the tree packing, which must go on working for ever.</summary>
    private static EmpireDesign? FromCoded(byte[] packed) =>
        EmpireDesignsFile.Load(Expand(Inflate(packed, from: 1))).Designs.FirstOrDefault();

    /// <summary>
    /// Reads a packed design by writing its tree back out and loading it the ordinary way.
    /// </summary>
    /// <remarks>
    /// Rather than building the design from the tree directly. The loader is where every rule about
    /// what a design is already lives, and a second way in would be a second set of answers to keep
    /// in step with the first.
    /// </remarks>
    private static EmpireDesign? FromPacked(byte[] packed)
    {
        var body = (packed[1] & Compressed) != 0 ? Inflate(packed, from: 2) : packed[2..];

        var document = new CwDocument();

        foreach (var node in DesignLinkV2.Unpack(body).Nodes)
        {
            document.Add(node);
        }

        return EmpireDesignsFile.Load(document.ToBytes(CwWriteOptions.Compact))
            .Designs.FirstOrDefault();
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
            // Both ends checked. Only the upper one was, so a marker followed by a zero byte gave an
            // index of -1 and threw past the filter that promises a link which will not unpack is
            // treated as no design rather than as an error.
            else if (coded[i] - 1 is var index && index >= 0 && index < Tokens.Length)
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
