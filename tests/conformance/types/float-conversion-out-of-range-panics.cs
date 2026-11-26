static class Program
{
    static int Narrow(double value)
    {
        return (int)value;
    }

    static int Main()
    {
        return Narrow(1e20);
    }
}
