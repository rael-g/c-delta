interface IShape
{
    int Area();

    int Scale(int factor);
}

struct Square : IShape
{
    public int Side;

    public int Area() => Side * Side;

    public int Scale(int factor) => Side * factor;
}

struct Rect : IShape
{
    public int Width;
    public int Height;

    public int Area() => Width * Height;

    public int Scale(int factor) => Width * Height * factor;
}

static class P
{
    static int Total(IShape shape) => shape.Area() + shape.Scale(2);

    static int Ignore(IShape first, IShape second) => 0;

    static IShape Erase<T>(ref T value) where T : IShape => Dyn<IShape>.Of(ref value);

    public static int Main()
    {
        Square square = default;
        square.Side = 3;
        Rect rect = default;
        rect.Width = 2;
        rect.Height = 3;
        IShape direct = Dyn<IShape>.Of(ref square);
        IShape erased = Erase(ref rect);
        IShape none = default;
        IShape empty = null;
        int nothing = Ignore(none, empty);
        return Total(direct) + Total(erased) + 9 + nothing;
    }
}
