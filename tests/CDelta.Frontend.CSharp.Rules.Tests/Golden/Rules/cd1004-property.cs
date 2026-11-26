interface IZero<T>
{
    static abstract T Zero { get; }
    static abstract T Bad { get; set; }
}

struct Meters : IZero<Meters>
{
    public int Value;

    public static Meters Zero => default;

    public static Meters Bad { get; set; }

    public static int Other => 1;

    public int Instance => Value;
}
