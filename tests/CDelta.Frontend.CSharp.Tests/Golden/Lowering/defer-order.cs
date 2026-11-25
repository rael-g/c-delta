static class Program
{
    public static int Counter;

    static void Body(int n)
    {
        Defer(() => Counter = Counter * 2);
        for (int i = 0; i < n; i++)
        {
            Defer(() => Counter += 1);
            if (i == 1) continue;
            Counter += 10;
        }
    }

    static int Early(bool leave)
    {
        int local = 5;
        Defer(() => Counter += local);
        if (leave) return 1;
        local = 100;
        return 2;
    }

    static int Main()
    {
        Body(3);
        int first = Early(true);
        int second = Early(false);
        return Counter + first + second;
    }
}
