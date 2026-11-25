static unsafe class C
{
    [Unsupported("long double")]
    public static extern void use_long_double();

    public static extern void fine();
}

static class P
{
    static void Run()
    {
        C.fine();
        C.use_long_double();
    }
}
