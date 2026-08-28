using System.Text;

namespace Sem.Clausewitz;

/// <summary>Formatting used for nodes this library created rather than parsed.</summary>
public sealed record CwWriteOptions
{
    /// <summary>Line terminator. Stellaris writes CRLF, on every platform.</summary>
    public string NewLine { get; init; } = "\r\n";

    /// <summary>One level of indentation. Stellaris uses a single tab.</summary>
    public string Indent { get; init; } = "\t";

    /// <summary>
    /// Whether to write <c>key = value</c> rather than <c>key=value</c>. The empire designs file
    /// has no spaces; the game's own script files do.
    /// </summary>
    public bool SpaceAroundOperator { get; init; }

    /// <summary>
    /// Whether a block's opening brace goes on its own line. This is how the game writes the
    /// empire designs file.
    /// </summary>
    public bool BraceOnNewLine { get; init; } = true;

    /// <summary>Matches how Stellaris writes <c>user_empire_designs_v3.4.txt</c>.</summary>
    public static CwWriteOptions EmpireDesigns { get; } = new();

    /// <summary>Matches how the game's own script files under <c>common/</c> are written.</summary>
    public static CwWriteOptions GameScript { get; } = new()
    {
        SpaceAroundOperator = true,
        BraceOnNewLine = false,
    };
}

/// <summary>
/// Renders a node tree back to text.
/// </summary>
/// <remarks>
/// Tokens that were parsed carry their original whitespace and are written back untouched, so an
/// unmodified document reproduces its source byte for byte. Only tokens this library created carry
/// no formatting, and those are laid out according to <see cref="CwWriteOptions"/>. The practical
/// effect is that editing one empire in a designs file leaves every other byte of it alone.
/// </remarks>
public sealed class CwWriter(CwWriteOptions? options = null)
{
    private readonly CwWriteOptions _options = options ?? CwWriteOptions.EmpireDesigns;

    /// <summary>Renders a whole document, including its trailing whitespace.</summary>
    public string Write(CwDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        WriteNodes(builder, document.Nodes, depth: 0);
        builder.Append(document.TrailingTrivia);
        return builder.ToString();
    }

    /// <summary>Renders a single node, for diagnostics and tests.</summary>
    public string Write(CwNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder();
        WriteNode(builder, node, depth: 0);
        return builder.ToString();
    }

    private void WriteNodes(StringBuilder builder, IReadOnlyList<CwNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            WriteNode(builder, node, depth);
        }
    }

    private void WriteNode(StringBuilder builder, CwNode node, int depth)
    {
        if (node.KeyToken is null)
        {
            WriteValue(builder, node.Value, depth, afterOperator: false);
            return;
        }

        Emit(builder, node.KeyToken, EntryTrivia(builder, depth));
        Emit(builder, node.OperatorToken!, OperatorTrivia());
        WriteValue(builder, node.Value, depth, afterOperator: true);
    }

    private void WriteValue(StringBuilder builder, CwValue value, int depth, bool afterOperator)
    {
        switch (value)
        {
            case CwScalar scalar:
                Emit(
                    builder,
                    scalar.Token,
                    afterOperator ? OperatorTrivia() : EntryTrivia(builder, depth));
                break;

            case CwBlock block:
                Emit(builder, block.Open, OpenBraceTrivia(builder, depth, afterOperator));
                WriteNodes(builder, block.Nodes, depth + 1);

                // A block the source never closed stays unclosed, so the file's bytes are unchanged.
                if (block.Close is not null)
                {
                    Emit(
                        builder,
                        block.Close,
                        block.Nodes.Count == 0 ? string.Empty : NewLineIndent(depth));
                }

                break;

            default:
                throw new NotSupportedException($"Unknown value type {value.GetType().Name}.");
        }
    }

    /// <summary>Writes a token's original whitespace, or the supplied fallback when it has none.</summary>
    private static void Emit(StringBuilder builder, CwToken token, string synthesized)
    {
        builder.Append(token.LeadingTrivia ?? synthesized).Append(token.Text);
    }

    /// <summary>Puts an entry on its own indented line, unless it opens the file.</summary>
    private string EntryTrivia(StringBuilder builder, int depth) =>
        builder.Length == 0 ? string.Empty : NewLineIndent(depth);

    private string OperatorTrivia() => _options.SpaceAroundOperator ? " " : string.Empty;

    private string OpenBraceTrivia(StringBuilder builder, int depth, bool afterOperator)
    {
        if (!afterOperator)
        {
            return EntryTrivia(builder, depth);
        }

        return _options.BraceOnNewLine ? NewLineIndent(depth) : OperatorTrivia();
    }

    private string NewLineIndent(int depth) =>
        depth == 0 ? _options.NewLine : _options.NewLine + string.Concat(Enumerable.Repeat(_options.Indent, depth));
}
