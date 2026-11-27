static class P
{
    static Result Close() { return default; }

    static Result Run()
    {
        Defer(() => Close().Try());
        ErrDefer(() => Close().Try());
        return default;
    }
}
