using System.Globalization;
using System.Text;
using Sem.Clausewitz;

namespace Sem.Ui.Services;

/// <summary>
/// The second packing: a design written as what it is rather than as the text of what it is.
/// </summary>
/// <remarks>
/// <para>
/// Version one wrote the block out, replaced runs of the format's own punctuation with two-byte
/// codes, and compressed the result. Measured across the eighteen empires in the corpus, that
/// leaves a median link of 660 characters, and it is close to the floor for the approach: dropping
/// the table and compressing the plain text is worse, a denser alphabet than base64 saves under one
/// per cent, and Brotli is not available to WebAssembly.
/// </para>
/// <para>
/// The measurement that gets past it is where the bytes go. Of an average empire's 1,776 bytes,
/// <b>938 are the shape of the format</b> — field names, braces, equals signs, quotes and newlines —
/// and 464 more are game vocabulary. Between them that is four fifths of a link, and neither is
/// information about the empire. Written as a tree of nodes with the names and the vocabulary held
/// as positions in <see cref="LinkDictionary"/>, both cost bits instead of letters.
/// </para>
/// <para>
/// What is preserved is exactly what <see cref="CwWriteOptions.Compact"/> would have written: the
/// order of the nodes, whether each has a key, the key and whether it was quoted, the operator, and
/// for each value either its text and quoting or another block. Trivia is not preserved because
/// compact writing discards it, which is what makes a decoded design re-encode to the same bytes.
/// </para>
/// <para>
/// Nothing is dropped. A key the dictionary has never heard of — an empire the player named, a trait
/// a mod added, a field a later game version introduces — is written out as itself. The format has
/// no list of fields it understands and no list it refuses; it copies whatever tree it is given, so
/// the guarantee version one made is the guarantee this makes.
/// </para>
/// </remarks>
internal static class DesignLinkV2
{
    /// <summary>The operators Clausewitz allows, in the order three bits name them.</summary>
    private static readonly string[] Operators = ["=", "==", "!=", ">", "<", ">=", "<=", "?="];

    /// <summary>What a scalar turned out to be, in the order three bits name them.</summary>
    private enum Scalar
    {
        Literal = 0,
        Repeat = 1,
        Vocabulary = 2,
        SpeciesName = 3,
        Integer = 4,
        Yes = 5,
        No = 6,
        Empty = 7,
    }

    /// <summary>
    /// How many nodes a link may unpack to.
    /// </summary>
    /// <remarks>
    /// The largest empire in the corpus is a little over four hundred nodes. Ten thousand is far
    /// past anything a design reaches and stops a hand-made link from asking for a tree that
    /// exhausts the tab it was opened in.
    /// </remarks>
    private const int NodeCeiling = 10_000;

    // ---------------------------------------------------------------- packing

    /// <summary>Writes a block as bits, with the text it could not encode following.</summary>
    internal static byte[] Pack(CwBlock block)
    {
        var bits = new BitWriter();
        var text = new List<byte>();
        var literals = new Dictionary<string, int>(StringComparer.Ordinal);

        WriteBlock(bits, block, LinkDictionary.Shape(LinkDictionary.RootShape), text, literals);

        var body = bits.ToArray();

        var packed = new List<byte>(body.Length + text.Count + 8);
        WriteVarint(packed, body.Length);
        packed.AddRange(body);
        packed.AddRange(text);

        return [.. packed];
    }

    private static void WriteBlock(BitWriter bits, CwBlock block, string[]? shape, List<byte> text,
                                   Dictionary<string, int> literals)
    {
        bits.Varint(block.Nodes.Count);

        foreach (var node in block.Nodes)
        {
            WriteNode(bits, node, shape, text, literals);
        }
    }

    private static void WriteNode(BitWriter bits, CwNode node, string[]? shape, List<byte> text,
                                  Dictionary<string, int> literals)
    {
        bits.Bit(node.IsAssignment);

        if (node.KeyToken is { } key)
        {
            WriteKey(bits, key, shape, text, literals);

            // Every design in the corpus uses "=" and nothing else, so the common operator is one
            // bit and the other seven cost four.
            var op = Array.IndexOf(Operators, node.Operator ?? "=");
            bits.Bit(op != 0);

            if (op != 0)
            {
                bits.Number(op < 0 ? 0 : op, 3);
            }
        }

        if (node.Value is CwBlock inner)
        {
            bits.Bit(true);

            // Vanilla ships one file that ends before its last block is closed, and the parser
            // represents that rather than inventing a brace. Whatever arrived, goes back out.
            bits.Bit(inner.IsClosed);
            WriteBlock(bits, inner, LinkDictionary.Shape(node.Key), text, literals);
        }
        else
        {
            bits.Bit(false);
            WriteScalar(bits, node.Scalar!, text, literals);
        }
    }

