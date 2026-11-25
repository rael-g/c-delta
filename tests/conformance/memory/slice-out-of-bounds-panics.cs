static class Program
{
    public static readonly int[] Table = [1, 2, 3];

    static int Main()
    {
        Span<int> whole = Table;
        int start = 2;
        int length = 5;
        Span<int> bad = whole.Slice(start, length);
        return bad.Length;
    }
}
