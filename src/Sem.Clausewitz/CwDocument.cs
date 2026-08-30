namespace Sem.Clausewitz;

/// <summary>
/// A parsed Paradox script file: the top-level entries, plus everything needed to write the file
/// back out exactly as it came in.
/// </summary>
public sealed class CwDocument
{
    private readonly List<CwNode> _nodes;

    private CwDocument(List<CwNode> nodes, string trailingTrivia, CwEncodingInfo encoding)
    {
        _nodes = nodes;
        TrailingTrivia = trailingTrivia;
        Encoding = encoding;
    }

    /// <summary>Creates an empty document.</summary>
    public CwDocument()
        : this([], string.Empty, CwEncodingInfo.Default)
    {
    }

    /// <summary>
    /// The top-level entries in source order. Order and repeated keys are both significant.
    /// </summary>
    public IReadOnlyList<CwNode> Nodes => _nodes;

    /// <summary>Whitespace and comments after the last token, preserved so the file ends identically.</summary>
    public string TrailingTrivia { get; set; }

    /// <summary>How the file was encoded, so writing can reproduce it.</summary>
    public CwEncodingInfo Encoding { get; set; }

    /// <summary>Parses script from raw file bytes, detecting the encoding.</summary>
    public static CwDocument Parse(ReadOnlySpan<byte> bytes, CwParseOptions? options = null)
    {
        var text = CwTextEncoding.Decode(bytes, out var encoding);
        return ParseText(text, encoding, options);
    }

    /// <summary>Parses script from text that has already been decoded.</summary>
    public static CwDocument ParseText(
        string text,
        CwEncodingInfo? encoding = null,
        CwParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lexer = new CwLexer(text);
        var tokens = lexer.Tokenize();
        var nodes = new CwParser(tokens, options ?? CwParseOptions.Strict).ParseDocument();

        return new CwDocument(nodes, lexer.TrailingTrivia, encoding ?? CwEncodingInfo.Default);
    }

    /// <summary>
    /// Appends a top-level entry.
    /// </summary>
    /// <remarks>
    /// The entry is made to forget where it used to sit. Parsing does not come through here — a
    /// document read from a file keeps every token's own whitespace, which is what makes an
    /// untouched file write back byte for byte — so this only affects entries being placed anew,
    /// and those should be laid out by the writer rather than by wherever they were copied from.
    /// </remarks>
    public void Add(CwNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        node.ForgetPlacement();
        _nodes.Add(node);
    }

    /// <summary>Inserts a top-level entry at a position.</summary>
    public void Insert(int index, CwNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        node.ForgetPlacement();
        _nodes.Insert(index, node);
    }

    /// <summary>Removes a top-level entry.</summary>
    public bool Remove(CwNode node) => _nodes.Remove(node);

    /// <summary>Removes the top-level entry at a position.</summary>
    public void RemoveAt(int index) => _nodes.RemoveAt(index);

    /// <summary>Renders the document to text.</summary>
    public string ToText(CwWriteOptions? options = null) => new CwWriter(options).Write(this);

    /// <summary>
    /// Renders the document to bytes using the encoding it was parsed with. A document that has
    /// not been modified produces exactly the bytes it was parsed from.
    /// </summary>
    public byte[] ToBytes(CwWriteOptions? options = null) =>
        CwTextEncoding.Encode(ToText(options), Encoding);
}
