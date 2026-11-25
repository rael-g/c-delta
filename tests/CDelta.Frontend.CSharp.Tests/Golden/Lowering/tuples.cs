static class Program
{
    static (int Count, int Total) Stats(int a, int b, int c)
    {
        return (3, a + b + c);
    }

    static int Main()
    {
        var stats = Stats(1, 2, 3);
        (int count, int total) = Stats(10, 20, 30);
        var (x, y) = (4, 5);
        var pair = (Left: 7, Right: 8);
        pair.Left += 1;
        return stats.Count + stats.Total + count + total + x + y + pair.Left + pair.Right;
    }
}
