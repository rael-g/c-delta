[Union]
interface IShape
{
}

struct Circle : IShape
{
}

struct Rect : IShape
{
}

static class Program
{
    static int Covered(IShape shape) => shape switch
    {
        Circle => 1,
        Rect => 2,
    };

    static int Missing(IShape shape) => shape switch
    {
        Circle => 1,
    };

    static int Discard(IShape shape) => shape switch
    {
        Circle => 1,
        _ => 0,
    };

    static int Statement(IShape shape)
    {
        switch (shape)
        {
            case Circle:
                return 1;
        }

        return 0;
    }
}
