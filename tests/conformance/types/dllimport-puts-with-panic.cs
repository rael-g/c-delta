static unsafe class Program
{
    [DllImport("c")]
    static extern int puts(byte* text);

    static int Divide(int a, int b)
    {
        return a / b;
    }

    static int Main()
    {
        puts("before");
        return Divide(1, 0);
    }
}
