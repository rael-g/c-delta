using Std.C;

namespace Std;

public static unsafe class Process
{
    [DllImport("c")]
    private static extern void exit(int code);

    public static ReadOnlySpan<CString> Args()
    {
        return new ReadOnlySpan<CString>((CString*)Startup.ArgValues(), Startup.ArgCount());
    }

    public static void Exit(int code)
    {
        exit(code);
        throw Unreachable();
    }
}
