static class P
{
    static int Size<T>(T value)
    {
        if (typeof(T) == typeof(int))
        {
            return 4;
        }

        if (typeof(T) != typeof(long))
        {
            return 1;
        }

        return 8;
    }

    static int Describe<T>(T value)
    {
        if (value is int i)
        {
            return i + 10;
        }

        if (value is long l)
        {
            return (int)l + 20;
        }

        return 0;
    }

    static int Kind<T>(T value) => value is float ? 3 : 0;

    public static int Main()
    {
        int sizes = Size(1) + Size(2L) + Size(true);
        int described = Describe(7) + Describe(5L) + Describe(1.5f);
        return sizes + described + Kind(2f) + Kind(1) - 16;
    }
}
