static class Program
{
    static int Sum(int n)
    {
        int total = 0;
        for (int i = 1; i <= n; i++)
        {
            total += i;
        }

        return total;
    }

    static int Main()
    {
        int a = Sum(10);
        long wide = a;
        bool flag = wide > 50 && a % 5 == 0;
        return flag ? a - 13 : 0;
    }
}
