enum Color
{
    Red,
    Green,
}

static class Program
{
    static int Pick(Color color)
    {
        switch (color)
        {
            case Color.Red:
                return 1;
        }

        return 0;
    }

    static void Main()
    {
        Pick(Color.Green);
    }
}
