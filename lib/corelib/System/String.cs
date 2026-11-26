namespace System;

public readonly unsafe struct String
{
    private readonly byte* _pointer;
    private readonly int _length;

    public ReadOnlySpan<byte> Bytes => throw null;

    public String Slice(int start, int count) => throw null;

    public Enumerator GetEnumerator() => throw null;

    public static bool operator ==(String left, String right) => throw null;

    public static bool operator !=(String left, String right) => throw null;

    public static implicit operator byte*(String value) => throw null;

    public static implicit operator ReadOnlySpan<byte>(String value) => throw null;

    public struct Enumerator
    {
        public bool MoveNext() => throw null;

        public System.Text.Rune Current => throw null;
    }
}
