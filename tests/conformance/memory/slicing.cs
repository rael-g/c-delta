static unsafe class Program
{
    public static readonly int[] Table = [1, 2, 3, 4, 5];

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
        Span<int> whole = Table;
        Span<int> middle = whole.Slice(1, 3);
        Span<int> rest = whole.Slice(2);
        Span<int> range = whole[1..4];
        Span<int> tail = whole[3..];
        middle[0] = 20;
        int last = whole[^1];
        return Sum(whole) + Sum(middle) + rest.Length + range[2] + tail.Length + last;
    }
}
