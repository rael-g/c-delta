static unsafe class Program
{
    public static readonly int[] Table = [4, 5, 6];

    static int Main()
    {
        Span<int> local = [1, 2, 3];
        ReadOnlySpan<int> pair = [10, 20];
        int total = 0;
        foreach (int value in local)
        {
            total += value;
        }

        foreach (int value in Table)
        {
            total += value;
        }

        foreach (int value in pair)
        {
            total += value;
        }

        return total;
    }
}
