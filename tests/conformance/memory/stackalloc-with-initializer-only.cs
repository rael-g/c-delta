static unsafe class Program
{
    static int Main()
    {
        int* values = stackalloc int[] { 4, 5, 6 };
        return values[0] + values[1] * 10 + values[2] * 100;
    }
}
