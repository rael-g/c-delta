struct Grid
{
    public int Width;

    public int this[int x] => x * Width;
}

unsafe struct Cells
{
    public int* First;

    public ref int this[int i] => ref First[i];
}

static unsafe class P
{
    public static int Main()
    {
        Grid g = default;
        g.Width = 3;
        int* data = stackalloc int[] { 1, 2, 3 };
        Cells c = default;
        c.First = data;
        c[1] = 20;
        c[2] += 5;
        return g[4] + c[0] + c[1] + c[2];
    }
}
