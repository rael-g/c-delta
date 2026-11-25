static class Program
{
    static int Pick(int v)
    {
        if (v == 0) return 1;
        throw Unreachable();
    }

    static int Main()
    {
        return Pick(2);
    }
}
