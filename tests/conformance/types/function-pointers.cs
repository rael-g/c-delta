static unsafe class P
{
    static int Add(int a, int b) => a + b;

    static int Scale(int value) => value * 3;

    static int Fold(delegate*<int, int, int> combine, int first, int second) => combine(first, second);

    public static int Main()
    {
        delegate*<int, int, int> add = &Add;
        delegate*<int, int> scale = &Scale;
        return Fold(add, 20, 1) + scale(7) + Fold(&Add, 0, 0);
    }
}
