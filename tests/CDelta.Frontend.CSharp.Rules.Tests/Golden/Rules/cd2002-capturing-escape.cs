delegate int Getter();

struct Query
{
    public Getter Source;

    public Query(Getter source)
    {
        Source = source;
    }
}

static class M
{
    public static Getter stored;

    static Getter Plain()
    {
        return () => 1;
    }

    static Getter Static(int value)
    {
        return static () => 2;
    }

    static Getter Returned(int value)
    {
        return () => value;
    }

    static Query Wrapped(int value)
    {
        Getter g = () => value;
        return new Query(g);
    }

    static void Stored(int value)
    {
        stored = () => value;
        Query local = new Query(() => value);
        int use = local.Source();
    }

    static void Out(int value, out Getter result)
    {
        result = () => value;
    }

    static int Fine(int value)
    {
        Getter g = () => value;
        Query q = new Query(g);
        return q.Source();
    }
}
