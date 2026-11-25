namespace System.Runtime.InteropServices;

public static class MemoryMarshal
{
    public static Span<T> CreateSpan<T>(ref T reference, int length) => throw null;

    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref readonly T reference, int length) => throw null;
}
