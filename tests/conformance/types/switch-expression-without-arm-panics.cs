enum Color { Red, Green }

static class Program
{
    static int Pick(Color value)
    {
        return value switch
        {
            Color.Red => 10,
            Color.Green => 20,
        };
    }

    static int Main()
    {
        return Pick((Color)3);
    }
}
