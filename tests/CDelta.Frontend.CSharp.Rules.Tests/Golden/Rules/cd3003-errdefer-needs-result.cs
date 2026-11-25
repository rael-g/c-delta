static class P
{
    static void Plain()
    {
        ErrDefer(() => { });
    }

    static Result Fine()
    {
        ErrDefer(() => { });
        return default;
    }
}
