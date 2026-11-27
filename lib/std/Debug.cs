namespace Std;

public static unsafe class Debug
{
    private const int StandardError = 2;

    [DllImport("c")]
    private static extern nint write(int descriptor, byte* bytes, nuint count);

    public static void PrintErrorTrace()
    {
        CDelta.Intrinsics.PrintErrorTrace();
    }

    public static void Print(ReadOnlySpan<byte> text)
    {
        fixed (byte* bytes = text)
        {
            _ = write(StandardError, bytes, (nuint)text.Length);
        }
    }
}
