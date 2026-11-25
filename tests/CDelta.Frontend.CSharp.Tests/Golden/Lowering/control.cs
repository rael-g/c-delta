static class Control
{
    static int Sum(int n)
    {
        int total = 0;
        for (int i = 0; i < n; i++)
        {
            if (i % 2 == 0) continue;
            total += i;
        }

        int j = n;
        while (j > 0)
        {
            j--;
            total = total + j;
        }

        return total;
    }

    static void Nothing()
    {
    }

    static void Main()
    {
        Nothing();
        Sum(5);
    }
}
