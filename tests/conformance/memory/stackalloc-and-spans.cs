static unsafe class Program
{
    static int Sum(ReadOnlySpan<int> values)
    {
        int total = 0;
        for (int i = 0; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }

    static int Main()
    {
        int* stack = stackalloc int[4];
        for (int i = 0; i < 4; i++)
        {
            stack[i] = i + 1;
        }

        Span<int> view = new Span<int>(stack, 4);
        view[0] = 10;
        return Sum(view) + view.Length;
    }
}
