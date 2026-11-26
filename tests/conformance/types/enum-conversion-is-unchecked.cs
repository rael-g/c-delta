enum Color
{
    Red,
    Green,
}

static class Program
{
    static Color Make(int value)
    {
        return (Color)value;
    }

    static int Main()
    {
        Color color = Make(7);
        return color == Color.Red ? 0 : 1;
    }
}
