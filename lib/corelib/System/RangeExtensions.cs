namespace System;

public static class RangeExtensions
{
    public static RangeEnumerator GetEnumerator(this Range range) => throw null;
}

public struct RangeEnumerator
{
    private int _current;
    private int _end;

    public int Current => _current;

    public bool MoveNext() => throw null;
}
