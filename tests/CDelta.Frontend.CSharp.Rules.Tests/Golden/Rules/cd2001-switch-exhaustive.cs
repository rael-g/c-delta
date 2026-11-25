enum Color { Red, Green }
[Flags]
enum Bits { A = 1, B = 2 }
static class M
{
    static int F(Color c)
    {
        switch (c)
        {
            case Color.Red: return 1;
        }

        switch (c)
        {
            case Color.Red: return 1;
            case Color.Green: return 2;
        }

        return 0;
    }

    static int G(Bits b)
    {
        switch (b)
        {
            case Bits.A: return 1;
            case Bits.B: return 2;
        }

        return 0;
    }
}
