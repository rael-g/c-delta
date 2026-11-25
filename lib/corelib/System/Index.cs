namespace System;

public readonly struct Index
{
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        _value = fromEnd ? ~value : value;
    }

    public bool IsFromEnd => _value < 0;

    public int Value => _value < 0 ? ~_value : _value;

    public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

    public static implicit operator Index(int value) => new Index(value);
}
