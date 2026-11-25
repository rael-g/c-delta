struct Num { public int V; }

static class NumOps
{
    extension(Num)
    {
        public static Num operator +(Num a, Num b) { return new Num { V = a.V + b.V }; }
    }
}

static class Program
{
    static int Main()
    {
        var a = new Num { V = 3 };
        var b = new Num { V = 4 };
        Num c = a + b;
        return c.V;
    }
}
