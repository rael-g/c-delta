struct Handle : IDisposable
{
    public int Id;

    public void Dispose()
    {
        Program.Closed += Id;
    }
}

static class Program
{
    public static int Closed;

    static Handle Open(int id)
    {
        Handle handle;
        handle.Id = id;
        return handle;
    }

    static int Work(bool early)
    {
        using var a = Open(1);
        using (var b = Open(10))
        {
            if (early) return 7;
        }

        using var c = Open(100);
        return 8;
    }

    static int Main()
    {
        int x = Work(true);
        int y = Work(false);
        return Closed + x + y;
    }
}
