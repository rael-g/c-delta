struct Box<T>
{
    public T Value;

    public T Get() => Value;

    public static Box<T> Of(T value)
    {
        Box<T> box = default;
        box.Value = value;
        return box;
    }
}

struct Pair<A, B>
{
    public A First;
    public B Second;
}

static class P
{
    static T Identity<T>(T value) => value;

    static T Pick<T>(T a, T b) => b;

    static T Twice<T>(T value) => Pick(value, Identity(value));

    static Pair<A, B> Make<A, B>(A first, B second)
    {
        Pair<A, B> pair = default;
        pair.First = first;
        pair.Second = second;
        return pair;
    }

    public static int Main()
    {
        int a = Identity<int>(20);
        long b = Twice(11L);
        Box<int> box = Box<int>.Of(4);
        Pair<int, long> pair = Make(5, 2L);
        return a + (int)b + box.Get() + pair.First + (int)pair.Second;
    }
}
