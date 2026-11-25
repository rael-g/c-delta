static unsafe class Program
{
    [DllImport("c")]
    static extern int puts(byte* text);

    static int Main()
    {
        puts("hello");
        return 0;
    }
}
