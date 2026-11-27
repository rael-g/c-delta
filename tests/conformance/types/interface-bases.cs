interface IReader
{
    int Read();
}

interface IWriter
{
    int Write(int value);
}

interface IChannel : IReader, IWriter
{
    int Both(int value);
}

struct Pipe : IChannel
{
    public int Stored;

    public int Read() => Stored;

    public int Write(int value)
    {
        Stored = value;
        return value;
    }

    public int Both(int value) => Write(value) + Read();
}

[Union]
interface IShape : IReader
{
    int Area();
}

struct Square : IShape
{
    public int Side;

    public int Read() => Side;

    public int Area() => Side * Side;
}

static class P
{
    static int Through<T>(ref T channel) where T : IChannel => channel.Write(5) + channel.Read();

    static int Use(IChannel channel) => channel.Write(7) + channel.Read() + channel.Both(1);

    public static int Main()
    {
        Pipe pipe = default;
        Pipe other = default;
        IChannel dynamic = Dyn<IChannel>.Of(ref other);
        Square square = default;
        square.Side = 3;
        IShape shape = square;
        return Through(ref pipe) + Use(dynamic) + shape.Read() + shape.Area() + 4;
    }
}
