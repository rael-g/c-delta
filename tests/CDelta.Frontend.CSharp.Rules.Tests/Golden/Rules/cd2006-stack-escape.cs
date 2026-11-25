static unsafe class M
{
    static int* F()
    {
        int* p = stackalloc int[4];
        return p;
    }

    static int* G()
    {
        int x = 0;
        return &x;
    }

    static void H()
    {
        int* p = stackalloc int[4];
        _ = p;
    }
}
