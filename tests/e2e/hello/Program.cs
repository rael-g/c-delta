static unsafe class Program
{
    [DllImport("c")]
    static extern int puts(byte* text);

    static void Main()
    {
        puts("hello");
    }
}
