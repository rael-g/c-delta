static class Program
{
    static int Add(int a, int b)
    {
        return a + b;
    }

    static int Main()
    {
        int wrapped = Add(int.MaxValue, 2);
        return wrapped == int.MinValue + 1 ? 7 : 1;
    }
}
