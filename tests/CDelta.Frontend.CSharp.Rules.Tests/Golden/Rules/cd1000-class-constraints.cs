interface IShape
{
}

struct Square : IShape
{
}

static class Program
{
    static void A<T>(T value) where T : class
    {
    }

    static void B<T>(T value) where T : class?
    {
    }

    static void C<T>(T value) where T : notnull
    {
    }

    static void D<T>(T value) where T : System.Attribute
    {
    }

    static void E<T>(T value) where T : struct, IShape
    {
    }

    static void F<T>(T value) where T : IShape, new()
    {
    }

    static void G<T>(T value) where T : unmanaged
    {
    }
}
