using System.Text;

namespace CDelta.Ir;

internal sealed class IrCursor(string text, int line)
{
    private int _position;

    public int Line { get; } = line;

    public bool AtEnd
    {
        get
        {
            SkipSpaces();
            return _position >= text.Length;
        }
    }

    public char Peek
    {
        get
        {
            SkipSpaces();
            return _position < text.Length ? text[_position] : '\0';
        }
    }

    public string Rest => text[_position..];

    public IrParseException Error(string message) => new(message + " in '" + text.Trim() + "'", Line);

    public bool TryChar(char c)
    {
        SkipSpaces();
        if (_position < text.Length && text[_position] == c)
        {
            _position++;
            return true;
        }

        return false;
    }

    public void Expect(char c)
    {
        if (!TryChar(c))
        {
            throw Error("expected '" + c + "'");
        }
    }

    public bool TryText(string literal)
    {
        SkipSpaces();
        if (string.CompareOrdinal(text, _position, literal, 0, literal.Length) == 0)
        {
            _position += literal.Length;
            return true;
        }

        return false;
    }

    public void ExpectText(string literal)
    {
        if (!TryText(literal))
        {
            throw Error("expected '" + literal + "'");
        }
    }

    public string PeekWord()
    {
        int saved = _position;
        string word = ReadWordOrEmpty();
        _position = saved;
        return word;
    }

    public bool TryWord(string word)
    {
        int saved = _position;
        if (ReadWordOrEmpty() == word)
        {
            return true;
        }

        _position = saved;
        return false;
    }

    public void ExpectWord(string word)
    {
        if (!TryWord(word))
        {
            throw Error("expected '" + word + "'");
        }
    }

    public string ReadWord()
    {
        string word = ReadWordOrEmpty();
        if (word.Length == 0)
        {
            throw Error("expected a word");
        }

        return word;
    }

    public string ReadToken()
    {
        SkipSpaces();
        int start = _position;
        while (_position < text.Length && !char.IsWhiteSpace(text[_position]))
        {
            _position++;
        }

        return text[start.._position];
    }

    public int ReadInt() => (int)ReadInt128();

    public Int128 ReadInt128()
    {
        SkipSpaces();
        int start = _position;
        if (_position < text.Length && text[_position] == '-')
        {
            _position++;
        }

        while (_position < text.Length && char.IsDigit(text[_position]))
        {
            _position++;
        }

        if (!Int128.TryParse(text.AsSpan(start, _position - start), out Int128 value))
        {
            throw Error("expected an integer");
        }

        return value;
    }

    public ulong ReadHex()
    {
        SkipSpaces();
        int start = _position;
        while (_position < text.Length && Uri.IsHexDigit(text[_position]))
        {
            _position++;
        }

        return ulong.Parse(text.AsSpan(start, _position - start), System.Globalization.NumberStyles.HexNumber);
    }

    public byte[] ReadStringBytes()
    {
        SkipSpaces();
        if (_position >= text.Length || text[_position] != '"')
        {
            throw Error("expected a string");
        }

        _position++;
        var bytes = new List<byte>();
        while (_position < text.Length && text[_position] != '"')
        {
            char c = text[_position++];
            if (c != '\\')
            {
                bytes.Add((byte)c);
                continue;
            }

            char escape = text[_position++];
            switch (escape)
            {
                case 'n':
                    bytes.Add((byte)'\n');
                    break;
                case 't':
                    bytes.Add((byte)'\t');
                    break;
                case 'x':
                    bytes.Add(Convert.ToByte(text.Substring(_position, 2), 16));
                    _position += 2;
                    break;
                default:
                    bytes.Add((byte)escape);
                    break;
            }
        }

        _position++;
        return [.. bytes];
    }

    public string ReadString() => Encoding.UTF8.GetString(ReadStringBytes());

    public string ReadReference()
    {
        SkipSpaces();
        if (_position < text.Length && text[_position] == '"')
        {
            return ReadString();
        }

        int start = _position;
        while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] is '_' or '.'))
        {
            _position++;
        }

        if (start == _position)
        {
            throw Error("expected a reference");
        }

        return text[start.._position];
    }

    private string ReadWordOrEmpty()
    {
        SkipSpaces();
        int start = _position;
        while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] is '_' or '.'))
        {
            _position++;
        }

        return text[start.._position];
    }

    private void SkipSpaces()
    {
        while (_position < text.Length && (text[_position] == ' ' || text[_position] == '\t'))
        {
            _position++;
        }
    }
}
