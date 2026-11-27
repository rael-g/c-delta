interface IShape
{
}

struct Square : IShape
{
}

static class Program
{
    static int Concrete<T>(T value) => value is int i ? i : typeof(T) == typeof(long) ? 1 : 0;

    static int Interface<T>(T value) => value is IShape ? 1 : 0;

    static int Declared<T>(T value) => value is IShape shape ? 1 : 0;
}
