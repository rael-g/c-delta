namespace Std;

public static unsafe class Mem
{
    [DllImport("c")]
    private static extern int memcmp(byte* left, byte* right, nuint count);

    [DllImport("c")]
    private static extern void* memcpy(byte* destination, byte* source, nuint count);

    [DllImport("c")]
    private static extern void* memset(byte* destination, int value, nuint count);

    public static bool Eql(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        if (a.Length == 0)
        {
            return true;
        }

        fixed (byte* left = a)
        fixed (byte* right = b)
        {
            return memcmp(left, right, (nuint)a.Length) == 0;
        }
    }

    public static void Copy(Span<byte> destination, ReadOnlySpan<byte> source)
    {
        if (destination.Length != source.Length)
        {
            throw Panic("copy of spans of different lengths"u8);
        }

        if (source.Length == 0)
        {
            return;
        }

        fixed (byte* to = destination)
        fixed (byte* from = source)
        {
            _ = memcpy(to, from, (nuint)source.Length);
        }
    }

    public static void Set(Span<byte> destination, byte value)
    {
        if (destination.Length == 0)
        {
            return;
        }

        fixed (byte* to = destination)
        {
            _ = memset(to, value, (nuint)destination.Length);
        }
    }
}
