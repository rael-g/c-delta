namespace System;

public readonly struct ReadOnlySpan<T>
{
    private readonly nint _pointer;
    private readonly int _length;

    public ReadOnlySpan(T[] array)
    {
    }

    public unsafe ReadOnlySpan(void* pointer, int length)
    {
        _pointer = (nint)pointer;
        _length = length;
    }

    public int Length => _length;

    public ref readonly T this[int index] => throw null;

    public ReadOnlySpan<T> Slice(int start) => throw null;

    public ReadOnlySpan<T> Slice(int start, int length) => throw null;

    public Enumerator GetEnumerator() => throw null;

    public static ReadOnlySpan<T> Empty => throw null;

    public struct Enumerator
    {
        public bool MoveNext() => throw null;

        public ref readonly T Current => throw null;
    }
}
