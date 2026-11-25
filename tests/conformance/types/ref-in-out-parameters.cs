struct Box
{
    public int Value;
}

static class Program
{
    static void Bump(ref int target, int by)
    {
        target += by;
    }

    static int Sum(in Box box, in int extra)
    {
        return box.Value + extra;
    }

    static void Split(int total, out int half, out int rest)
    {
        half = total / 2;
        rest = total - half;
    }

    static int Main()
    {
        int n = 5;
        Bump(ref n, 3);
        Box box;
        box.Value = 10;
        Bump(ref box.Value, 1);
        Split(9, out int half, out int rest);
        return n + Sum(in box, 2) + half * 10 + rest;
    }
}
