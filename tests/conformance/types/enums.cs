enum Color
{
    Red,
    Green = 5,
    Blue,
}

[Flags]
enum Options
{
    None = 0,
    A = 1,
    B = 2,
    C = 4,
}

static class Program
{
    static int Weight(Color color)
    {
        switch (color)
        {
            case Color.Red:
                return 1;
            case Color.Green:
                return 10;
            case Color.Blue:
                return 100;
        }

        return 0;
    }

    static int Main()
    {
        Color green = Color.Green;
        int number = (int)Color.Blue;
        Options both = Options.A | Options.C;
        bool hasC = (both & Options.C) != Options.None;
        Color back = (Color)6;
        return Weight(green) + number + Weight(back) + (hasC ? 1000 : 0) - 1000 + (green == Color.Green ? 1 : 0);
    }
}
