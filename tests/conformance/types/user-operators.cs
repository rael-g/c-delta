struct Vec
{
    public int X;
    public int Y;

    public static Vec operator +(Vec a, Vec b)
    {
        return new Vec { X = a.X + b.X, Y = a.Y + b.Y };
    }

    public static Vec operator -(Vec a)
    {
        return new Vec { X = -a.X, Y = -a.Y };
    }

    public static bool operator ==(Vec a, Vec b)
    {
        return a.X == b.X && a.Y == b.Y;
    }

    public static bool operator !=(Vec a, Vec b)
    {
        return !(a == b);
    }

    public static explicit operator int(Vec v)
    {
        return v.X + v.Y;
    }
}

static class Program
{
    static int Main()
    {
        var a = new Vec { X = 1, Y = 2 };
        var b = new Vec { X = 10, Y = 20 };
        Vec c = a + b;
        Vec d = -c;
        int total = (int)c;
        if (c == new Vec { X = 11, Y = 22 }) total += 100;
        if (a != b) total += 50;
        total += (int)d + 33;
        return total;
    }
}
