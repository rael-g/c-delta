namespace Std;

public static unsafe class Process
{
    [DllImport("c")]
    private static extern void exit(int code);

    public static ReadOnlySpan<string> Args()
    {
        return Startup.Args();
    }

    private const int Failure = 1;

    public static int Run(Func<Result> body)
    {
        Result result = body();
        if (!result.IsError)
        {
            return 0;
        }

        Report(result.Error);
        return Failure;
    }

    public static int Run(Func<Result<int>> body)
    {
        Result<int> result = body();
        if (!result.IsError)
        {
            return result.Value;
        }

        Report(result.Error);
        return Failure;
    }

    private static void Report(Error error)
    {
        Debug.Print("error: "u8);
        Debug.Print(error.Name);
        Debug.Print("\n"u8);
        Debug.PrintErrorTrace();
    }

    public static void Exit(int code)
    {
        exit(code);
        throw Unreachable();
    }
}
