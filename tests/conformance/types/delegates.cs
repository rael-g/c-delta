delegate int Binary(int a, int b);

static class P
{
    static int Add(int a, int b) => a + b;

    static int Apply(Binary f, int x, int y) => f(x, y);

    static int Twice(Func<int, int> f, int value) => f(f(value));

    static void Ignore(Action action) => action();

    public static int Main()
    {
        Binary add = Add;
        Func<int, int> inc = x => x + 1;
        Binary mul = static (a, b) => a * b;
        Ignore(() => { });
        return Apply(add, 10, 2) + Twice(inc, 8) + Apply(mul, 3, 6) + Apply(add, 1, 1);
    }
}
