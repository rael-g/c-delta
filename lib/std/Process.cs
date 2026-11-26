namespace Std;

public static unsafe class Process
{
    [DllImport("c")]
    private static extern void exit(int code);

    public static ReadOnlySpan<string> Args()
    {
        return Startup.Args();
    }

    public static void Exit(int code)
    {
        exit(code);
        throw Unreachable();
    }
}
