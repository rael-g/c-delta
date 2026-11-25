struct Cell
{
    public int Value;
}

static class Program
{
    static ref int Pick(ref Cell a, ref Cell b, bool first)
    {
        if (first) return ref a.Value;
        return ref b.Value;
    }

    static ref readonly int Peek(in Cell cell)
    {
        return ref cell.Value;
    }

    static int Main()
    {
        Cell x;
        x.Value = 1;
        Cell y;
        y.Value = 2;
        Pick(ref x, ref y, false) = 20;
        ref int alias = ref Pick(ref x, ref y, true);
        alias += 5;
        ref readonly int seen = ref Peek(in y);
        return x.Value + y.Value + seen;
    }
}
