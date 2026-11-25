static class Program
{
    static int Divide(int a, int b)
    {
        return a / b;
    }

    static int Main()
    {
        return Divide(int.MinValue, -1);
    }
}
