static class Program
{
    static int Add(int a, int b)
    {
        return a + b;
    }

    static int Main()
    {
        return Add(int.MaxValue, 1);
    }
}
