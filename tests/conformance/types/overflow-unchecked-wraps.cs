static class Program
{
    static int Add(int a, int b)
    {
        return unchecked(a + b);
    }

    static int Main()
    {
        int sum = Add(int.MaxValue, 1);
        return sum == int.MinValue ? 7 : 1;
    }
}
