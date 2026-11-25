using System;

static unsafe class Program
{
    [DllImport("c")]
    static extern int puts(byte* text);

    [DllImport("c")]
    static extern void exit(int code);

    [PanicHandler]
    static void OnPanic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> file, int line)
    {
        puts("custom handler");
        exit(7);
        throw Unreachable();
    }

    static int Main()
    {
        int zero = 0;
        return 10 / zero;
    }
}
