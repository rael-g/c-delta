struct Pair
{
    public int A;
    public int B;
}

static class Program
{
    static int? Make(int v)
    {
        if (v > 0) return v;
        return null;
    }

    static Pair? Pick(bool yes)
    {
        if (yes) return new Pair { A = 3, B = 4 };
        return null;
    }

    static int Main()
    {
        int? a = Make(5);
        int? b = Make(0);
        int total = a ?? 100;
        total += b ?? 7;
        if (a is null) total += 1000;
        if (b is null) total += 1;
        if (a is { } got) total += got;
        if (a != null) total += 2;
        if (b == null) total += 3;
        Pair? p = Pick(true);
        int? first = p?.A;
        total += first ?? 0;
        b ??= 9;
        total += b.Value;
        int? sum = a;
        total += sum.Value;
        return total;
    }
}
