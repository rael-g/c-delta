[InlineArray(3)]
struct Three
{
    public int Element;
}

static class Program
{
    static int Main()
    {
        int total = 0;
        foreach (int i in 0..4)
        {
            total += i;
        }

        Three three = default;
        three[0] = 4;
        three[2] = 1;
        foreach (int value in three)
        {
            total += value;
        }

        return total;
    }
}
