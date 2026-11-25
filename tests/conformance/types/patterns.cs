struct Point
{
    public int X;
    public int Y;
}

static class Program
{
    static int Describe(int value)
    {
        if (value is > 0 and < 10) return 1;
        if (value is 10 or 20) return 2;
        if (value is not 0 and var other && other > 100) return 3;
        return 0;
    }

    static int Grade(int score) => score switch
    {
        >= 90 => 4,
        >= 80 => 3,
        >= 70 => 2,
        _ => 1,
    };

    static int Where(Point p)
    {
        if (p is { X: 0, Y: 0 }) return 100;
        if (p is { X: > 0 }) return 200;
        return 300;
    }

    static int Main()
    {
        Point origin;
        origin.X = 0;
        origin.Y = 0;
        Point right;
        right.X = 5;
        right.Y = 1;
        return Describe(5) + Describe(20) + Describe(500) + Grade(85) + Where(origin) / 100 + Where(right) / 100;
    }
}
