static class P
{
    static Result Close() { return default; }

    static Result Run()
    {
        Defer(() => Try(Close()));
        ErrDefer(() => Try(Close()));
        return default;
    }
}
