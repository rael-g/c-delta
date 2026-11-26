static class Program
{
    static int Narrow(double value)
    {
        return (int)value;
    }

    static int Main()
    {
        return Narrow(1e20) == int.MaxValue && Narrow(-1e20) == int.MinValue && Narrow(0.0 / 0.0) == 0 ? 5 : 1;
    }
}
