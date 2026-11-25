static class Program
{
    static int? Make(int v)
    {
        if (v > 0) return v;
        return null;
    }

    static int Main()
    {
        int? a = Make(5);
        int? b = Make(0);
        int? sum = a + 1;
        int? none = a + b;
        int? neg = -a;
        int total = sum.Value;
        if (none == null) total += 10;
        if (neg.Value == -5) total += 20;
        if (a == 5) total += 40;
        if (b == null) total += 1;
        if (a != b) total += 2;
        if (!(a < b)) total += 4;
        if (a > 4) total += 8;
        if (b == b) total += 100;
        return total;
    }
}
