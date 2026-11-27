struct Counter
{
    public int Count;

    public void Add(int n)
    {
        Count += n;
    }

    public static void Reset(int n)
    {
    }
}

interface ISource<out T>
{
}

delegate void Handler(int value);

static unsafe class Program
{
    static void Run(Counter counter)
    {
        Handler fromInstance = counter.Add;
        Handler fromStatic = Counter.Reset;
        Handler fromLambda = n => counter.Add(n);
        delegate* unmanaged<int, void> bare = null;
        delegate* unmanaged[Cdecl]<int, void> named = null;
        delegate*<int, void> plain = null;
    }
}
