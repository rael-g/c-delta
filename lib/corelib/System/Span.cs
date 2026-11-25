namespace System;

public readonly struct Span<T>
{
    private readonly nint _pointer;
    private readonly int _length;

    public Span(T[] array)
    {
    }

    public unsafe Span(void* pointer, int length)
    {
        _pointer = (nint)pointer;
        _length = length;
    }

    public int Length => _length;

    public ref T this[int index] => throw null;

    public Span<T> Slice(int start) => throw null;

    public Span<T> Slice(int start, int length) => throw null;

    public Enumerator GetEnumerator() => throw null;

    public static implicit operator ReadOnlySpan<T>(Span<T> span) => throw null;

    public struct Enumerator
    {
        public bool MoveNext() => throw null;

        public ref T Current => throw null;
    }
}
