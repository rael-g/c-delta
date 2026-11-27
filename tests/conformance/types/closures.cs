static class P
{
    static int Count(ReadOnlySpan<int> items, Func<int, bool> keep)
    {
        int total = 0;
        foreach (int item in items)
        {
            if (keep(item))
            {
                total++;
            }
        }

        return total;
    }

    static int Run(Action step, int times)
    {
        for (int i = 0; i < times; i++)
        {
            step();
        }

        return times;
    }

    static int Scale(int factor, int value)
    {
        int Apply(int x) => x * factor;
        return Apply(value) + Apply(1);
    }

    public static int Main()
    {
        ReadOnlySpan<int> values = [3, 12, 25, 7, 40];
        int threshold = 10;
        int big = Count(values, x => x > threshold);
        int counter = 0;
        Run(() => counter += 5, 4);
        int bias = 2;
        int shifted = 0;
        Func<int, int> add = x => x + bias;
        shifted = add(shifted);
        bias = 3;
        shifted += add(1);
        return big * 10 + counter + shifted - 1 + Scale(2, 2) - 19;
    }
}
