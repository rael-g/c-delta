struct Vec2
{
    public int X;
    public int Y;
}

static class Program
{
    static int Main()
    {
        Vec2 a;
        a.X = 3;
        a.Y = 4;
        Vec2 b = a;
        b.X = 10;
        return a.X + a.Y + b.X;
    }
}
