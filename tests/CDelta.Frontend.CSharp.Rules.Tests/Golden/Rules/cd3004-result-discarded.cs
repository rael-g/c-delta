static class P
{
    static Result Close() { return default; }

    static void Run()
    {
        Close();
        _ = Close();
        var kept = Close();
    }
}
