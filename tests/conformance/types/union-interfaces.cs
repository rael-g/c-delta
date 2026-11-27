[Union]
interface IShape
{
}

struct Circle : IShape
{
    public int Radius;
}

struct Rect : IShape
{
    public int Width;
    public int Height;
}

static class P
{
    static int Area(IShape shape)
    {
        if (shape is Circle c)
        {
            return c.Radius * 3;
        }

        if (shape is Rect r)
        {
            return r.Width * r.Height;
        }

        return 0;
    }

    static int Kind(IShape shape) => shape switch
    {
        Circle => 1,
        Rect => 2,
        _ => 0,
    };

    public static int Main()
    {
        Circle circle = default;
        circle.Radius = 4;
        Rect rect = default;
        rect.Width = 5;
        rect.Height = 6;
        IShape first = circle;
        IShape second = rect;
        return Area(first) + Area(second) + Kind(first) * 100 - 100;
    }
}
