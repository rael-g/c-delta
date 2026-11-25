using System;

static class P
{
    [PanicHandler]
    static void First(ReadOnlySpan<byte> message, ReadOnlySpan<byte> file, int line)
    {
        throw Unreachable();
    }

    [PanicHandler]
    static void Second(ReadOnlySpan<byte> message, ReadOnlySpan<byte> file, int line)
    {
        throw Unreachable();
    }
}

static class Q
{
    [PanicHandler]
    static void Wrong(int code)
    {
        throw Unreachable();
    }
}

static class R
{
    [PanicHandler]
    static void Returns(ReadOnlySpan<byte> message, ReadOnlySpan<byte> file, int line)
    {
    }
}
