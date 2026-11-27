interface IGet<T>
{
    T Get();
}

interface IZero<T>
{
    static abstract T Zero { get; }
}

struct Box<T> : IGet<T>, IZero<Box<T>>
{
    public T Value;

    public T Get() => Value;

    public static Box<T> Zero => default;
}

static class P
{
    static T Read<TBox, T>(ref TBox box) where TBox : IGet<T> => box.Get();

    static TBox Empty<TBox>() where TBox : IZero<TBox> => TBox.Zero;

    public static int Main()
    {
        Box<int> a = default;
        a.Value = 40;
        Box<long> b = default;
        b.Value = 2;
        Box<int> none = Empty<Box<int>>();
        return Read<Box<int>, int>(ref a) + (int)Read<Box<long>, long>(ref b) + none.Value;
    }
}
