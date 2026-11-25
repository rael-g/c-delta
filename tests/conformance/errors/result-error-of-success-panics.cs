static class Program
{
    static Result<int> Works() { return 1; }

    static int Main()
    {
        Error e = Works().Error;
        return e.Name.Length;
    }
}
