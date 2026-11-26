interface IZero<T>
{
    static abstract T Zero { get; }
}

struct Meters : IZero<Meters>
{
    public int Value;

    public static Meters Zero => new Meters { Value = 7 };
}

static class P
{
    public static int Main()
    {
        return Meters.Zero.Value;
    }
}
