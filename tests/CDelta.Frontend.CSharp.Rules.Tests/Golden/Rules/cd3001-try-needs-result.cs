static class P
{
    static Result<int> Source() { return 1; }

    static int Plain()
    {
        return Try(Source());
    }

    static Result<int> Fine()
    {
        return Try(Source());
    }
}
