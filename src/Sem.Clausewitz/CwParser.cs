namespace Sem.Clausewitz;

/// <summary>
/// Builds a node tree from a token list.
/// </summary>
/// <remarks>
/// The grammar is small: a block holds a sequence of entries, and each entry is either
/// <c>key = value</c> or a bare value. Whether an entry is an assignment is decided by looking
/// ahead for an operator, which also means a malformed line such as <c>some_key ""</c> (seen in a
/// shipped DLC descriptor) parses as two bare values instead of failing.
/// </remarks>
internal sealed class CwParser(List<CwToken> tokens, CwParseOptions options)
{
    private readonly List<CwToken> _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    private readonly CwParseOptions _options = options;
    private int _index;

    /// <summary>Parses the top level of a file, which has no enclosing braces.</summary>
    public List<CwNode> ParseDocument()
    {
        var nodes = ParseEntries();

        if (_index < _tokens.Count)
        {
            throw new CwSyntaxException(
                $"Unexpected '{_tokens[_index].Text}' at the top level; a closing brace has no matching opening brace.");
        }

        return nodes;
    }

    /// <summary>Parses entries until the input ends or a closing brace is reached.</summary>
    private List<CwNode> ParseEntries()
    {
        var nodes = new List<CwNode>();

        while (_index < _tokens.Count && _tokens[_index].Kind != CwTokenKind.RightBrace)
        {
            nodes.Add(ParseEntry());
        }

        return nodes;
    }

    private CwNode ParseEntry()
    {
        var token = _tokens[_index];

        if (token.Kind == CwTokenKind.LeftBrace)
        {
            return new CwNode(ParseBlock());
        }

        if (token.Kind is not (CwTokenKind.BareToken or CwTokenKind.QuotedString))
        {
            throw new CwSyntaxException($"Unexpected '{token.Text}' where a key or value was expected.");
        }

        if (_index + 1 < _tokens.Count && _tokens[_index + 1].Kind == CwTokenKind.Operator)
        {
            var key = _tokens[_index++];
            var op = _tokens[_index++];
            return new CwNode(key, op, ParseValue(key));
        }

        return new CwNode(new CwScalar(_tokens[_index++]));
    }

    private CwValue ParseValue(CwToken key)
    {
        if (_index >= _tokens.Count)
        {
            throw new CwSyntaxException($"'{key.Text}' has no value; the file ends after the operator.");
        }

        var token = _tokens[_index];

        return token.Kind switch
        {
            CwTokenKind.LeftBrace => ParseBlock(),
            CwTokenKind.BareToken or CwTokenKind.QuotedString => new CwScalar(_tokens[_index++]),
            _ => throw new CwSyntaxException($"'{key.Text}' has '{token.Text}' where a value was expected."),
        };
    }

    private CwBlock ParseBlock()
    {
        var open = _tokens[_index++];
        var nodes = ParseEntries();

        if (_index >= _tokens.Count)
        {
            if (!_options.AllowUnclosedBlocks)
            {
                throw new CwSyntaxException("A block was opened but never closed.");
            }

            return new CwBlock(open, nodes, close: null);
        }

        return new CwBlock(open, nodes, _tokens[_index++]);
    }
}
