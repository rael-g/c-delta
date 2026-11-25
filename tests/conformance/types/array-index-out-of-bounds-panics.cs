static class Tables
{
    public static readonly int[] Values = [1, 2, 3];
}

static class Program
{
    static int Read(int index)
    {
        return Tables.Values[index];
    }

    static int Main()
    {
        return Read(3);
    }
}
