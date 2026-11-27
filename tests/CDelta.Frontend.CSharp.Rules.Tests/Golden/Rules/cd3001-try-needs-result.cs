static class P
{
    static Result<int> Source() { return 1; }

    static int Plain()
    {
        return Source().Try();
    }

    static Result<int> Fine()
    {
        return Source().Try();
    }
}
