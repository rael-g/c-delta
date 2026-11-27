enum StepError { Failed }

static class Program
{
    public static int Log;

    static Result<int, StepError> Step(int v)
    {
        if (v < 0) return StepError.Failed;
        return v;
    }

    static Result<int, StepError> Run(int a, int b)
    {
        Defer(() => Log = Log * 10 + 1);
        int x = Step(a).Try();
        ErrDefer(() => Log = Log * 10 + 2);
        int y = Step(b).Try();
        return x + y;
    }

    static int Main()
    {
        Log = 0;
        _ = Run(1, 2);
        int ok = Log;
        Log = 0;
        _ = Run(1, -1);
        int late = Log;
        Log = 0;
        _ = Run(-1, 2);
        int early = Log;
        return ok * 100 + late * 10 + early - 100;
    }
}
