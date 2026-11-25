static class Counter
{
    public static int Count = 5;
    public static int Limit = 100;
    public static bool Flag;
}

static class Program
{
    static void Tick()
    {
        Counter.Count++;
        Counter.Flag = true;
    }

    static int Main()
    {
        Tick();
        Tick();
        return Counter.Count + (Counter.Flag ? 1 : 0) + Counter.Limit - 100;
    }
}
