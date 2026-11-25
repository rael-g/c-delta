struct Vec2
{
    public int X;
    public int Y;

    public Vec2(int x, int y)
    {
        X = x;
        Y = y;
    }

    public readonly int Dot(Vec2 other)
    {
        return X * other.X + Y * other.Y;
    }

    public void Scale(int k)
    {
        X *= k;
        Y *= k;
    }

    public static Vec2 Add(Vec2 a, Vec2 b)
    {
        return new Vec2(a.X + b.X, a.Y + b.Y);
    }
}

static class Program
{
    static int Main()
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(3, 4);
        a.Scale(2);
        Vec2 c = Vec2.Add(a, b);
        Vec2 d = new Vec2 { X = 1 };
        return a.Dot(b) + c.X + c.Y + d.X + d.Y;
    }
}