    /// <summary>
    /// Writes a node's key as cheaply as the parent allows.
    /// </summary>
    /// <remarks>
    /// Three ways, in order of what they cost. Among the handful its parent can hold, which is two
    /// or three bits and covers nearly every node in a design. Among all fifty-nine field names,
    /// for a field in an unexpected place. Or as its own letters, which is what the empire's key
    /// takes, that being whatever the empire is called.
    /// </remarks>
    private static void WriteKey(BitWriter bits, CwToken key, string[]? shape, List<byte> text,
                                 Dictionary<string, int> literals)
    {
        var quoted = key.Kind == CwTokenKind.QuotedString;

        // A field name is never quoted in a design, so the shape path can imply that and spend no
        // bit on it. One that somehow is quoted takes the longer road and still comes back right.
        if (!quoted && shape is not null && Array.IndexOf(shape, key.Value) is var at && at >= 0)
        {
            bits.Bit(true);
            bits.Index(at, shape.Length);
            return;
        }

        bits.Bit(false);

        if (!quoted && LinkDictionary.TryFind(key.Value, out var group, out var index)
            && group == LinkDictionary.FieldGroup)
        {
            bits.Bit(true);
            bits.Index(index, LinkDictionary.Count(LinkDictionary.FieldGroup));
            return;
        }

        bits.Bit(false);
        bits.Bit(quoted);
        WriteLiteral(bits, key.Value, text, literals);
    }

    private static void WriteScalar(BitWriter bits, CwScalar scalar, List<byte> text,
                                    Dictionary<string, int> literals)
    {
        var value = scalar.Value;
        var quoted = scalar.IsQuoted;

        if (value.Length == 0)
        {
            Tag(bits, Scalar.Empty, quoted);
            return;
        }

        if (!quoted && value == "yes")
        {
            Tag(bits, Scalar.Yes, quoted);
            return;
        }

        if (!quoted && value == "no")
        {
            Tag(bits, Scalar.No, quoted);
            return;
        }

        // Only where the number written back is the number that arrived: "07" and "1.0" are not
        // integers this can round-trip, and are carried as the text they are.
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number.ToString(CultureInfo.InvariantCulture) == value)
        {
            Tag(bits, Scalar.Integer, quoted);
            bits.Varint(number);
            return;
        }

