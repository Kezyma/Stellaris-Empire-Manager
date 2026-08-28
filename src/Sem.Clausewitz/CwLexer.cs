namespace Sem.Clausewitz;

/// <summary>
/// Turns Paradox script text into tokens, attaching the whitespace and comments before each token
/// to that token so the original text can be reconstructed exactly.
/// </summary>
public sealed class CwLexer(string text)
{
    private readonly string _text = text ?? throw new ArgumentNullException(nameof(text));
    private int _position;

    /// <summary>
    /// Trailing whitespace and comments after the final token. Only meaningful once
    /// <see cref="Tokenize"/> has run to completion.
    /// </summary>
    public string TrailingTrivia { get; private set; } = string.Empty;

    /// <summary>Reads the whole input into a token list.</summary>
    public List<CwToken> Tokenize()
    {
        var tokens = new List<CwToken>();

        while (true)
        {
            var trivia = ReadTrivia();

            if (_position >= _text.Length)
            {
                TrailingTrivia = trivia;
                return tokens;
            }

            tokens.Add(ReadToken(trivia));
        }
    }

    /// <summary>Consumes whitespace and <c>#</c> comments, returning them verbatim.</summary>
    private string ReadTrivia()
    {
        var start = _position;

        while (_position < _text.Length)
        {
            var c = _text[_position];

            if (char.IsWhiteSpace(c))
            {
                _position++;
            }
            else if (c == '#')
            {
                // A comment runs to the end of the line; the line break itself is whitespace.
                while (_position < _text.Length && _text[_position] is not ('\n' or '\r'))
                {
                    _position++;
                }
            }
            else
            {
                break;
            }
        }

        return _position == start ? string.Empty : _text[start.._position];
    }

    private CwToken ReadToken(string leadingTrivia)
    {
        var c = _text[_position];

        switch (c)
        {
            case '{':
                _position++;
                return new CwToken(CwTokenKind.LeftBrace, "{", leadingTrivia);

            case '}':
                _position++;
                return new CwToken(CwTokenKind.RightBrace, "}", leadingTrivia);

            case '"':
                return ReadQuotedString(leadingTrivia);
        }

        if (IsOperatorStart(c))
        {
            return ReadOperator(leadingTrivia);
        }

        return ReadBareToken(leadingTrivia);
    }

    private CwToken ReadQuotedString(string leadingTrivia)
    {
        var start = _position;
        _position++;

        while (_position < _text.Length)
        {
            var c = _text[_position];

            if (c == '\\' && _position + 1 < _text.Length)
            {
                // Paradox script has no real escape mechanism, but a stray backslash before a
                // quote must not be allowed to swallow the terminator.
                _position += 2;
                continue;
            }

            _position++;

            if (c == '"')
            {
                return new CwToken(CwTokenKind.QuotedString, _text[start.._position], leadingTrivia);
            }

            // An unterminated string would otherwise consume the rest of the file; stopping at the
            // line break keeps the damage local and the error message useful.
            if (c is '\n')
            {
                break;
            }
        }

        throw new CwSyntaxException(
            $"Unterminated quoted string starting at offset {start}.", start);
    }

    private CwToken ReadOperator(string leadingTrivia)
    {
        var start = _position;
        _position++;

        // Two-character forms: ==, !=, >=, <=
        if (_position < _text.Length && _text[_position] == '=' && _text[start] is '=' or '!' or '>' or '<')
        {
            _position++;
        }

        return new CwToken(CwTokenKind.Operator, _text[start.._position], leadingTrivia);
    }

    private CwToken ReadBareToken(string leadingTrivia)
    {
        var start = _position;

        // Inline maths such as @[ base * 2 ] is one token; the brackets are not block braces.
        if (_text[_position] == '@' && _position + 1 < _text.Length && _text[_position + 1] == '[')
        {
            _position += 2;
            while (_position < _text.Length && _text[_position] != ']')
            {
                _position++;
            }

            if (_position < _text.Length)
            {
                _position++;
            }

            return new CwToken(CwTokenKind.BareToken, _text[start.._position], leadingTrivia);
        }

        while (_position < _text.Length && IsBareTokenChar(_text[_position]))
        {
            _position++;
        }

        if (_position == start)
        {
            throw new CwSyntaxException(
                $"Unexpected character '{_text[start]}' at offset {start}.", start);
        }

        return new CwToken(CwTokenKind.BareToken, _text[start.._position], leadingTrivia);
    }

    /// <summary>
    /// Note that <c>?</c> is deliberately absent. In modern Paradox script it suffixes a scope
    /// name to mean "only if it exists", as in <c>owner? = { ... }</c>, so it belongs to the
    /// identifier rather than being an operator of its own.
    /// </summary>
    private static bool IsOperatorStart(char c) => c is '=' or '<' or '>' or '!';

    private static bool IsBareTokenChar(char c) =>
        !char.IsWhiteSpace(c) &&
        c is not ('{' or '}' or '"' or '#') &&
        !IsOperatorStart(c);
}
