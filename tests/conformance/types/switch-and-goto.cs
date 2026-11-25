static class Program
{
    static int Classify(int value)
    {
        switch (value)
        {
            case 0:
                return 10;
            case 1:
            case 2:
                return 20;
            default:
                return 30;
        }
    }

    static int Loop(int n)
    {
        int total = 0;
        int i = 0;
    again:
        if (i >= n) goto done;
        total += i;
        i++;
        goto again;
    done:
        return total;
    }

    static int Main()
    {
        int sum = Classify(0) + Classify(2) + Classify(9);
        return sum + Loop(5);
    }
}