        WriteWord(bits, value, quoted, text, literals);
    }

    /// <summary>
    /// Writes one piece of text as cheaply as it can be written: a position in the dictionary, a
    /// pointer back to somewhere it already appeared, or its own letters.
    /// </summary>
    private static void WriteWord(BitWriter bits, string value, bool quoted,
                                  List<byte> text, Dictionary<string, int> literals)
    {
        if (literals.TryGetValue(value, out var seen))
        {
            Tag(bits, Scalar.Repeat, quoted);
            bits.Varint(seen);
            return;
        }

        if (LinkDictionary.TryFindSpecies(value, out var stem, out var suffix))
        {
            Tag(bits, Scalar.SpeciesName, quoted);
            bits.Index(stem, LinkDictionary.Count(LinkDictionary.SpeciesStemGroup));
            bits.Number(suffix, 2);
            return;
        }

        // A name from the ruler dropdown is two positions rather than one. It rides in the
        // vocabulary kind rather than taking a kind of its own, because naming the prefix group is
        // already enough to say that a second index follows - and a ninth kind would have widened
        // the tag on every scalar in the design to buy this one.
        if (LinkDictionary.TryFindCharacter(value, out var prefix, out var ending))
        {
            Tag(bits, Scalar.Vocabulary, quoted);
            bits.Number(LinkDictionary.CharPrefixGroup, 5);
            bits.Index(prefix, LinkDictionary.Count(LinkDictionary.CharPrefixGroup));
            bits.Index(ending, LinkDictionary.Count(LinkDictionary.CharSuffixGroup));
            return;
        }

        if (LinkDictionary.TryFind(value, out var found, out var at))
        {
            Tag(bits, Scalar.Vocabulary, quoted);
            bits.Number(found, 5);
            bits.Index(at, LinkDictionary.Count(found));
            return;
        }

        Tag(bits, Scalar.Literal, quoted);
        WriteLiteral(bits, value, text, literals);
    }

    private static void Tag(BitWriter bits, Scalar kind, bool quoted)
    {
        bits.Number((int)kind, 3);
        bits.Bit(quoted);
    }

    /// <summary>
    /// Sends the letters to the text that follows the bits, and remembers where they went.
    /// </summary>
    /// <remarks>
    /// Kept out of the bit stream so that the one part of a design that is genuinely its own — the
    /// name somebody typed, and a biography if they wrote one — sits together as bytes that
    /// compress, rather than scattered through a stream of indices that do not.
    /// </remarks>
    private static void WriteLiteral(BitWriter bits, string value, List<byte> text,
                                     Dictionary<string, int> literals)
    {
        literals[value] = literals.Count;

        var bytes = Encoding.UTF8.GetBytes(value);
        bits.Varint(bytes.Length);
        text.AddRange(bytes);
    }

    // ---------------------------------------------------------------- unpacking

    /// <summary>Reads back what <see cref="Pack"/> wrote.</summary>
    /// <exception cref="InvalidDataException">The link is damaged, or is not this format.</exception>
    internal static CwBlock Unpack(byte[] packed)
    {
        var at = 0;
        var length = ReadVarint(packed, ref at);

        if (length < 0 || at + length > packed.Length)
        {
            throw new InvalidDataException("The packed design ends before its own body does.");
        }

        var bits = new BitReader(packed, at, length);
        var text = new TextCursor(packed, at + length);
        var literals = new List<string>();
        var budget = NodeCeiling;

        return ReadBlock(bits, LinkDictionary.Shape(LinkDictionary.RootShape), text, literals,
                         ref budget);
    }

    private static CwBlock ReadBlock(BitReader bits, string[]? shape, TextCursor text,
                                     List<string> literals, ref int budget)
    {
        var count = bits.Varint();

        if (count < 0 || count > budget)
        {
            throw new InvalidDataException("The link asks for more of a design than one can hold.");
        }

        budget -= count;

        var nodes = new List<CwNode>(count);

        for (var i = 0; i < count; i++)
        {
            nodes.Add(ReadNode(bits, shape, text, literals, ref budget));
        }

        return new CwBlock(CwToken.Synthetic(CwTokenKind.LeftBrace, "{"), nodes,
                           CwToken.Synthetic(CwTokenKind.RightBrace, "}"));
    }

    private static CwNode ReadNode(BitReader bits, string[]? shape, TextCursor text,
                                   List<string> literals, ref int budget)
    {
        CwToken? key = null;
        var op = "=";

        if (bits.Bit())
        {
            string name;
            var quotedKey = false;

            if (bits.Bit())
            {
                // Named among what its parent holds. A link claiming this where nothing is written
                // down about the parent is damaged rather than merely unfamiliar.
                var at = bits.Index(shape?.Length ?? 0);

                name = shape is not null && at < shape.Length
                    ? shape[at]
                    : throw new InvalidDataException("The link names a field its parent cannot hold.");
            }
            else if (bits.Bit())
            {
                var index = bits.Index(LinkDictionary.Count(LinkDictionary.FieldGroup));
                name = LinkDictionary.At(LinkDictionary.FieldGroup, index)
                    ?? throw new InvalidDataException("The link names a field this version lacks.");
            }
            else
            {
                quotedKey = bits.Bit();
                name = ReadLiteral(bits, text, literals);
            }

            key = quotedKey
                ? CwToken.Synthetic(CwTokenKind.QuotedString, CwToken.Quote(name))
                : CwToken.Synthetic(CwTokenKind.BareToken, name);

            if (bits.Bit())
            {
                op = Operators[bits.Number(3) % Operators.Length];
            }
        }

        CwValue value;

        if (bits.Bit())
        {
            var closed = bits.Bit();
            var block = ReadBlock(bits, LinkDictionary.Shape(key?.Value), text, literals,
                                  ref budget);

            value = closed
                ? block
                : new CwBlock(CwToken.Synthetic(CwTokenKind.LeftBrace, "{"), block.Nodes, close: null);
        }
        else
        {
            value = ReadScalar(bits, text, literals);
        }

        return key is null
            ? new CwNode(value)
            : new CwNode(key, CwToken.Synthetic(CwTokenKind.Operator, op), value);
    }

    private static CwValue ReadScalar(BitReader bits, TextCursor text, List<string> literals)
    {
        var kind = (Scalar)bits.Number(3);
        var quoted = bits.Bit();

        var value = kind switch
        {
            Scalar.Empty => string.Empty,
            Scalar.Yes => "yes",
            Scalar.No => "no",
            Scalar.Integer => bits.Varint().ToString(CultureInfo.InvariantCulture),
            Scalar.Repeat => Repeat(bits, literals),
            Scalar.SpeciesName => Species(bits),
            Scalar.Vocabulary => Vocabulary(bits),
            _ => ReadLiteral(bits, text, literals),
        };

        return quoted ? CwScalar.Quoted(value) : CwScalar.Bare(value);
    }

    private static string Repeat(BitReader bits, List<string> literals)
    {
        var index = bits.Varint();

        return index >= 0 && index < literals.Count
            ? literals[index]
            : throw new InvalidDataException("The link points back at text it never wrote.");
    }

    private static string Species(BitReader bits)
    {
        var stem = bits.Index(LinkDictionary.Count(LinkDictionary.SpeciesStemGroup));
        var suffix = bits.Number(2);

        return (LinkDictionary.At(LinkDictionary.SpeciesStemGroup, stem)
                ?? throw new InvalidDataException("The link names a species this version lacks."))
            + LinkDictionary.StemSuffixes[suffix];
    }

    private static string Vocabulary(BitReader bits)
    {
        var group = bits.Number(5);
        var index = bits.Index(LinkDictionary.Count(group));

        var value = LinkDictionary.At(group, index)
            ?? throw new InvalidDataException("The link names a choice this version lacks.");

        if (group != LinkDictionary.CharPrefixGroup)
        {
            return value;
        }

        // The prefix group is the one that carries a second half.
        var ending = bits.Index(LinkDictionary.Count(LinkDictionary.CharSuffixGroup));

        return value + (LinkDictionary.At(LinkDictionary.CharSuffixGroup, ending)
            ?? throw new InvalidDataException("The link names a character this version lacks."));
    }

    private static string ReadLiteral(BitReader bits, TextCursor text, List<string> literals)
    {
        var length = bits.Varint();
        var value = text.Take(length);

        literals.Add(value);

        return value;
    }

    // ---------------------------------------------------------------- the bits themselves

    private static void WriteVarint(List<byte> into, int value)
    {
        var rest = (uint)value;

        while (true)
        {
            var chunk = (byte)(rest & 0x7F);
            rest >>= 7;

            into.Add(rest == 0 ? chunk : (byte)(chunk | 0x80));

            if (rest == 0)
            {
                return;
            }
        }
    }

    private static int ReadVarint(byte[] from, ref int at)
    {
        var value = 0;
        var shift = 0;

        while (at < from.Length && shift <= 28)
        {
            var b = from[at++];
            value |= (b & 0x7F) << shift;

            if ((b & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
        }

        throw new InvalidDataException("The link ends part way through a length.");
    }

    /// <summary>How many bits it takes to name one of <paramref name="count"/> things.</summary>
    internal static int Width(int count)
    {
        var bits = 0;

        while ((1 << bits) < count)
        {
            bits++;
        }

        return Math.Max(bits, 1);
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _pending;
        private int _held;

        internal void Bit(bool set) => Number(set ? 1 : 0, 1);

        internal void Number(int value, int width)
        {
            for (var i = width - 1; i >= 0; i--)
            {
                _pending = (_pending << 1) | ((value >> i) & 1);

                if (++_held != 8)
                {
                    continue;
                }

                _bytes.Add((byte)_pending);
                _pending = 0;
                _held = 0;
            }
        }

        internal void Index(int value, int count) => Number(value, Width(count));

        /// <summary>A number in four-bit pieces, so a small one costs five bits and not thirty-two.</summary>
        internal void Varint(int value)
        {
            var rest = (uint)value;

            while (true)
            {
                Number((int)(rest & 0xF), 4);
                rest >>= 4;
                Bit(rest != 0);

                if (rest == 0)
                {
                    return;
                }
            }
        }

        internal byte[] ToArray()
        {
            if (_held == 0)
            {
                return [.. _bytes];
            }

            var tail = new List<byte>(_bytes) { (byte)(_pending << (8 - _held)) };

            return [.. tail];
        }
    }

    private sealed class BitReader(byte[] source, int start, int length)
    {
        private readonly int _end = start + length;
        private int _at = start;
        private int _held;

        internal bool Bit() => Number(1) == 1;

        internal int Number(int width)
        {
            var value = 0;

            for (var i = 0; i < width; i++)
            {
                if (_at >= _end)
                {
                    throw new InvalidDataException("The link ends part way through a design.");
                }

                value = (value << 1) | ((source[_at] >> (7 - _held)) & 1);

                if (++_held == 8)
                {
                    _held = 0;
                    _at++;
                }
            }

            return value;
        }

        internal int Index(int count) => Number(Width(count));

        internal int Varint()
        {
            var value = 0;
            var shift = 0;

            while (shift <= 28)
            {
                value |= Number(4) << shift;
                shift += 4;

                if (!Bit())
                {
                    return value;
                }
            }

            throw new InvalidDataException("The link holds a number too long to be one.");
        }
    }

    /// <summary>The letters that follow the bits, handed out in the order they were written.</summary>
    private sealed class TextCursor(byte[] source, int start)
    {
        private int _at = start;

        internal string Take(int length)
        {
            if (length < 0 || _at + length > source.Length)
            {
                throw new InvalidDataException("The link ends part way through a name.");
            }

            var value = Encoding.UTF8.GetString(source, _at, length);
            _at += length;

            return value;
        }
    }
}
