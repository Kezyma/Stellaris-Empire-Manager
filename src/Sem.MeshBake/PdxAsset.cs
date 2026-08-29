using System.Buffers.Binary;
using System.Text;

namespace Sem.MeshBake;

/// <summary>
/// A node in a Paradox binary asset: a name, some typed properties, and child nodes.
/// </summary>
public sealed class PdxNode(string name)
{
    /// <summary>The node's name, such as <c>mesh</c> or <c>material</c>.</summary>
    public string Name { get; } = name;

    /// <summary>Child nodes, in file order.</summary>
    public List<PdxNode> Children { get; } = [];

    /// <summary>Properties by name.</summary>
    public Dictionary<string, PdxValue> Properties { get; } = new(StringComparer.Ordinal);

    /// <summary>Finds the first child with a name.</summary>
    public PdxNode? Child(string name) =>
        Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    /// <summary>Reads a float property, or null when it is absent or another type.</summary>
    public float[]? Floats(string name) =>
        Properties.TryGetValue(name, out var value) ? value.Floats : null;

    /// <summary>Reads an integer property, or null when it is absent or another type.</summary>
    public int[]? Ints(string name) =>
        Properties.TryGetValue(name, out var value) ? value.Ints : null;

    /// <summary>Reads the first string of a property, or null.</summary>
    public string? String(string name) =>
        Properties.TryGetValue(name, out var value) ? value.Strings?.FirstOrDefault() : null;

    /// <summary>Walks this node and everything beneath it.</summary>
    public IEnumerable<PdxNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;

            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    public override string ToString() =>
        $"{Name} ({Properties.Count} properties, {Children.Count} children)";
}

/// <summary>A property's value, which is an array of one of three types.</summary>
public sealed class PdxValue
{
    private PdxValue()
    {
    }

    /// <summary>Integer values, when the property holds them.</summary>
    public int[]? Ints { get; private init; }

    /// <summary>Floating-point values, when the property holds them.</summary>
    public float[]? Floats { get; private init; }

    /// <summary>Strings, when the property holds them.</summary>
    public string[]? Strings { get; private init; }

    internal static PdxValue Of(int[] values) => new() { Ints = values };

    internal static PdxValue Of(float[] values) => new() { Floats = values };

    internal static PdxValue Of(string[] values) => new() { Strings = values };

    public override string ToString() =>
        Ints is not null ? $"int[{Ints.Length}]"
        : Floats is not null ? $"float[{Floats.Length}]"
        : $"string[{Strings?.Length ?? 0}]";
}

/// <summary>
/// Reads the binary asset format Paradox uses for models and animations.
/// </summary>
/// <remarks>
/// The format is a tree written depth-first. A node opens with one square bracket per level of
/// nesting followed by its name; a property opens with an exclamation mark, its name, a type
/// letter and a count. There is no length prefix on a node, so its extent is worked out from the
/// depth of whatever comes next.
/// </remarks>
public static class PdxAssetReader
{
    private static readonly byte[] Magic = "@@b@"u8.ToArray();

    private const byte NodeMarker = (byte)'[';
    private const byte PropertyMarker = (byte)'!';
    private const byte IntType = (byte)'i';
    private const byte FloatType = (byte)'f';
    private const byte StringType = (byte)'s';

    /// <summary>Whether the bytes begin with the format's marker.</summary>
    public static bool IsPdxAsset(ReadOnlySpan<byte> bytes) =>
        bytes.Length > Magic.Length && bytes[..Magic.Length].SequenceEqual(Magic);

    /// <summary>Reads an asset into a tree whose root holds the file's top-level nodes.</summary>
    public static PdxNode Read(ReadOnlySpan<byte> bytes)
    {
        if (!IsPdxAsset(bytes))
        {
            throw new InvalidDataException("Not a Paradox binary asset.");
        }

        var root = new PdxNode("root");
        var stack = new List<PdxNode> { root };
        var position = Magic.Length;

        while (position < bytes.Length)
        {
            var marker = bytes[position];

            if (marker == PropertyMarker)
            {
                position++;
                ReadProperty(bytes, ref position, stack[^1]);
                continue;
            }

            if (marker == NodeMarker)
            {
                var depth = 0;
                while (position < bytes.Length && bytes[position] == NodeMarker)
                {
                    depth++;
                    position++;
                }

                var name = ReadNullTerminated(bytes, ref position);
                var node = new PdxNode(name);

                // Depth counts from the root, so anything deeper than this node is discarded and
                // the new node hangs off its parent.
                while (stack.Count > depth)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                stack[^1].Children.Add(node);
                stack.Add(node);
                continue;
            }

            // Anything else means the file is not shaped as expected; stopping keeps whatever was
            // read rather than throwing away a mesh over one unfamiliar byte.
            break;
        }

        return root;
    }

    private static void ReadProperty(ReadOnlySpan<byte> bytes, ref int position, PdxNode node)
    {
        var nameLength = bytes[position++];
        var name = Encoding.ASCII.GetString(bytes.Slice(position, nameLength));
        position += nameLength;

        var type = bytes[position++];
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
        position += 4;

        switch (type)
        {
            case IntType:
            {
                var values = new int[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes[(position + (i * 4))..]);
                }

                position += count * 4;
                node.Properties[name] = PdxValue.Of(values);
                break;
            }

            case FloatType:
            {
                var values = new float[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(position + (i * 4))..]);
                }

                position += count * 4;
                node.Properties[name] = PdxValue.Of(values);
                break;
            }

            case StringType:
            {
                // Each string carries its own length, which counts the terminator as well.
                var values = new string[count];
                for (var i = 0; i < count; i++)
                {
                    var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
                    position += 4;

                    values[i] = Encoding.ASCII.GetString(bytes.Slice(position, Math.Max(0, length - 1)));
                    position += length;
                }

                node.Properties[name] = PdxValue.Of(values);
                break;
            }

            default:
                throw new InvalidDataException(
                    $"Unknown property type '{(char)type}' for '{name}' at offset {position - 5}.");
        }
    }

    private static string ReadNullTerminated(ReadOnlySpan<byte> bytes, ref int position)
    {
        var start = position;
        while (position < bytes.Length && bytes[position] != 0)
        {
            position++;
        }

        var text = Encoding.ASCII.GetString(bytes[start..position]);

        if (position < bytes.Length)
        {
            position++;
        }

        return text;
    }
}
