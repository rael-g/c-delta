enum E { Bad }

static class Program
{
    static Result<int, E> Fails() { return E.Bad; }

    static int Main()
    {
        return Fails().Value;
    }
}
