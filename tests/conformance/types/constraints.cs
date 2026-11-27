interface IShape
{
    int Area();
}

interface IZero<T>
{
    static abstract T Zero { get; }
}

struct Square : IShape, IZero<Square>
{
    public int Side;

    public int Area() => Side * Side;

    public static Square Zero => default;
}

struct Rect : IShape
{
    public int Width;
    public int Height;

    public int Area() => Width * Height;
}

static class P
{
    static int Measure<T>(ref T shape) where T : IShape => shape.Area();

    static T Empty<T>() where T : IZero<T> => T.Zero;

    public static int Main()
    {
        Square square = default;
        square.Side = 5;
        Rect rect = default;
        rect.Width = 3;
        rect.Height = 4;
        Square none = Empty<Square>();
        return Measure(ref square) + Measure(ref rect) + none.Side + 5;
    }
}
